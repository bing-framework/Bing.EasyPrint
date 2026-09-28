using System.Collections.Generic;

namespace Bing.EasyPrint.Model
{
    /// <summary>
    /// 定义用于生成打印页的布局、数据绑定和输出设置。
    /// </summary>
    public class BpTemplate
    {
        /// <summary>
        /// 获取或设置模板 JSON 格式版本，默认值为 1。
        /// </summary>
        /// <remarks>
        /// 版本 2 支持页面分区、变量和可复用组件。
        /// </remarks>
        public int Version { get; set; } = 1;

        /// <summary>
        /// 获取或设置版本 2 模板中的变量名与根数据字段路径映射。
        /// </summary>
        public Dictionary<string, string> Variables { get; set; }

        /// <summary>
        /// 获取或设置版本 2 模板声明的可复用元素组件。
        /// </summary>
        public List<BpComponentDefinition> Components { get; set; }

        /// <summary>
        /// 获取或设置是否根据实际内容计算页面高度。
        /// </summary>
        /// <remarks>
        /// 启用后若 <see cref="Height" /> 为零，则使用渲染选项指定的页面高度上限。
        /// </remarks>
        public bool AutoHeight { get; set; }
        /// <summary>
        /// 获取或设置纸张类型编码。
        /// </summary>
        /// <remarks>
        /// 当前定义中 1 表示 A4；尺寸计算仍以模板的宽度和高度为准。
        /// </remarks>
        public int PaperType { get; set; }

        /// <summary>
        /// 获取或设置页面宽度，单位为打印点。
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// 获取或设置页面高度，单位为打印点。
        /// </summary>
        public int Height{ get; set; }

        /// <summary>
        /// 获取或设置页面背景颜色编码。
        /// </summary>
        /// <remarks>
        /// 1 表示白色，2 表示黑色，3 表示黄色。
        /// </remarks>
        public int Color{ get; set; }

        /// <summary>
        /// 获取或设置页面边框和填充样式。
        /// </summary>
        public BpBorder Border { get; set; }

        /// <summary>
        /// 获取或设置构成页面布局的顶层行。
        /// </summary>
        public List<BpRow> Rows { get; set; }
    }
}
