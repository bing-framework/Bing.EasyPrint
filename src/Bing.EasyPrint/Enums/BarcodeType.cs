// ReSharper disable once CheckNamespace
namespace Bing.EasyPrint;

/// <summary>
/// 指定一维条码使用的编码制式。
/// </summary>
public enum BarcodeType
{
    /// <summary>
    /// Code 128 条码制式。
    /// </summary>
    Code128 = 0,
    /// <summary>
    /// Code 39 条码制式。
    /// </summary>
    Code39 = 1,
    /// <summary>
    /// Code 93 条码制式。
    /// </summary>
    Code93 = 2,
    /// <summary>
    /// Codabar 条码制式。
    /// </summary>
    Codabar = 3,
    /// <summary>
    /// EAN-8 商品条码制式。
    /// </summary>
    Ean8 = 4,
    /// <summary>
    /// EAN-13 商品条码制式。
    /// </summary>
    Ean13 = 5,
    /// <summary>
    /// UPC-A 商品条码制式。
    /// </summary>
    UpcA = 6,
    /// <summary>
    /// UPC-E 商品条码制式。
    /// </summary>
    UpcE = 7,
    /// <summary>
    /// I2OF5 条码制式。
    /// </summary>
    // ReSharper disable once InconsistentNaming
    I2OF5 = 8,
    /// <summary>
    /// I2OF5C 条码制式。
    /// </summary>
    // ReSharper disable once InconsistentNaming
    I2OF5C = 9,
    /// <summary>
    /// I2OF5G 条码制式。
    /// </summary>
    // ReSharper disable once InconsistentNaming
    I2OF5G = 10,
    /// <summary>
    /// UCC/EAN-128 条码制式。
    /// </summary>
    UccEan128 = 11,
    /// <summary>
    /// MSI 条码制式。
    /// </summary>
    Msi = 12,
    /// <summary>
    /// POSTNET 邮政条码制式。
    /// </summary>
    Postnet = 13,
    /// <summary>
    /// FIM 邮政条码制式。
    /// </summary>
    Fim = 14,
    /// <summary>
    /// PLANET 邮政条码制式。
    /// </summary>
    Planet = 15,
    /// <summary>
    /// USPS Intelligent Mail（IMb）邮政条码制式。
    /// </summary>
    IntelligentMail = 16,
    /// <summary>
    /// Plessey 条码制式；当前由 TSPL 原生 PLESSEY 类型和 ZPL 原生 ^BP 支持。
    /// </summary>
    Plessey = 17,
    /// <summary>
    /// ITF-14 条码制式；当前由 TSPL 原生 ITF14 类型支持。
    /// </summary>
    Itf14 = 18,
    /// <summary>
    /// EAN-14 条码制式；当前由 TSPL 原生 EAN14 类型支持。
    /// </summary>
    Ean14 = 19,
    /// <summary>
    /// Code 11 条码制式；当前由 TSPL 原生 11 类型支持。
    /// </summary>
    Code11 = 20,
    /// <summary>
    /// China Post 邮政条码制式；TSPL 使用原生 CPOST，ESC/POS Raster 使用主机光栅编码。
    /// </summary>
    ChinaPost = 21,
    /// <summary>
    /// Telepen 条码制式；由 TSPL 原生 TELEPEN 或 ESC/POS Raster 主机编码支持。
    /// </summary>
    Telepen = 22,
    /// <summary>
    /// Telepen 数字条码制式；由 TSPL 原生 TELEPENN 或 ESC/POS Raster 主机编码支持。
    /// </summary>
    Telepenn = 23,
    /// <summary>
    /// Code 49 堆叠条码制式；当前由 TSPL 原生 CODE49 类型和 ZPL 原生 ^B4 支持。
    /// </summary>
    Code49 = 24,
    /// <summary>
    /// GS1 DataBar Omnidirectional；当前由 ESC/POS Native Function B 75、ESC/POS Raster 主机编码和 ZPL ^BR 类型 1 支持。
    /// </summary>
    Gs1DataBarOmnidirectional = 25,
    /// <summary>
    /// GS1 DataBar Truncated；当前由 ESC/POS Native Function B 76、ESC/POS Raster 主机编码和 ZPL ^BR 类型 2 支持。
    /// </summary>
    Gs1DataBarTruncated = 26,
    /// <summary>
    /// GS1 DataBar Limited；当前由 ESC/POS Native Function B 77、ESC/POS Raster 主机编码和 ZPL ^BR 类型 5 支持。
    /// </summary>
    Gs1DataBarLimited = 27,
    /// <summary>
    /// GS1 DataBar Expanded；ESC/POS Native Function B 78、ESC/POS Raster 主机编码（显式 FNC1 分隔的 Method 2 原始 GS1 数据、GTIN-14 `(01)` 加标准通用字段字符集子集及显式 `(3103)` 公斤重量 Method 3、`(3202)/(3203)` 英制重量 Method 4、`(392x)` 本地价格 Method 5、`(393x)` ISO 4217 货币+价格 Method 6、`(310x)/(320x)` 重量+虚拟日期 Method 7/8、带 `(11)/(13)/(15)/(17)` 日期 AI 的 Method 9–14）和 ZPL ^BR 类型 6 支持。
    /// </summary>
    Gs1DataBarExpanded = 28,
    /// <summary>
    /// Code 128 auto；当前由 ESC/POS Native Function B 79 支持。
    /// </summary>
    Code128Auto = 29,
    /// <summary>
    /// GS1 DataBar Stacked；当前由 ESC/POS Raster 主机编码支持。
    /// </summary>
    Gs1DataBarStacked = 30
}
