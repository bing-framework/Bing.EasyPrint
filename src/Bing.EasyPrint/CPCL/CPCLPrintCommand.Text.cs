namespace Bing.EasyPrint.CPCL
{
    /// <summary>
    /// CPCL 打印命令 - 文本指令
    /// </summary>
    // ReSharper disable once InconsistentNaming
    partial class CPCLPrintCommand
    {
        /// <summary>
        /// TEXT 命令
        /// </summary>
        /// <param name="font">字体名称/编号</param>
        /// <param name="size">字体的大小标识</param>
        /// <param name="x">横向起始位置</param>
        /// <param name="y">纵向起始位置</param>
        /// <param name="data">要打印的文本</param>
        /// <remarks>横向打印文本。</remarks>
        public CPCLPrintCommand Text(int font, int size, int x, int y, string data) => WriteRawLine($"T {font} {size} {x} {y} {data}");

        /// <summary>
        /// TEXT 命令
        /// </summary>
        /// <param name="font">字体名称/编号</param>
        /// <param name="size">字体的大小标识</param>
        /// <param name="x">横向起始位置</param>
        /// <param name="y">纵向起始位置</param>
        /// <param name="data">要打印的文本</param>
        /// <remarks>横向打印文本。</remarks>
        public CPCLPrintCommand Text(string font, int size, int x, int y, string data) => WriteRawLine($"T {font} {size} {x} {y} {data}");

        /// <summary>
        /// TEXT90 命令
        /// </summary>
        /// <param name="font">字体名称/编号</param>
        /// <param name="size">字体的大小标识</param>
        /// <param name="x">横向起始位置</param>
        /// <param name="y">纵向起始位置</param>
        /// <param name="data">要打印的文本</param>
        /// <remarks>逆时针旋转90度，横向打印文本。</remarks>
        public CPCLPrintCommand Text90(int font, int size, int x, int y, string data) => WriteRawLine($"T90 {font} {size} {x} {y} {data}");

        /// <summary>
        /// TEXT90 命令
        /// </summary>
        /// <param name="font">字体名称/编号</param>
        /// <param name="size">字体的大小标识</param>
        /// <param name="x">横向起始位置</param>
        /// <param name="y">纵向起始位置</param>
        /// <param name="data">要打印的文本</param>
        /// <remarks>逆时针旋转90度，横向打印文本。</remarks>
        public CPCLPrintCommand Text90(string font, int size, int x, int y, string data) => WriteRawLine($"T90 {font} {size} {x} {y} {data}");

        /// <summary>
        /// TEXT180 命令
        /// </summary>
        /// <param name="font">字体名称/编号</param>
        /// <param name="size">字体的大小标识</param>
        /// <param name="x">横向起始位置</param>
        /// <param name="y">纵向起始位置</param>
        /// <param name="data">要打印的文本</param>
        /// <remarks>逆时针旋转180度，反转打印文本。</remarks>
        public CPCLPrintCommand Text180(int font, int size, int x, int y, string data) => WriteRawLine($"T180 {font} {size} {x} {y} {data}");

        /// <summary>
        /// TEXT180 命令
        /// </summary>
        /// <param name="font">字体名称/编号</param>
        /// <param name="size">字体的大小标识</param>
        /// <param name="x">横向起始位置</param>
        /// <param name="y">纵向起始位置</param>
        /// <param name="data">要打印的文本</param>
        /// <remarks>逆时针旋转180度，反转打印文本。</remarks>
        public CPCLPrintCommand Text180(string font, int size, int x, int y, string data) => WriteRawLine($"T180 {font} {size} {x} {y} {data}");

        /// <summary>
        /// TEXT270 命令
        /// </summary>
        /// <param name="font">字体名称/编号</param>
        /// <param name="size">字体的大小标识</param>
        /// <param name="x">横向起始位置</param>
        /// <param name="y">纵向起始位置</param>
        /// <param name="data">要打印的文本</param>
        /// <remarks>逆时针旋转270度，纵向打印文本。</remarks>
        public CPCLPrintCommand Text270(int font, int size, int x, int y, string data) => WriteRawLine($"T270 {font} {size} {x} {y} {data}");

        /// <summary>
        /// TEXT270 命令
        /// </summary>
        /// <param name="font">字体名称/编号</param>
        /// <param name="size">字体的大小标识</param>
        /// <param name="x">横向起始位置</param>
        /// <param name="y">纵向起始位置</param>
        /// <param name="data">要打印的文本</param>
        /// <remarks>逆时针旋转270度，纵向打印文本。</remarks>
        public CPCLPrintCommand Text270(string font, int size, int x, int y, string data) => WriteRawLine($"T270 {font} {size} {x} {y} {data}");

        /// <summary>
        /// VTEXT 命令
        /// </summary>
        /// <param name="font">字体名称/编号</param>
        /// <param name="size">字体的大小标识</param>
        /// <param name="x">横向起始位置</param>
        /// <param name="y">纵向起始位置</param>
        /// <param name="data">要打印的文本</param>
        /// <remarks>逆时针旋转90度，纵向打印文本。</remarks>
        public CPCLPrintCommand VText(string font, int size, int x, int y, string data) => WriteRawLine($"VT {font} {size} {x} {y} {data}");

        /// <summary>
        /// FONT-GROUP 命令
        /// </summary>
        /// <param name="fg">字体组编号。最多可指定10个字体组。有效字体组范围是0至9.</param>
        /// <param name="param">字体名称/编号-字体的大小标识</param>
        /// <remarks>字体组。
        /// 使用  FG 命令，用户可以将多 10 个预缩放字体文件分至一个组。然后，用户可在 TEXT 命令中指定字体组。如果 文本命令中使用了字体组，则打印机将使用字体组中指定的大字体，这将生成所需的文本数据，并仍保留在文本 标签的可用宽度范围内。在 TEXT 命令中进行指定时，{font} 参数将指定为 FG，而 {size} 参数则指定为 {fg}。请注 意，用户还可以在 CONCAT/ENCONCAT 命令中指定 FG 命令。
        /// </remarks>
        public CPCLPrintCommand FontGroup(int fg, params (string fn, int fs)[] param)
        {
            var sb = string.Empty;
            foreach (var item in param)
                sb = $"{sb} {item.fn} {item.fs}";
            return WriteRawLine($"FG {fg}{sb}");
        }

        /// <summary>
        /// CONCAT 文本串联命令（横向）
        /// </summary>
        /// <param name="x">横向起始位置</param>
        /// <param name="y">纵向起始位置</param>
        /// <param name="param">参数格式：字体名称-字体大小-偏移量-文本</param>
        /// <remarks>使用文本串联，可以为字符串分配不同的字符样式，在同一文本行上使用统一间距进行打印。这项命令可以与可缩 放字体组合使用。请参见可缩放串联命令。</remarks>
        public CPCLPrintCommand Concat(int x, int y, params string[] param)
        {
            WriteRawLine($"CONCAT {x} {y}");
            foreach (var item in param)
                WriteRawLine($"{item}");
            WriteRawLine("ENDCONCAT");
            return this;
        }

        /// <summary>
        /// CONCAT 文本串联命令（横向）
        /// </summary>
        /// <param name="x">横向起始位置</param>
        /// <param name="y">纵向起始位置</param>
        /// <param name="param">参数格式：字体名称-字体大小-偏移量-文本</param>
        /// <remarks>使用文本串联，可以为字符串分配不同的字符样式，在同一文本行上使用统一间距进行打印。这项命令可以与可缩 放字体组合使用。请参见可缩放串联命令。</remarks>
        public CPCLPrintCommand Concat(int x, int y, params (string font, int size, int offset, string data)[] param)
        {
            WriteRawLine($"CONCAT {x} {y}");
            foreach (var item in param)
                WriteRawLine($"{item.font} {item.size} {item.offset} {item.data}");
            WriteRawLine("ENDCONCAT");
            return this;
        }

        /// <summary>
        /// VCONCAT 文本串联命令（纵向）
        /// </summary>
        /// <param name="x">横向起始位置</param>
        /// <param name="y">纵向起始位置</param>
        /// <param name="param">参数格式：字体名称-字体大小-偏移量-文本</param>
        /// <remarks>使用文本串联，可以为字符串分配不同的字符样式，在同一文本行上使用统一间距进行打印。这项命令可以与可缩 放字体组合使用。请参见可缩放串联命令。</remarks>
        public CPCLPrintCommand VConcat(int x, int y, params string[] param)
        {
            WriteRawLine($"VCONCAT {x} {y}");
            foreach (var item in param)
                WriteRawLine($"{item}");
            WriteRawLine("ENDCONCAT");
            return this;
        }

        /// <summary>
        /// VCONCAT 文本串联命令（纵向）
        /// </summary>
        /// <param name="x">横向起始位置</param>
        /// <param name="y">纵向起始位置</param>
        /// <param name="param">参数格式：字体名称-字体大小-偏移量-文本</param>
        /// <remarks>使用文本串联，可以为字符串分配不同的字符样式，在同一文本行上使用统一间距进行打印。这项命令可以与可缩 放字体组合使用。请参见可缩放串联命令。</remarks>
        public CPCLPrintCommand VConcat(int x, int y, params (string font, int size, int offset, string data)[] param)
        {
            WriteRawLine($"VCONCAT {x} {y}");
            foreach (var item in param)
                WriteRawLine($"{item.font} {item.size} {item.offset} {item.data}");
            WriteRawLine("ENDCONCAT");
            return this;
        }

        /// <summary>
        /// MULTILINE 命令
        /// </summary>
        /// <param name="height">每行文本的单位高度</param>
        /// <param name="textCmd">文本命令，TEXT、VTEXT</param>
        /// <param name="font">字体名称/编号</param>
        /// <param name="size">字体的大小标识</param>
        /// <param name="x">横向起始位置</param>
        /// <param name="y">纵向起始位置</param>
        /// <param name="data">要打印的文本</param>
        /// <remarks>使用 MULTILINE (ML)，可以以相同字体和行高打印多行文本。 </remarks>
        public CPCLPrintCommand MultiLine(int height, string textCmd, string font, int size, int x, int y,
            params string[] data)
        {
            WriteRawLine($"ML {height}");
            WriteRawLine($"{textCmd} {font} {size} {x} {y}");
            foreach (var item in data)
                WriteRawLine(item);
            WriteRawLine("ENDML");
            return this;
        }

        /// <summary>
        /// COUNT 命令
        /// </summary>
        /// <param name="count">递增值。任何整数值都不能超过20个字符。如果希望减小 TEXT/BARCODE 值，则可以在值前添加“-”符号。输出结果中将保值前导零。</param>
        /// <remarks>
        /// COUNT 命令可以用于打印多个标签，其中条码中编码的数字文本域或数字数据将针对每个标签依次递增或者递减。TEXT/BARCODE 命令字符串必须包含此数字数据，将其作为字符串的后若干字符。数字数据部分多可以包 含 20 个字符，且可以以‘-’符号作为前缀。增加或减少数字数据时不能以‘0’为增量或减量。前导零将予以保留。一个标签文件中多可使用三个 COUNT 命令。 递增/递减的数字数据包含在 TEXT 或 BARCODE 命令中，后面紧跟 COUNT 命令。 
        /// </remarks>
        public CPCLPrintCommand Count(int count) => WriteRawLine($"COUNT {count}");

        /// <summary>
        /// SETMAG 命令。设置字体放大倍数
        /// </summary>
        /// <param name="w">字体的宽度放大倍数。有效放大倍数为1到16。</param>
        /// <param name="h">字体的高度放大倍数。有效放大倍数为1到16。</param>
        /// <remarks>
        /// SETMAG 命令可将常驻字体放大指定的放大倍数。<br/>
        /// SETMAG 命令在标签打印后仍保持有效。这意味着要打印的下一标签将使用近设置的 SETMAG 值。要取消 SETMAG 值并使打印机可以 使用默认字体大小，请使用 SETMAG 命令，且放大倍数为 0,0。 
        /// </remarks>
        public CPCLPrintCommand SetMag(int w, int h) => WriteRawLine($"SETMAG {w} {h}");

        /// <summary>
        /// SCALABLE-TEXT 命令（横向）
        /// </summary>
        /// <param name="name">字体名称。缩放字体</param>
        /// <param name="width">字体宽度（点大小）</param>
        /// <param name="height">字体高度（点大小）</param>
        /// <param name="x">横向起始位置</param>
        /// <param name="y">纵向起始位置</param>
        /// <param name="data">要打印的文本</param>
        /// <remarks>使用可缩放文本，用户可以打印任何字体大小的文本。通过指定 X 和 Y 两个方向上的字体大小，可以生成宽度或 高度经过“缩放”的字符。指定的字体大小和生成的文本将以 72 点，即 1 英寸（25.4 毫米）打印出来</remarks>
        public CPCLPrintCommand ScalableText(string name, int width, int height, int x, int y, string data) => WriteRawLine($"ST {name} {width} {height} {x} {y} {data}");

        /// <summary>
        /// VSCALABLE-TEXT 命令（纵向）
        /// </summary>
        /// <param name="name">字体名称。缩放字体</param>
        /// <param name="width">字体宽度（点大小）</param>
        /// <param name="height">字体高度（点大小）</param>
        /// <param name="x">横向起始位置</param>
        /// <param name="y">纵向起始位置</param>
        /// <param name="data">要打印的文本</param>
        /// <remarks>使用可缩放文本，用户可以打印任何字体大小的文本。通过指定 X 和 Y 两个方向上的字体大小，可以生成宽度或 高度经过“缩放”的字符。指定的字体大小和生成的文本将以 72 点，即 1 英寸（25.4 毫米）打印出来</remarks>
        public CPCLPrintCommand VScalableText(string name, int width, int height, int x, int y, string data) => WriteRawLine($"VST {name} {width} {height} {x} {y} {data}");

        /// <summary>
        /// SCALE-TO-FIT 命令（横向）
        /// </summary>
        /// <param name="name">字体名称。缩放字体</param>
        /// <param name="width">窗口的单位宽度</param>
        /// <param name="height">窗口的单位高度</param>
        /// <param name="x">横向起始位置</param>
        /// <param name="y">纵向起始位置</param>
        /// <param name="data">要打印的文本</param>
        /// <remarks>SCALE-TO-FIT 命令可自动计算文本的比例，确保文本不超出窗口范围。</remarks>
        public CPCLPrintCommand ScaleToFit(string name, int width, int height, int x, int y, string data) => WriteRawLine($"STF {name} {width} {height} {x} {y} {data}");

        /// <summary>
        /// SCALE-TO-FIT 命令（纵向）
        /// </summary>
        /// <param name="name">字体名称。缩放字体</param>
        /// <param name="width">窗口的单位宽度</param>
        /// <param name="height">窗口的单位高度</param>
        /// <param name="x">横向起始位置</param>
        /// <param name="y">纵向起始位置</param>
        /// <param name="data">要打印的文本</param>
        /// <remarks>SCALE-TO-FIT 命令可自动计算文本的比例，确保文本不超出窗口范围。</remarks>
        public CPCLPrintCommand VScaleToFit(string name, int width, int height, int x, int y, string data) => WriteRawLine($"VSTF {name} {width} {height} {x} {y} {data}");

        /// <summary>
        /// SCALABLE-CONCATENATION 命令（横向）
        /// </summary>
        /// <param name="x">横向起始位置</param>
        /// <param name="y">纵向起始位置</param>
        /// <param name="param">参数：字体名称-字体宽度-字体高度-偏移量-文本</param>
        /// <remarks>使用可缩放串联，可以为字符串分配不同的字符样式，在同一文本行上使用统一间距进行打印。可缩放和位图文本 组合到 CONCAT/ENCONCAT 命令中</remarks>
        public CPCLPrintCommand ScalableConcat(int x, int y, params (string name, int width, int height, int offset, string data)[] param)
        {
            WriteRawLine($"CONCAT {x} {y}");
            foreach (var item in param)
                WriteRawLine($"ST {item.name} {item.width} {item.height} {item.offset} {item.data}");
            WriteRawLine("ENDCONCAT");
            return this;
        }

        /// <summary>
        /// SCALABLE-CONCATENATION 命令（纵向）
        /// </summary>
        /// <param name="x">横向起始位置</param>
        /// <param name="y">纵向起始位置</param>
        /// <param name="param">参数：字体名称-字体宽度-字体高度-偏移量-文本</param>
        /// <remarks>使用可缩放串联，可以为字符串分配不同的字符样式，在同一文本行上使用统一间距进行打印。可缩放和位图文本 组合到 CONCAT/ENCONCAT 命令中</remarks>
        public CPCLPrintCommand VScalableConcat(int x, int y, params (string name, int width, int height, int offset, string data)[] param)
        {
            WriteRawLine($"VCONCAT {x} {y}");
            foreach (var item in param)
                WriteRawLine($"ST {item.name} {item.width} {item.height} {item.offset} {item.data}");
            WriteRawLine("ENDCONCAT");
            return this;
        }

        /// <summary>
        /// ROTATE 命令
        /// </summary>
        /// <param name="angle">旋转角度</param>
        /// <remarks>ROTATE 命令用于以指定角度旋转所有后续可缩放文本域。旋转方向为以文本中心点为中心逆时针。在发出下一个 ROTATE 命令前，前一个旋转一直有效。默认角度为零度。</remarks>
        public CPCLPrintCommand Rotate(int angle) => WriteRawLine($"R {angle}");

        /// <summary>
        /// SETBOLD 命令
        /// </summary>
        /// <param name="value">值。介于 0 到 5 之间的偏移量。</param>
        /// <remarks>SETBOLD 命令可使文本加粗并且稍微加宽。SETBOLD 命令会采用一个操作数来设置文本变黑的程度。</remarks>
        public CPCLPrintCommand SetBold(int value) => WriteRawLine($"SETBOLD {value}");
    }
}
