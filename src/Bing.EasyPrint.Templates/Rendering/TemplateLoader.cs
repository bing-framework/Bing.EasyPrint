using System;
using System.IO;
using System.Linq;
using System.Text;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bing.EasyPrint.Templates;

/// <summary>
/// 读写带版本号的 JSON 模板。
/// </summary>
/// <remarks>
/// 加载器只读取声明的数据，不执行模板中的代码。
/// </remarks>
public sealed class TemplateLoader
{
    /// <summary>
    /// 加载并校验 JSON 模板。
    /// </summary>
    /// <param name="json">带版本号的模板 JSON。</param>
    /// <param name="options">可选的 JSON 大小、深度和模板资源限制。</param>
    /// <returns>可供后续渲染的模板。</returns>
    public BpTemplate LoadJson(string json, TemplateRenderOptions options = null)
        => LoadJsonCore(json, options, false);

    /// <summary>
    /// 加载模板并保留结构错误的节点路径。
    /// </summary>
    /// <param name="json">带版本号的模板 JSON。</param>
    /// <param name="options">可选的 JSON 大小、深度和模板资源限制。</param>
    /// <returns>可供后续渲染的模板。</returns>
    public BpTemplate LoadJsonWithPaths(string json, TemplateRenderOptions options = null)
        => LoadJsonCore(json, options, true);

    /// <summary>
    /// 解析 JSON 并校验模板结构。
    /// </summary>
    /// <param name="json">待解析的模板 JSON。</param>
    /// <param name="options">加载限制。</param>
    /// <param name="includePaths">是否在结构错误中附带节点路径。</param>
    /// <returns>规范化后的模板。</returns>
    private static BpTemplate LoadJsonCore(string json, TemplateRenderOptions options, bool includePaths)
    {
        options = options ?? new TemplateRenderOptions();
        options.Validate();
        if (json == null) throw new ArgumentNullException(nameof(json));
        if (Encoding.UTF8.GetByteCount(json) > options.MaxJsonBytes)
            throw new ArgumentException("JSON 超过大小上限。", nameof(json));
        using (var reader = new JsonTextReader(new StringReader(json)) { MaxDepth = checked(options.MaxDepth * 8 + 16), DateParseHandling = DateParseHandling.None })
        {
            var root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            if (reader.Read()) throw new JsonSerializationException("模板后存在额外 JSON 内容。");
            if (root["Version"]?.Type != JTokenType.Integer || (root.Value<int>("Version") != 1 && root.Value<int>("Version") != 2 && root.Value<int>("Version") != 3))
                throw new JsonSerializationException("必须指定 Version: 1、2 或 3。");
            foreach (var property in root.Descendants().OfType<JProperty>())
                if (property.Name.StartsWith("$", StringComparison.Ordinal) || string.Equals(property.Name, "ValueFunc", StringComparison.OrdinalIgnoreCase))
                    throw new JsonSerializationException("JSON 不允许类型元数据、引用或委托。");
            var template = root.ToObject<BpTemplate>(CreateSerializer(includePaths));
            Normalize(template.Rows);
            if (template.Components != null) foreach (var component in template.Components)
            {
                if (component == null) continue;
                Normalize(component.Rows);
                NormalizeElements(component.Elements);
            }
            TemplateGraph.Validate(template, options, false, includePaths: includePaths);
            return template;
        }
    }

    /// <summary>
    /// 校验模板并保存为 JSON。
    /// </summary>
    /// <param name="template">待保存的模板。</param>
    /// <param name="options">可选的 JSON 大小、深度和模板资源限制。</param>
    /// <returns>保留模板版本号的 JSON 字符串。</returns>
    public string SaveJson(BpTemplate template, TemplateRenderOptions options = null)
    {
        options = options ?? new TemplateRenderOptions();
        options.Validate();
        TemplateGraph.Validate(template, options, true);
        var result = new StringBuilder();
        using (var writer = new JsonTextWriter(new LimitedWriter(result, options.MaxJsonBytes)) { Formatting = Formatting.Indented })
            CreateSerializer().Serialize(writer, template);
        var json = result.ToString();
        if (Encoding.UTF8.GetByteCount(json) > options.MaxJsonBytes)
            throw new ArgumentException("JSON 超过大小上限。", nameof(template));
        return json;
    }

