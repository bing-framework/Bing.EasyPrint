using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bing.EasyPrint.Templates;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

internal static class DesignerRequestProcessor
{
    internal const long MaxRequestBytes = 44L * 1024 * 1024;
    private const int MaxDataBytes = 1024 * 1024;
    private const int MaxResourceCount = 64;
    private const long MaxTotalResourceBytes = 32L * 1024 * 1024;

    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    internal static IResult Preview(DesignerRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var input = Prepare(request);
            var result = new TemplateRenderer().RenderWithLayout(input.Template, input.Data, input.Resources,
                input.Options, cancellationToken);
            return Results.Json(new
            {
                pages = result.Pages.Select(page => new
                {
                    width = page.Width,
                    height = page.Height,
                    elements = page.Elements.Select(element => new
                    {
                        sourcePath = element.SourcePath,
                        kind = element.Kind,
                        type = element.Type,
                        instanceIndices = element.InstanceIndices,
                        x = element.X,
                        y = element.Y,
                        width = element.Width,
                        height = element.Height,
                        text = element.Text,
                        resourceKey = element.ResourceKey
                    })
                })
            }, WebJson);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) { return Error(error); }
    }

    internal static IResult Render(DesignerRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var input = Prepare(request);
            var result = new TemplateRenderer().RenderWithLayout(input.Template, input.Data, input.Resources,
                input.Options, cancellationToken);
            using var archiveStream = new MemoryStream();
            using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, true))
            {
                for (var index = 0; index < result.Pages.Count; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var entry = archive.CreateEntry($"page-{index + 1:000}.cpcl", CompressionLevel.Fastest);
                    using var target = entry.Open();
                    target.Write(result.Pages[index].Data);
                }
            }
            return Results.File(archiveStream.ToArray(), "application/zip", "easyprint-pages.zip");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) { return Error(error); }
    }

    internal static IResult GetSamples()
    {
        try
        {
            var root = Path.Combine(AppContext.BaseDirectory, "Samples");
            var loader = new TemplateLoader();
            var labels = new PriceLabelRenderer();
            var defaultPrice = new PriceLabelData
            {
                Name = "三全高端汤圆炫彩小汤圆混装口味黑芝麻、花生、紫薯、抹茶",
                Barcode = "092836193615302",
                Code = "GT-001",
                QrCode = "https://go.example/p/0001",
                OriginPrice = 300m,
                SalePrice = 250m,
                MemberPrice = 200.03m,
                Unit = "袋",
                Specification = "250ml*16",
                Origin = "杭州"
            };
            var pm0025 = new PriceLabelData
            {
                Name = "万代（BANDAI）MG 高达2 拼装模型 180CM（拆封无售后）红龙异端",
                Barcode = "1208000780",
                Code = "1208000780",
                OriginPrice = 10.99m,
                SalePrice = 8.89m,
                Unit = "袋",
                Specification = "132g/袋",
                Origin = "广东广州"
            };
            var pm0034 = new PriceLabelData
            {
                Name = "三全高端汤圆炫彩小汤圆混装口味黑芝麻、花生、紫薯、抹茶、麻薯、巧克力、豆沙",
                Barcode = "092836193615302",
                QrCode = "编码-二维码",
                OriginPrice = 300.02m,
                SalePrice = 250.01m,
                MemberPrice = 200.03m,
                Specification = "250ml*16",
                Origin = "中国"
            };

            var samples = new List<DesignerSample>
            {
                FromFile("label", "固定商品标签", "label.json", new { Product = new { Name = "黑芝麻汤圆 250g", Code = "6901234567890" } }),
                FromFile("receipt", "分页小票", "receipt.json", new { Title = "零售小票", Items = new[] { new { Name = "黑芝麻汤圆", Quantity = 2 }, new { Name = "糯米粉", Quantity = 1 }, new { Name = "红糖", Quantity = 3 } } }),
                FromTemplate("price-label-15", "15mm 价签", PriceLabelTemplateFactory.Create(PriceLabelPreset.Mm15), labels.PrepareData(defaultPrice)),
                FromTemplate("price-label-70", "70mm 价签", PriceLabelTemplateFactory.Create(PriceLabelPreset.Mm70), labels.PrepareData(defaultPrice)),
                FromTemplate("price-label-90", "90mm 价签", PriceLabelTemplateFactory.Create(PriceLabelPreset.Mm90), labels.PrepareData(defaultPrice)),
                FromFile("pm0025", "PM0025 价签重制样例", "price-label-pm0025.json", labels.PrepareData(pm0025)),
                FromFile("pm0034", "PM0034 价签重制样例", "price-label-pm0034.json", labels.PrepareData(pm0034))
            };
            return Results.Json(new { samples }, WebJson);

            DesignerSample FromFile(string id, string name, string file, object data)
            {
                var json = File.ReadAllText(Path.Combine(root, file), Encoding.UTF8);
                return new DesignerSample(id, name, JsonNode.Parse(json)!, ToNode(data));
            }

            DesignerSample FromTemplate(string id, string name, Bing.EasyPrint.Model.BpTemplate template, object data)
                => new(id, name, JsonNode.Parse(loader.SaveJson(template))!, ToNode(data));
        }
        catch (Exception error) { return Error(error); }
    }

    private static PreparedRequest Prepare(DesignerRequest request)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        if (request.Template.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("template 必须为模板 JSON 对象。");
        if (request.Data.ValueKind == JsonValueKind.Undefined)
            throw new ArgumentException("data 必须包含 JSON 样例数据。");
        var templateJson = request.Template.GetRawText();
        var dataJson = request.Data.GetRawText();
        if (Encoding.UTF8.GetByteCount(dataJson) > MaxDataBytes)
            throw new ArgumentException("样例数据超过 1 MiB 上限。");

        var options = new TemplateRenderOptions();
        var template = new TemplateLoader().LoadJsonWithPaths(templateJson, options);
        var data = JToken.Parse(dataJson).ToObject<object>();
        var resources = DecodeResources(request.Resources);
        return new PreparedRequest(template, data, new MemoryResources(resources), options);
    }

    private static Dictionary<string, byte[]> DecodeResources(Dictionary<string, string>? encodedResources)
    {
        var decoded = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        if (encodedResources is null) return decoded;
        if (encodedResources.Count > MaxResourceCount)
            throw new ArgumentException($"资源数量不能超过 {MaxResourceCount} 个。");

        long totalBytes = 0;
        foreach (var pair in encodedResources)
        {
            if (string.IsNullOrWhiteSpace(pair.Key) || pair.Key.Length > 256)
                throw new ArgumentException("资源键长度必须为 1–256 个字符。");
            byte[] bytes;
            try { bytes = Convert.FromBase64String(pair.Value ?? string.Empty); }
            catch (FormatException error) { throw new ArgumentException("图片资源必须使用有效的 Base64 数据。", error); }
            if (bytes.Length > 10 * 1024 * 1024)
                throw new ArgumentException($"资源“{pair.Key}”超过单图 10 MiB 上限。");
            totalBytes = checked(totalBytes + bytes.Length);
            if (totalBytes > MaxTotalResourceBytes)
                throw new ArgumentException("本次请求的图片资源总量超过 32 MiB 上限。");
            decoded.Add(pair.Key, bytes);
        }
        return decoded;
    }

    private static JsonNode ToNode(object value)
        => JsonNode.Parse(JsonConvert.SerializeObject(value, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            Formatting = Formatting.None
        }))!;

    private static IResult Error(Exception error)
    {
        var path = error is TemplateRenderException renderError ? renderError.SourcePath : null;
        var message = error is Newtonsoft.Json.JsonException
            ? "模板 JSON 格式无效或包含不支持的字段。"
            : error.Message;
        return Results.Json(new { errors = new[] { new { sourcePath = path, message } } }, WebJson, statusCode: 400);
    }

    private sealed record PreparedRequest(Bing.EasyPrint.Model.BpTemplate Template, object? Data,
        ITemplateResourceProvider Resources, TemplateRenderOptions Options);

    private sealed record DesignerSample(string Id, string Name, JsonNode Template, JsonNode Data);

    private sealed class MemoryResources(Dictionary<string, byte[]> resources) : ITemplateResourceProvider
    {
        public Stream OpenRead(string resourceKey, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return resources.TryGetValue(resourceKey, out var bytes) ? new MemoryStream(bytes, false) : null!;
        }
    }
}

internal sealed class DesignerRequest
{
    public JsonElement Template { get; set; }
    public JsonElement Data { get; set; }
    public Dictionary<string, string>? Resources { get; set; }
}
