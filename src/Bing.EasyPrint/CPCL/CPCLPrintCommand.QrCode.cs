namespace Bing.EasyPrint.CPCL
{
    /// <summary>
    /// CPCL 打印命令 - 二维条码指令
    /// </summary>
    // ReSharper disable once InconsistentNaming
    partial class CPCLPrintCommand
    {
        /// <summary>
        /// 二维条码命令 PDF417（横向）
        /// </summary>
        /// <param name="x">横向起始位置</param>
        /// <param name="y">纵向起始位置</param>
        /// <param name="width">窄元素的单位宽度。范围介于 1 至 32 之间，默认值为 2。</param>
        /// <param name="height">窄元素的单位高度。范围介于 1 至 32 之间，默认值为 6。</param>
        /// <param name="col">要使用的列数。数据列不包括起始/终止字符和左/右指示符。范围介于 1 至 30 之间，默认值为 3</param>
        /// <param name="safeLevel">安全级别，指示要检测和/或纠正的大错误量。范围介于 0 至 8 之间，默认值为 1。</param>
        /// <param name="data">条码数据</param>
        /// <remarks>
        /// PDF417 条码是一种二维条码，这种条码可以在狭小的空间里包含数量巨大的数据。
        /// 仔细观察 PDF417 条码，您会 发现其实它是由较小的条码堆叠而成。
        /// 堆叠的数量和高度由用户控制。这类条码可以包含整个 ASCII 255 字符集，并能使用不同的编码方案和不同的纠错安全级别。大数据编码量为 2725 个字符
        /// </remarks>
        public CPCLPrintCommand Pdf417(int x, int y, int width, int height, int col, int safeLevel, params string[] data)
        {
            WriteRawLine($"B PDF-417 {x} {y} XD {width} YD {height} C {col} S {safeLevel}");
            foreach (var item in data)
                WriteRawLine(item);
            WriteRawLine("ENDPDF");
            return this;
        }

        /// <summary>
        /// 二维条码命令 PDF417（纵向）
        /// </summary>
        /// <param name="x">横向起始位置</param>
        /// <param name="y">纵向起始位置</param>
        /// <param name="width">窄元素的单位宽度。范围介于 1 至 32 之间，默认值为 2。</param>
        /// <param name="height">窄元素的单位高度。范围介于 1 至 32 之间，默认值为 6。</param>
        /// <param name="col">要使用的列数。数据列不包括起始/终止字符和左/右指示符。范围介于 1 至 30 之间，默认值为 3</param>
        /// <param name="safeLevel">安全级别，指示要检测和/或纠正的大错误量。范围介于 0 至 8 之间，默认值为 1。</param>
        /// <param name="data">条码数据</param>
        /// <remarks>
        /// PDF417 条码是一种二维条码，这种条码可以在狭小的空间里包含数量巨大的数据。
        /// 仔细观察 PDF417 条码，您会 发现其实它是由较小的条码堆叠而成。
        /// 堆叠的数量和高度由用户控制。这类条码可以包含整个 ASCII 255 字符集，并能使用不同的编码方案和不同的纠错安全级别。大数据编码量为 2725 个字符
        /// </remarks>
        public CPCLPrintCommand VPdf417(int x, int y, int width, int height, int col, int safeLevel, params string[] data)
        {
            WriteRawLine($"VB PDF-417 {x} {y} XD {width} YD {height} C {col} S {safeLevel}");
            foreach (var item in data)
                WriteRawLine(item);
            WriteRawLine("ENDPDF");
            return this;
        }

        /// <summary>
        /// 二维条码命令 MAXICODE
        /// </summary>
        /// <param name="x">横向起始位置</param>
        /// <param name="y">纵向起始位置</param>
        /// <param name="param">参数：标签-数据</param>
        /// <remarks>Maxicode 条码现在可以处理 UPS 定义的所有符号以及标准代码支持的基本字段。Maxicode 支持所有标准的可 打印字符，并能够自动将次级消息中的所有小写字母转换为大写字母。本修订版手册仅介绍了 Mode 2 条码。</remarks>
        public CPCLPrintCommand Maxicode(int x, int y, params (string tag, string data)[] param)
        {
            WriteRawLine($"B MAXICODE {x} {y}");
            foreach (var item in param)
                WriteRawLine($"{item.tag} {item.data}");
            WriteRawLine("ENDMAXICODE");
            return this;
        }

        /// <summary>
        /// 二维条码命令 QRCode（横向）
        /// </summary>
        /// <param name="x">横向起始位置</param>
        /// <param name="y">纵向起始位置</param>
        /// <param name="model">QR Code规范编号。选项是 1 或 2。QR Code Model 1 是原始规范，而 QR Code Model 2 则是该符号 的经过增强后的形式。Model 2 提供了附加功能，而且可以自动与 Model 1 进行区分。Model 2 为推荐规范，是 默认值。</param>
        /// <param name="widthWithHeight">模块的单位宽度/单位高度。范围是 1 至 32。默认值为 6。</param>
        /// <param name="data">提供生成 QR Code 所需的信息。需要录入二维自定义信息</param>
        // ReSharper disable once InconsistentNaming
        public CPCLPrintCommand QRCode(int x, int y, int model, int widthWithHeight, params string[] data)
        {
            WriteRawLine($"B QR {x} {y} M {model} U {widthWithHeight}");
            foreach (var item in data)
                WriteRawLine(item);
            WriteRawLine("ENDQR");
            return this;
        }

        /// <summary>
        /// 二维条码命令 QRCode（纵向）
        /// </summary>
        /// <param name="x">横向起始位置</param>
        /// <param name="y">纵向起始位置</param>
        /// <param name="model">QR Code规范编号。选项是 1 或 2。QR Code Model 1 是原始规范，而 QR Code Model 2 则是该符号 的经过增强后的形式。Model 2 提供了附加功能，而且可以自动与 Model 1 进行区分。Model 2 为推荐规范，是 默认值。</param>
        /// <param name="widthWithHeight">模块的单位宽度/单位高度。范围是 1 至 32。默认值为 6。</param>
        /// <param name="data">提供生成 QR Code 所需的信息。需要录入二维自定义信息</param>
        // ReSharper disable once InconsistentNaming
        public CPCLPrintCommand VQRCode(int x, int y, int model, int widthWithHeight, params string[] data)
        {
            WriteRawLine($"VB QR {x} {y} M {model} U {widthWithHeight}");
            foreach (var item in data)
                WriteRawLine(item);
            WriteRawLine("ENDQR");
            return this;
        }

        /// <summary>
        /// 二维条码命令 QRCode（横向）
        /// </summary>
        /// <param name="x">横向起始位置</param>
        /// <param name="y">纵向起始位置</param>
        /// <param name="model">QR Code规范编号。选项是 1 或 2。QR Code Model 1 是原始规范，而 QR Code Model 2 则是该符号 的经过增强后的形式。Model 2 提供了附加功能，而且可以自动与 Model 1 进行区分。Model 2 为推荐规范，是 默认值。</param>
        /// <param name="widthWithHeight">模块的单位宽度/单位高度。范围是 1 至 32。默认值为 6。</param>
        /// <param name="errorCorrectionLevel">纠错级别。如：H、Q、M、L</param>
        /// <param name="maskNo">掩码号。可能会省略，也可能具有一个值（介于 0 至 8 之间）</param>
        /// <param name="data">二维码信息</param>
        // ReSharper disable once InconsistentNaming
        public CPCLPrintCommand QRCode(int x, int y, int model, int widthWithHeight, char errorCorrectionLevel, int? maskNo, string data)
        {
            WriteRawLine($"B QR {x} {y} M {model} U {widthWithHeight}");
            WriteRawLine($"{errorCorrectionLevel}{(maskNo == null ? string.Empty : maskNo.ToString())}A,{data}");
            WriteRawLine("ENDQR");
            return this;
        }

        /// <summary>
        /// 二维条码命令 QRCode（纵向）
        /// </summary>
        /// <param name="x">横向起始位置</param>
        /// <param name="y">纵向起始位置</param>
        /// <param name="model">QR Code规范编号。选项是 1 或 2。QR Code Model 1 是原始规范，而 QR Code Model 2 则是该符号 的经过增强后的形式。Model 2 提供了附加功能，而且可以自动与 Model 1 进行区分。Model 2 为推荐规范，是 默认值。</param>
        /// <param name="widthWithHeight">模块的单位宽度/单位高度。范围是 1 至 32。默认值为 6。</param>
        /// <param name="errorCorrectionLevel">纠错级别。如：H、Q、M、L</param>
        /// <param name="maskNo">掩码号。可能会省略，也可能具有一个值（介于 0 至 8 之间）</param>
        /// <param name="data">二维码信息</param>
        // ReSharper disable once InconsistentNaming
        public CPCLPrintCommand VQRCode(int x, int y, int model, int widthWithHeight, char errorCorrectionLevel, int? maskNo, string data)
        {
            WriteRawLine($"VB QR {x} {y} M {model} U {widthWithHeight}");
            WriteRawLine($"{errorCorrectionLevel}{(maskNo == null ? string.Empty : maskNo.ToString())}A,{data}");
            WriteRawLine("ENDQR");
            return this;
        }

        /// <summary>
        /// 二维条码命令 QRCode（横向）手动
        /// </summary>
        /// <param name="x">横向起始位置</param>
        /// <param name="y">纵向起始位置</param>
        /// <param name="model">QR Code规范编号。选项是 1 或 2。QR Code Model 1 是原始规范，而 QR Code Model 2 则是该符号 的经过增强后的形式。Model 2 提供了附加功能，而且可以自动与 Model 1 进行区分。Model 2 为推荐规范，是 默认值。</param>
        /// <param name="widthWithHeight">模块的单位宽度/单位高度。范围是 1 至 32。默认值为 6。</param>
        /// <param name="errorCorrectionLevel">纠错级别。如：H、Q、M、L</param>
        /// <param name="maskNo">掩码号。可能会省略，也可能具有一个值（介于 0 至 8 之间）</param>
        /// <param name="param">参数：字符模式符(N、A、Bxxxx、K)）-数据</param>
        // ReSharper disable once InconsistentNaming
        public CPCLPrintCommand QRCodeManual(int x, int y, int model, int widthWithHeight, char errorCorrectionLevel, int? maskNo, params (string charMode, string data)[] param)
        {
            WriteRawLine($"B QR {x} {y} M {model} U {widthWithHeight}");
            WriteRaw($"{errorCorrectionLevel}{(maskNo == null ? string.Empty : maskNo.ToString())}M");
            foreach (var item in param)
                WriteRaw($",{item.charMode}{item.data}");
            WriteRawLine("");
            WriteRawLine("ENDQR");
            return this;
        }

        /// <summary>
        /// 二维条码命令 QRCode（纵向）手动
        /// </summary>
        /// <param name="x">横向起始位置</param>
        /// <param name="y">纵向起始位置</param>
        /// <param name="model">QR Code规范编号。选项是 1 或 2。QR Code Model 1 是原始规范，而 QR Code Model 2 则是该符号 的经过增强后的形式。Model 2 提供了附加功能，而且可以自动与 Model 1 进行区分。Model 2 为推荐规范，是 默认值。</param>
        /// <param name="widthWithHeight">模块的单位宽度/单位高度。范围是 1 至 32。默认值为 6。</param>
        /// <param name="errorCorrectionLevel">纠错级别。如：H、Q、M、L</param>
        /// <param name="maskNo">掩码号。可能会省略，也可能具有一个值（介于 0 至 8 之间）</param>
        /// <param name="param">参数：字符模式符(N、A、Bxxxx、K)）-数据</param>
        // ReSharper disable once InconsistentNaming
        public CPCLPrintCommand VQRCodeManual(int x, int y, int model, int widthWithHeight, char errorCorrectionLevel, int? maskNo, params (string charMode, string data)[] param)
        {
            WriteRawLine($"VB QR {x} {y} M {model} U {widthWithHeight}");
            WriteRaw($"{errorCorrectionLevel}{(maskNo == null ? string.Empty : maskNo.ToString())}M");
            foreach (var item in param)
                WriteRaw($",{item.charMode}{item.data}");
            WriteRawLine("");
            WriteRawLine("ENDQR");
            return this;
        }

        /// <summary>
        /// 二维条码命令 Aztec（横向）
        /// </summary>
        /// <param name="x">横向起始位置</param>
        /// <param name="y">纵向起始位置</param>
        /// <param name="width">最窄元素的单位宽度（以点位单位）。默认值为 6。</param>
        /// <param name="errorCorrectionPercentage">纠错参数（0-99）。默认值为 0（默认纠错百分比）。</param>
        /// <param name="data">条码数据</param>
        /// <returns></returns>
        public CPCLPrintCommand Aztec(int x, int y, int width, int errorCorrectionPercentage, params string[] data)
        {
            WriteRawLine($"B AZTEC {x} {y} XD {width} EC {errorCorrectionPercentage}");
            foreach (var item in data)
                WriteRawLine(item);
            WriteRawLine("ENDAZTEC");
            return this;
        }

        /// <summary>
        /// 二维条码命令 Aztec（纵向）
        /// </summary>
        /// <param name="x">横向起始位置</param>
        /// <param name="y">纵向起始位置</param>
        /// <param name="width">最窄元素的单位宽度（以点位单位）。默认值为 6。</param>
        /// <param name="errorCorrectionPercentage">纠错参数（0-99）。默认值为 0（默认纠错百分比）。</param>
        /// <param name="data">条码数据</param>
        /// <returns></returns>
        public CPCLPrintCommand VAztec(int x, int y, int width, int errorCorrectionPercentage, params string[] data)
        {
            WriteRawLine($"VB AZTEC {x} {y} XD {width} EC {errorCorrectionPercentage}");
            foreach (var item in data)
                WriteRawLine(item);
            WriteRawLine("ENDAZTEC");
            return this;
        }
    }
}
