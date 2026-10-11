using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Templates;

/// <summary>
/// 生成 V3 组合组件和流式小票示例。
/// </summary>
internal static class V3Samples
{
    /// <summary>
    /// 渲染 V3 C# 模板和等价 JSON 模板并写入示例文件。
    /// </summary>
    /// <param name="output">示例 CPCL 文件的输出目录。</param>
    /// <param name="loader">用于加载 JSON 模板的模板加载器。</param>
    /// <param name="renderer">用于生成 CPCL 页面的模板渲染器。</param>
    /// <remarks>
    /// 方法会比较两种模板的页数和页面字节，并在不一致时抛出异常。
    /// </remarks>
    internal static void WriteTo(string output, TemplateLoader loader, TemplateRenderer renderer)
    {
        var code = new BpTemplate { Version = 3, Width = 320, Height = 120,
            Components = new List<BpComponentDefinition> {
                new BpComponentDefinition { Id = "orderLines", Width = 320,
                    Parameters = new Dictionary<string, BpComponentParameterDefinition> {
                        ["heading"] = new BpComponentParameterDefinition { Required = true } },
                    Rows = new List<BpRow> {
                        TextRow("$param.heading", "TableHeader", null),
                        TextRow("Name", "Body", "Items") } } },
            Rows = new List<BpRow> { TextRow("Shop", null, null),
                new BpRow { Cells = new List<BpCell> { new BpCell { Elements = new List<BpElement> {
                    new BpGroupElement { ComponentId = "orderLines", DataPath = "Order",
                        Pagination = BpGroupPagination.Split,
                        Parameters = new Dictionary<string, BpComponentParameterBinding> {
                            ["heading"] = new BpComponentParameterBinding { Path = "Labels.ItemTitle" } } } } } } },
                TextRow("Footer", null, null) } };
        var json = loader.LoadJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "v3-flowing-receipt.json")));
        var data = new { Shop = "北店", Labels = new { ItemTitle = "商品明细" }, Footer = "欢迎再来",
            Order = new { Items = Enumerable.Range(1, 9).Select(index => new { Name = "商品 " + index }).ToArray() } };
        var fromCode = renderer.Render(code, data);
        var fromJson = renderer.Render(json, data);
        if (fromCode.PageCount != fromJson.PageCount || fromCode.Pages.Where((page, index) =>
            !page.Data.SequenceEqual(fromJson.Pages[index].Data)).Any())
            throw new Exception("V3 C# 与 JSON 输出不一致。");
        for (var index = 0; index < fromCode.PageCount; index++)
            File.WriteAllBytes(Path.Combine(output, $"v3-receipt-{index + 1}.cpcl"), fromCode.Pages[index].Data);
    }

    /// <summary>
    /// 创建包含单个文字元素的模板行。
    /// </summary>
    /// <param name="path">文字字段或静态值路径。</param>
    /// <param name="section">模板行所属的布局区段。</param>
    /// <param name="repeatSource">模板行使用的重复数据源。</param>
    /// <returns>包含单个文字元素的模板行。</returns>
    private static BpRow TextRow(string path, string section, string repeatSource) => new BpRow {
        Section = section, RepeatSource = repeatSource, Height = 28,
        Cells = new List<BpCell> { new BpCell { Elements = new List<BpElement> {
            new BpFontElement { FontSize = 24, Field = new BgField { Name = path } } } } } };
}