    /// <summary>
    /// 创建限制类型元数据并识别模板元素的序列化器。
    /// </summary>
    /// <returns>模板专用序列化器。</returns>
    private static JsonSerializer CreateSerializer(bool includePaths = false)
    {
        var serializer = new JsonSerializer
        {
            TypeNameHandling = TypeNameHandling.None,
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Error,
            NullValueHandling = NullValueHandling.Ignore,
            DateParseHandling = DateParseHandling.None,
            FloatParseHandling = FloatParseHandling.Decimal
        };
        serializer.Converters.Add(new ElementConverter(includePaths));
        return serializer;
    }

    /// <summary>
    /// 将旧元素容器转换为唯一的元素列表。
    /// </summary>
    /// <param name="rows">需要递归处理的行。</param>
    private static void Normalize(System.Collections.Generic.List<BpRow> rows)
    {
        if (rows == null) return;
        foreach (var row in rows)
        {
            if (row?.Cells == null) continue;
            foreach (var cell in row.Cells)
            {
                if (cell == null) continue;
                if (cell.ElementAlls != null && (cell.Elements == null || cell.Elements.Count == 0))
                {
                    cell.Elements = new System.Collections.Generic.List<BpElement>();
                    foreach (var all in cell.ElementAlls)
                    {
                        if (all == null) throw new JsonSerializationException("ElementAlls 不能包含 null。");
                        var elements = new BpElement[] { all.FontElement, all.ImageElement, all.QrCodeElement, all.BarcodeElement }.Where(x => x != null).ToArray();
                        if (elements.Length != 1) throw new JsonSerializationException("ElementAlls 每项必须恰有一个元素。");
                        cell.Elements.Add(elements[0]);
                    }
                }
                cell.ElementAlls = null;
                Normalize(cell.Rows);
                NormalizeElements(cell.Elements);
            }
        }
    }

    /// <summary>
    /// 规范化组合中的嵌套行列。
    /// </summary>
    /// <param name="elements">需要递归处理的元素。</param>
    private static void NormalizeElements(System.Collections.Generic.List<BpElement> elements)
    {
        if (elements == null) return;
        foreach (var group in elements.OfType<BpGroupElement>())
        {
            Normalize(group.Rows);
            NormalizeElements(group.Elements);
        }
    }

    private const string QuietZoneError = "QuietZone 仅用于二维码或条码，且只能包含非负整数打印点 Left、Top、Right、Bottom。";

    // 只检查实际元素，组件参数中的同名业务字段不属于打印设置。
    private static string InvalidQuietZonePath(JObject token)
    {
        var quietZone = token["QuietZone"];
        if (quietZone == null || quietZone.Type == JTokenType.Null) return null;
        var type = token["Type"];
        if (type?.Type != JTokenType.Integer || type.ToString() != "3" && type.ToString() != "4" ||
            !(quietZone is JObject zone)) return quietZone.Path;
        foreach (var property in zone.Properties())
            if (property.Name != "Left" && property.Name != "Top" && property.Name != "Right" && property.Name != "Bottom" ||
                property.Value.Type != JTokenType.Integer ||
                !long.TryParse(property.Value.ToString(), out var dots) || dots < 0 || dots > int.MaxValue)
                return property.Path;
        return null;
    }

    /// <summary>
    /// 按模板元素类型标识选择具体元素类型。
    /// </summary>
    private sealed class ElementConverter : JsonConverter
    {
        private readonly bool includePaths;
        private string parentPath;

        internal ElementConverter(bool includePaths) => this.includePaths = includePaths;

