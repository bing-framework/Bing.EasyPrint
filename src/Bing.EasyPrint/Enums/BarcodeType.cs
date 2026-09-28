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
    Fim = 14
}
