using System.Collections.Generic;

namespace Bing.EasyPrint.Model
{
    /// <summary>
    /// 定义模板中的一行布局，可包含单元格或重复数据项。
    /// </summary>
    public class BpRow
    {
        /// <summary>
        /// 获取或设置重复数据源的字段路径。
        /// </summary>
        /// <remarks>
        /// 路径为空时该行只渲染一次；路径指向序列时，该行针对序列项重复布局。
        /// </remarks>
        public string RepeatSource { get; set; }

        /// <summary>
        /// 获取或设置该行是否作为每页重复的页面表头。
        /// </summary>
        /// <remarks>
        /// 仅支持模板顶层连续排列在起始位置的表头行。
        /// </remarks>
        public bool IsHeader { get; set; }

        /// <summary>
        /// 获取或设置版本 2 模板中的页面分区名称。
        /// </summary>
        /// <remarks>
        /// 支持 <c>Body</c>、<c>DocumentHeader</c>、<c>PageHeader</c>、<c>DocumentFooter</c>、<c>PageFooter</c> 和 <c>TableHeader</c>。<c>TableHeader</c> 仅在紧随其后的重复明细发生分页时重绘。
        /// </remarks>
        public string Section { get; set; }

        /// <summary>
        /// 获取或设置控制此行是否渲染的条件。
        /// </summary>
        public BpCondition Condition { get; set; }
        /// <summary>
        /// 获取或设置行的布局高度，单位为打印点。
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// 获取或设置行的边框和填充样式。
        /// </summary>
        public BpBorder Border { get; set; }

        /// <summary>
        /// 获取或设置按顺序排列在此行中的单元格。
        /// </summary>
        public List<BpCell> Cells { get; set; }
    }
}