        /// <inheritdoc />
        public override bool CanConvert(Type objectType) => objectType == typeof(BpElement);
        /// <inheritdoc />
        public override bool CanWrite => false;
        /// <inheritdoc />
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var elementPath = string.IsNullOrEmpty(parentPath) ? reader.Path : parentPath + "." + reader.Path;
            var token = JObject.Load(reader);
            if (token["Type"]?.Type != JTokenType.Integer) throw new JsonSerializationException("元素必须指定整数 Type。");
            var quietZonePath = InvalidQuietZonePath(token);
            if (quietZonePath != null)
            {
                if (includePaths)
                    throw new TemplateRenderException(elementPath + "." + quietZonePath, QuietZoneError,
                        new JsonSerializationException(QuietZoneError));
                throw new JsonSerializationException(QuietZoneError);
            }
            if (token.Value<int>("Type") == 1 && token.Property("Font") != null)
            {
                if (!(token["Font"] is JObject font) || font["Kind"]?.Type != JTokenType.Integer ||
                    (font["Kind"].ToString() != "0" && font["Kind"].ToString() != "1" && font["Kind"].ToString() != "2") ||
                    font.Properties().Any(property => property.Name != "Kind" && property.Name != "Name"))
                    throw new JsonSerializationException("Font 必须仅包含整数 Kind（0、1、2）及可选 Name。");
                if (font.Value<int>("Kind") == 0 ? font.Property("Name") != null :
                    font["Name"]?.Type != JTokenType.String || string.IsNullOrWhiteSpace(font.Value<string>("Name")))
                    throw new JsonSerializationException("默认字体不能包含 Name；设备或主机字体必须提供非空字符串 Name。");
                if (token.Property("DeviceFontName") != null ||
                    token.Property("FontName") != null && (token["FontName"].Type != JTokenType.Integer || token["FontName"].ToString() != "0") ||
                    token["FontFamily"] != null && token["FontFamily"].Type != JTokenType.Null &&
                    (token["FontFamily"].Type != JTokenType.String || token.Value<string>("FontFamily") != ""))
                    throw new JsonSerializationException("Font 不能同时指定旧版字体字段。");
            }
            if (token["Filled"] is JToken filled &&
                (filled.Type != JTokenType.Boolean || token.Value<int>("Type") != 7 && filled.Value<bool>()))
                throw new JsonSerializationException("Filled 必须为布尔值，且仅矩形元素支持填充。");
            if (token.Value<int>("Type") == 2 && token["Rotation"] is JToken imageRotation &&
                (imageRotation.Type != JTokenType.Integer ||
                 !new long[] { 0, 90, 180, 270 }.Contains(imageRotation.Value<long>())))
                throw new JsonSerializationException("图片 Rotation 必须为整数 0、90、180 或 270。");
            if (token["Rotation"] is JToken rotation && token.Value<int>("Type") != 1 &&
                token.Value<int>("Type") != 4 && token.Value<int>("Type") != 2 &&
                token.Value<int>("Type") != 3 &&
                (rotation.Type != JTokenType.Integer || rotation.Value<long>() != 0))
                throw new JsonSerializationException("此元素类型不支持 Rotation；旋转仅用于文字、图片、二维码和一维条码。");
            BpElement element;
            switch (token.Value<int>("Type"))
            {
                case 1: element = new BpFontElement(); break;
                case 2: element = new BpImageElement(); break;
                case 3: element = new BpQrCodeElement(); break;
                case 4: element = new BpBarcodeElement(); break;
                case 5: element = new BpLineElement(); break;
                case 6: element = new BpGroupElement(); break;
                case 7: element = new BpRectangleElement(); break;
                case 8: element = new BpEllipseElement(); break;
                case 9: element = new BpIconElement(); break;
                case 10: element = new BpMatrixCodeElement(); break;
                default: throw new JsonSerializationException("不支持的元素 Type。");
            }
            // Populate 会从局部 token 创建读取器，嵌套组的路径须加上当前元素的外层路径。
            var previousPath = parentPath;
            parentPath = elementPath;
            try
            {
                using (var childReader = token.CreateReader()) serializer.Populate(childReader, element);
            }
            finally { parentPath = previousPath; }
            return element;
        }
        /// <inheritdoc />
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) => throw new NotSupportedException();
    }

    /// <summary>
    /// 限制模板 JSON 序列化期间的字符缓冲区长度。
    /// </summary>
    private sealed class LimitedWriter : StringWriter
    {
        /// <summary>
        /// 保存允许写入的字符数上限。
        /// </summary>
        private readonly int limit;
        /// <summary>
        /// 初始化一个 <see cref="LimitedWriter"/> 类型的实例。
        /// </summary>
        /// <param name="builder">接收 JSON 的缓冲区。</param>
        /// <param name="limit">最大字符数。</param>
        internal LimitedWriter(StringBuilder builder, int limit) : base(builder) { this.limit = limit; }
        /// <inheritdoc />
        public override void Write(char value)
        {
            if (GetStringBuilder().Length >= limit) throw new ArgumentException("JSON 超过大小上限。");
            base.Write(value);
        }
        /// <inheritdoc />
        public override void Write(string value)
        {
            if ((long)GetStringBuilder().Length + (value?.Length ?? 0) > limit) throw new ArgumentException("JSON 超过大小上限。");
            base.Write(value);
        }
        /// <inheritdoc />
        public override void Write(char[] buffer, int index, int count)
        {
            if ((long)GetStringBuilder().Length + count > limit) throw new ArgumentException("JSON 超过大小上限。");
            base.Write(buffer, index, count);
        }
    }
}
