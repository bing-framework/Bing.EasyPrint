using System;
using Bing.EasyPrint.CPCL;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>
/// 验证 CPCL Helper 的条码类型映射和异常边界。
/// </summary>
public sealed class CpclHelperBarcodeTypeTests
{
    [Theory]
    [InlineData(BarcodeType.Code128, "128")]
    [InlineData(BarcodeType.Code39, "39")]
    [InlineData(BarcodeType.Code93, "93")]
    [InlineData(BarcodeType.Codabar, "CODABAR")]
    [InlineData(BarcodeType.Ean8, "EAN8")]
    [InlineData(BarcodeType.Ean13, "EAN13")]
    [InlineData(BarcodeType.UpcA, "UPCA")]
    [InlineData(BarcodeType.UpcE, "UPCE")]
    [InlineData(BarcodeType.I2OF5, "I2OF5")]
    [InlineData(BarcodeType.I2OF5C, "I2OF5C")]
    [InlineData(BarcodeType.I2OF5G, "I2OF5G")]
    [InlineData(BarcodeType.UccEan128, "UCCEAN128")]
    [InlineData(BarcodeType.Msi, "MSI")]
    [InlineData(BarcodeType.Postnet, "POSTNET")]
    [InlineData(BarcodeType.Fim, "FIM")]
    public void ConvertBarcodeType_preserves_supported_cpcl_markers(BarcodeType type, string expected)
    {
        Assert.Equal(expected, Helper.ConvertBarcodeType(type));
    }

    [Theory]
    [InlineData(BarcodeType.Planet)]
    [InlineData(BarcodeType.IntelligentMail)]
    [InlineData(BarcodeType.Plessey)]
    [InlineData(BarcodeType.Itf14)]
    [InlineData(BarcodeType.Ean14)]
    [InlineData(BarcodeType.Code11)]
    [InlineData(BarcodeType.ChinaPost)]
    [InlineData(BarcodeType.Telepen)]
    [InlineData(BarcodeType.Telepenn)]
    [InlineData(BarcodeType.Code49)]
    [InlineData(BarcodeType.Gs1DataBarOmnidirectional)]
    [InlineData(BarcodeType.Gs1DataBarTruncated)]
    [InlineData(BarcodeType.Gs1DataBarLimited)]
    [InlineData(BarcodeType.Gs1DataBarExpanded)]
    [InlineData(BarcodeType.Code128Auto)]
    [InlineData(BarcodeType.Gs1DataBarStacked)]
    public void ConvertBarcodeType_rejects_valid_but_unmapped_types(BarcodeType type)
    {
        Assert.Throws<NotSupportedException>(() => Helper.ConvertBarcodeType(type));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(31)]
    public void ConvertBarcodeType_rejects_values_outside_the_enum(int value)
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(() =>
            Helper.ConvertBarcodeType((BarcodeType)value));

        Assert.Equal("type", error.ParamName);
    }
}
