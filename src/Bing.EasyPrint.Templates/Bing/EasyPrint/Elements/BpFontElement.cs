
namespace Bing.EasyPrint.Elements
{
    /// <summary>
    /// 用于输出单行或多行文本的模板元素。
    /// </summary>
    public class BpFontElement : BpElement
    {
        /// <summary>
    /// 初始化一个 <see cref="BpFontElement"/> 类型的实例。
        /// </summary>
        public BpFontElement() { Type = 1; }
        /// <summary>
        /// 获取或设置打印字体的字号值。
        /// </summary>
        public int FontSize { get; set; }

        /// <summary>
        /// 获取或设置打印字体的样式编号。
        /// </summary>
        public int FontStyle { get; set; }

        /// <summary>
        /// 获取或设置打印机使用的字体编号。
        /// </summary>
        public int FontName { get; set; }

        /// <summary>获取或设置显式语义字体选择；为空时使用旧版字体字段。</summary>
        /// <remarks>指定后不能同时设置非零 FontName、非空 FontFamily 或 DeviceFontName。</remarks>
        public PrintFont Font { get; set; }

        /// <summary>获取或设置 ESC/POS Raster 输出时使用的系统字体族名称。</summary>
        public string FontFamily { get; set; }
        /// <summary>获取或设置 TSPL/ZPL 的内置设备字体标识；留空时使用字体 0。</summary>
        public string DeviceFontName { get; set; }

        /// <summary>获取或设置顺时针旋转角度；坐标表示旋转后占位的左上角。</summary>
        public RotationAngle Rotation { get; set; }
        /// <summary>获取或设置旋转前的单行排版宽度，单位为打印点；零表示自动选择可用宽度。</summary>
        public int Width { get; set; }

        /// <summary>
        /// 获取或设置是否以粗体输出文字。
        /// </summary>
        public bool Bold { get; set; }

        /// <summary>
        /// 获取或设置是否以反白方式输出文字。
        /// </summary>
        public bool Reverse { get; set; }

        /// <summary>
        /// 获取或设置文字在可用区域内的水平对齐方式，默认值为左对齐。
        /// </summary>
        public BpTextAlignment TextAlignment { get; set; }
    }
}
