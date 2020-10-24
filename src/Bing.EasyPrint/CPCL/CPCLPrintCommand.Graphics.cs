namespace Bing.EasyPrint.CPCL
{
    /// <summary>
    /// CPCL 打印命令 - 图形指令
    /// </summary>
    // ReSharper disable once InconsistentNaming
    partial class CPCLPrintCommand
    {
        /// <summary>
        /// BOX 命令
        /// </summary>
        /// <param name="x0">左上角的 X 坐标</param>
        /// <param name="y0">左上角的 Y 坐标</param>
        /// <param name="x1">右下角的 X 坐标</param>
        /// <param name="y1">右下角的 Y 坐标</param>
        /// <param name="width">形成矩形框的线条的单位宽度。</param>
        /// <remarks>用户可以使用 BOX 命令生成具有指定线条宽度的矩形。 </remarks>
        public CPCLPrintCommand Box(int x0, int y0, int x1, int y1, int width) => WriteRawLine($"BOX {x0} {y0} {x1} {y1} {width}");

        /// <summary>
        /// LINE 命令
        /// </summary>
        /// <param name="x0">左上角的 X 坐标</param>
        /// <param name="y0">左上角的 Y 坐标</param>
        /// <param name="x1">水平轴的右上角、垂直轴的左下角</param>
        /// <param name="y1">水平轴的右上角、垂直轴的左下角</param>
        /// <param name="width">线条的单位宽度</param>
        /// <remarks>使用 LINE 命令可以绘制任何长度、宽度和角度方向的线条。</remarks>
        public CPCLPrintCommand Line(int x0, int y0, int x1, int y1, int width) => WriteRawLine($"L {x0} {y0} {x1} {y1} {width}");

        /// <summary>
        /// INVERSE-LINE 命令
        /// </summary>
        /// <param name="x0">左上角的 X 坐标</param>
        /// <param name="y0">左上角的 Y 坐标</param>
        /// <param name="x1">水平轴的右上角、垂直轴的左下角</param>
        /// <param name="y1">水平轴的右上角、垂直轴的左下角</param>
        /// <param name="width">反转线的单位宽度</param>
        /// <remarks>
        /// INVERSE-LINE 命令的语法与 LINE 命令相同。
        /// 位于 INVERSE-LINE 命令所定义区域内的以前创建的对象的黑色区域将重绘为白色，白色区域将重绘为黑色。<br />
        /// 这些对象可以包括文本、条码和/或图形（包括下载的.pcx 文件）。<br />
        /// INVERSE-LINE 对在其之后创建的对象不起作用，即使这些对象位于该命令的覆盖区域内也是如此。<br />
        /// 在示例 INVERSE2.LBL 中，在 INVERSE-LINE 命令之后创建的文本字段部分仍然为黑色，因此不可见，即使被放置在 INVERSE-LINE 区域内也是如此。 
        /// </remarks>
        public CPCLPrintCommand InverseLine(int x0, int y0, int x1, int y1, int width) => WriteRawLine($"IL {x0} {y0} {x1} {y1} {width}");

        /// <summary>
        /// PATTERN 命令
        /// </summary>
        /// <param name="number">填充模式。<br/>
        /// 100 - 填充（实心黑色/默认模式）<br/>
        /// 101 - 水平线<br/>
        /// 102 - 垂直线<br/>
        /// 103 - 向右上升的对角线<br/>
        /// 104 - 向左上升的对角线<br/>
        /// 105 - 正方形图案<br/>
        /// 106 - 剖面线图案<br/>
        /// </param>
        /// <returns></returns>
        public CPCLPrintCommand Pattern(int number) => WriteRawLine($"PATTERN {number}");

        /// <summary>
        /// 图形命令 - EXPANDED-GRAPHICS（横向打印扩展图形）
        /// </summary>
        /// <param name="width">图像的宽度（以字节为单位）</param>
        /// <param name="height">图形的高度（以点位单位）</param>
        /// <param name="x">横向起始位置</param>
        /// <param name="y">纵向起始位置</param>
        /// <param name="data">图形数据</param>
        /// <remarks>
        /// 可以使用图形命令打印位映射图形。扩展图形数据使用 ASCII 十六进制字符来表示（参见示例）。通过对十六进制 数据的等效二进制字符使用 COMPRESSED-GRAPHICS 命令，可以将数据大小减半。如果使用 CG，对于每 8 位图 形数据，将会发送一个 8 位字符。如果使用 EG，将使用两个字符（16 位）来传输 8 位图形数据，因此 EG 的效率会 减半。   但是由于该数据是字符数据，因此比二进制数据更容易处理和传输。 
        /// </remarks>
        public CPCLPrintCommand ExpandedGraphics(int width, int height, int x, int y, string data) => WriteRawLine($"EG {width} {height} {x} {y} {data}");

        /// <summary>
        /// 图形命令 - VEXPANDED-GRAPHICS（纵向打印扩展图形）
        /// </summary>
        /// <param name="width">图像的宽度（以字节为单位）</param>
        /// <param name="height">图形的高度（以点位单位）</param>
        /// <param name="x">横向起始位置</param>
        /// <param name="y">纵向起始位置</param>
        /// <param name="data">图形数据</param>
        /// <remarks>
        /// 可以使用图形命令打印位映射图形。扩展图形数据使用 ASCII 十六进制字符来表示（参见示例）。通过对十六进制 数据的等效二进制字符使用 COMPRESSED-GRAPHICS 命令，可以将数据大小减半。如果使用 CG，对于每 8 位图 形数据，将会发送一个 8 位字符。如果使用 EG，将使用两个字符（16 位）来传输 8 位图形数据，因此 EG 的效率会 减半。   但是由于该数据是字符数据，因此比二进制数据更容易处理和传输。 
        /// </remarks>
        public CPCLPrintCommand VExpandedGraphics(int width, int height, int x, int y, string data) => WriteRawLine($"VEG {width} {height} {x} {y} {data}");

        /// <summary>
        /// 图形命令 - COMPRESSED-GRAPHICS（横向打印压缩图形）
        /// </summary>
        /// <param name="width">图像的宽度（以字节为单位）</param>
        /// <param name="height">图形的高度（以点位单位）</param>
        /// <param name="x">横向起始位置</param>
        /// <param name="y">纵向起始位置</param>
        /// <param name="data">图形数据</param>
        /// <remarks>
        /// 可以使用图形命令打印位映射图形。扩展图形数据使用 ASCII 十六进制字符来表示（参见示例）。通过对十六进制 数据的等效二进制字符使用 COMPRESSED-GRAPHICS 命令，可以将数据大小减半。如果使用 CG，对于每 8 位图 形数据，将会发送一个 8 位字符。如果使用 EG，将使用两个字符（16 位）来传输 8 位图形数据，因此 EG 的效率会 减半。   但是由于该数据是字符数据，因此比二进制数据更容易处理和传输。 
        /// </remarks>
        public CPCLPrintCommand CompressedGraphics(int width, int height, int x, int y, string data) => WriteRawLine($"CG {width} {height} {x} {y} {data}");

        /// <summary>
        /// 图形命令 - VCOMPRESSED-GRAPHICS（横向打印压缩图形）
        /// </summary>
        /// <param name="width">图像的宽度（以字节为单位）</param>
        /// <param name="height">图形的高度（以点位单位）</param>
        /// <param name="x">横向起始位置</param>
        /// <param name="y">纵向起始位置</param>
        /// <param name="data">图形数据</param>
        /// <remarks>
        /// 可以使用图形命令打印位映射图形。扩展图形数据使用 ASCII 十六进制字符来表示（参见示例）。通过对十六进制 数据的等效二进制字符使用 COMPRESSED-GRAPHICS 命令，可以将数据大小减半。如果使用 CG，对于每 8 位图 形数据，将会发送一个 8 位字符。如果使用 EG，将使用两个字符（16 位）来传输 8 位图形数据，因此 EG 的效率会 减半。   但是由于该数据是字符数据，因此比二进制数据更容易处理和传输。 
        /// </remarks>
        public CPCLPrintCommand VCompressedGraphics(int width, int height, int x, int y, string data) => WriteRawLine($"VCG {width} {height} {x} {y} {data}");
    }
}
