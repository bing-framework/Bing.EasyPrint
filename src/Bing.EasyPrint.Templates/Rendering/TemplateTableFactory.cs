using System;
using System.Collections.Generic;
using System.Linq;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;

namespace Bing.EasyPrint.Templates;

/// <summary>
/// 将表格列定义转换为可编辑的模板行。
/// </summary>
/// <remarks>
/// 明细沿用模板的重复与分页能力。
/// </remarks>
public static class TemplateTableFactory
{
    /// <summary>
    /// 创建可插入单元格或组件定义的流式表格组合。
    /// </summary>
    /// <param name="repeatSource">明细集合的字段路径。</param>
    /// <param name="columns">表格列定义。</param>
    /// <param name="headerHeight">表头高度，单位为打印点。</param>
    /// <param name="detailHeight">明细高度，单位为打印点。</param>
    /// <param name="repeatHeader">是否在跨页时重复表头。</param>
    /// <returns>包含表头和明细行的组合。</returns>
    public static BpGroupElement CreateGroup(string repeatSource, IReadOnlyList<TemplateTableColumn> columns,
        int headerHeight = 30, int detailHeight = 30, bool repeatHeader = true)
        => new BpGroupElement { Rows = Create(repeatSource, columns, headerHeight, detailHeight, repeatHeader).ToList(),
            Pagination = BpGroupPagination.Split };

    /// <summary>
    /// 创建表头与重复明细行。
    /// </summary>
    /// <param name="repeatSource">指向明细集合的字段路径。</param>
    /// <param name="columns">一至六十四项表格列定义。</param>
    /// <param name="headerHeight">表头打印点高度，必须大于零。</param>
    /// <param name="detailHeight">明细行打印点高度，必须大于零。</param>
    /// <param name="repeatHeader">是否在明细跨页时重复表头。</param>
    /// <returns>按表头、明细顺序排列的可编辑模板行。</returns>
    public static IReadOnlyList<BpRow> Create(string repeatSource, IReadOnlyList<TemplateTableColumn> columns,
        int headerHeight = 30, int detailHeight = 30, bool repeatHeader = true)
    {
        if (string.IsNullOrWhiteSpace(repeatSource)) throw new ArgumentException("表格需要明细字段路径。", nameof(repeatSource));
        if (columns == null || columns.Count == 0 || columns.Count > 64 || columns.Any(column => column == null ||
            string.IsNullOrWhiteSpace(column.Title) || string.IsNullOrWhiteSpace(column.FieldPath) || column.Width < 0))
            throw new ArgumentException("表格列定义无效。", nameof(columns));
        if (headerHeight < 1 || detailHeight < 1) throw new ArgumentException("表格行高必须为正数。");
        var header = new BpRow { Section = repeatHeader ? "TableHeader" : "Body", Height = headerHeight, Cells = new List<BpCell>() };
        var detail = new BpRow { Section = "Body", RepeatSource = repeatSource, Height = detailHeight, Cells = new List<BpCell>() };
        foreach (var column in columns)
        {
            var border = new BpBorder { TopStyle = 1, RightStyle = 1, BottomStyle = 1, LeftStyle = 1 };
            header.Cells.Add(new BpCell { Width = column.Width, Border = border, Elements = new List<BpElement> {
                new BpFontElement { FontSize = 24, TextAlignment = column.Alignment, Field = new BgField { Value = column.Title } } } });
            detail.Cells.Add(new BpCell { Width = column.Width, Border = new BpBorder { RightStyle = 1, BottomStyle = 1, LeftStyle = 1 }, Elements = new List<BpElement> {
                new BpFontElement { FontSize = 24, TextAlignment = column.Alignment, Field = new BgField { Name = column.FieldPath } } } });
        }
        return new[] { header, detail };
    }
}

/// <summary>
/// 定义可编辑表格的一列。
/// </summary>
public sealed class TemplateTableColumn
{
    /// <summary>
    /// 获取或设置表头显示文字。
    /// </summary>
    public string Title { get; set; }
    /// <summary>
    /// 获取或设置明细项的字段路径。
    /// </summary>
    public string FieldPath { get; set; }
    /// <summary>
    /// 获取或设置打印点宽度；零表示由父级自动分配。
    /// </summary>
    public int Width { get; set; }
    /// <summary>
    /// 获取或设置单元格中文字的对齐方式。
    /// </summary>
    public BpTextAlignment Alignment { get; set; }
}
