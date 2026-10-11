using System.Collections.Generic;
using System.Linq;
using System.Text;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Templates;
using Xunit;
using ZXing;
using ZXing.Common;
using ZXing.QrCode.Internal;

namespace Bing.EasyPrint.Tests;

public sealed class EscPosRasterQrMetricsTests
{
    [Fact]
    public void Chinese_qr_layout_uses_the_raster_encoders_actual_utf8_size()
    {
        var value = new string('中', 20);
        const int moduleSize = 2;
        var template = new BpTemplate
        {
            Width = 200,
            Height = 200,
            Rows = new List<BpRow>
            {
                new BpRow { Cells = new List<BpCell>
                {
                    new BpCell { Elements = new List<BpElement>
                    {
                        new BpQrCodeElement { UnitWidth = moduleSize, Field = new BgField { Value = value } }
                    } }
                } }
            }
        };
        var result = new TemplateRenderer().RenderWithLayout(template, null, options: new TemplateRenderOptions
        {
            Protocol = PrintProtocol.EscPos,
            EscPosLayout = EscPosLayoutMode.Raster,
            Encoding = Encoding.ASCII
        });

        var hints = new Dictionary<EncodeHintType, object>
        {
            [EncodeHintType.ERROR_CORRECTION] = ErrorCorrectionLevel.M,
            [EncodeHintType.CHARACTER_SET] = "UTF-8",
            [EncodeHintType.MARGIN] = 0
        };
        var symbol = new MultiFormatWriter().encode(value, BarcodeFormat.QR_CODE, 1, 1, hints);
        var item = result.Pages.Single().Elements.Single(element => element.Type == 3);
        Assert.Equal((symbol.Width + 8) * moduleSize, item.Width);
        Assert.Equal(item.Width, item.Height);
    }
}
