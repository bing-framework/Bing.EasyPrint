namespace Bing.EasyPrint.CPCL
{
    /// <summary>
    /// CPCL 打印命令 - 条码指令
    /// </summary>
    // ReSharper disable once InconsistentNaming
    partial class CPCLPrintCommand
    {
        /// <summary>
        /// BARCODE 命令（横向）
        /// </summary>
        /// <param name="type">条码类型</param>
        /// <param name="width">窄条的单位宽度</param>
        /// <param name="ratio">宽度与窄条的比率</param>
        /// <param name="height">条码的单位高度</param>
        /// <param name="x">横向起始位置</param>
        /// <param name="y">纵向起始位置</param>
        /// <param name="data">条码数据</param>
        /// <remarks>BARCODE 命令能够以指定的宽度和高度纵向和横向打印条码。</remarks>
        public CPCLPrintCommand Barcode(string type, int width, int ratio, int height, int x, int y, string data) => WriteRawLine($"B {type} {width} {ratio} {height} {x} {y} {data}");

        /// <summary>
        /// VBARCODE 命令（纵向）
        /// </summary>
        /// <param name="type">条码类型</param>
        /// <param name="width">窄条的单位宽度</param>
        /// <param name="ratio">宽度与窄条的比率</param>
        /// <param name="height">条码的单位高度</param>
        /// <param name="x">横向起始位置</param>
        /// <param name="y">纵向起始位置</param>
        /// <param name="data">条码数据</param>
        /// <remarks>BARCODE 命令能够以指定的宽度和高度纵向和横向打印条码。</remarks>
        public CPCLPrintCommand VBarcode(string type, int width, int ratio, int height, int x, int y, string data) => WriteRawLine($"VB {type} {width} {ratio} {height} {x} {y} {data}");

        /// <summary>
        /// BARCODE-TEXT 命令（开启）
        /// </summary>
        /// <param name="font">注释条码时要使用的字体号</param>
        /// <param name="size">注释条码时要使用的字体大小</param>
        /// <param name="offset">文本距离条码的单位偏移量</param>
        /// <remarks>
        /// BARCODE-TEXT 命令用于通过创建条码时所用的相同数据来标记条码。这项命令避免了使用单独文本命令注释 条码的必要。文本位于条码下方的中间位置。<br />
        /// 使用 BARCODE-TEXT OFF（或 BT OFF）可以禁用它。
        /// </remarks>
        public CPCLPrintCommand BarcodeText(string font, int size, int offset) => WriteRawLine($"BT {font} {size} {offset}");

        /// <summary>
        /// BARCODE-TEXT 命令（关闭）
        /// </summary>
        /// <remarks>
        /// BARCODE-TEXT 命令用于通过创建条码时所用的相同数据来标记条码。这项命令避免了使用单独文本命令注释 条码的必要。文本位于条码下方的中间位置。<br />
        /// 使用 BARCODE-TEXT OFF（或 BT OFF）可以禁用它。
        /// </remarks>
        public CPCLPrintCommand BarcodeTextOff() => WriteRawLine("BT OFF");

        /// <summary>
        /// BARCODE-RSS 命令
        /// </summary>
        /// <param name="x">横向起始位置</param>
        /// <param name="y">纵向起始位置</param>
        /// <param name="width">最窄元素的单位宽度</param>
        /// <param name="linHeight">条码线性部分的高度</param>
        /// <param name="sepHeight">分隔符的高度</param>
        /// <param name="segments">每行的段数</param>
        /// <param name="subType">RSS/复合子类型</param>
        /// <param name="dataLine">线性数据</param>
        /// <param name="data2D">2D数据</param>
        /// <remarks>缩减码型(RSS) 涵盖一系列线性符号，旨在为用户解决特定空间限制和应用需求提供相应功能。RSS 多支持 74 个数据字符的编码。 </remarks>
        public CPCLPrintCommand BarcodeRss(int x, int y, int width, int linHeight, int sepHeight, int segments, int subType, string dataLine, string data2D) => WriteRawLine($"B RSS {x} {y} {width} {linHeight} {sepHeight} {segments} {subType} {dataLine}|{data2D}");
    }
}
