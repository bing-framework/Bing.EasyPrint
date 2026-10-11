using System;
using System.Linq;
using System.Text;
using Bing.EasyPrint.CPCL;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;
using Xunit;

namespace Bing.EasyPrint.Tests;

public sealed class ProtocolQuietZoneTests
{
    [Fact]
    public void Quiet_zone_rotates_edges_clockwise()
    {
        var quiet = new PrintQuietZone(1, 2, 3, 4);

        Assert.Equal((1, 2, 3, 4), (quiet.Left, quiet.Top, quiet.Right, quiet.Bottom));
        Assert.Equal((4, 1, 2, 3), Edges(quiet.Rotate(RotationAngle.Rotate90)));
        Assert.Equal((3, 4, 1, 2), Edges(quiet.Rotate(RotationAngle.Rotate180)));
        Assert.Equal((2, 3, 4, 1), Edges(quiet.Rotate(RotationAngle.Rotate270)));
        Assert.Equal((5, 5, 5, 5), Edges(PrintQuietZone.Uniform(5)));
    }

    [Theory]
    [InlineData(PrintProtocol.Cpcl, EscPosLayoutMode.Native)]
    [InlineData(PrintProtocol.EscPos, EscPosLayoutMode.Raster)]
    [InlineData(PrintProtocol.Tspl, EscPosLayoutMode.Native)]
    [InlineData(PrintProtocol.Zpl, EscPosLayoutMode.Native)]
    public void Explicit_quiet_zone_renders_through_each_coordinate_provider(
        PrintProtocol protocol, EscPosLayoutMode layout)
    {
        var document = new PrintDocument();
        document.AddPage(480, 180)
            .Add(new PrintBarcodeElement(40, 40, BarcodeType.Code128, "1234", 24,
                new PrintQuietZone(3, 4, 5, 6), narrowWidth: 2, wideWidth: 2,
                humanReadable: false, reservedWidth: 180), "Barcode")
            .Add(new PrintQrCodeElement(240, 40, "Q", 2, QrCodeCorrectionLevel.M,
                new PrintQuietZone(3, 4, 5, 6), width: 64, height: 64), "Qr");

        var bytes = PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII, layout)
            .Render(document)[0];

        Assert.NotEmpty(bytes);
    }

    [Fact]
    public void Cpcl_quiet_zone_keeps_body_origin_and_matches_command_golden_bytes()
    {
        const int width = 480;
        const int height = 180;
        var document = new PrintDocument();
        document.AddPage(width, height)
            .Add(new PrintBarcodeElement(40, 40, BarcodeType.Code128, "1234", 24,
                new PrintQuietZone(3, 4, 5, 6), narrowWidth: 2, wideWidth: 2,
                humanReadable: false, reservedWidth: 180))
            .Add(new PrintQrCodeElement(240, 40, "Q", 2, QrCodeCorrectionLevel.M,
                new PrintQuietZone(3, 4, 5, 6), width: 64, height: 64));

        var actual = PrintProtocolProviderFactory.Create(PrintProtocol.Cpcl, Encoding.ASCII)
            .Render(document)[0];
        var expected = new CPCLPrintCommand(Encoding.ASCII)
            .SetPage(width, height)
            .DrawBarcode1D(BarcodeType.Code128, 40, 40, "1234", 2, 24, 2, RotationAngle.None)
            .DrawQrCode(240, 40, "Q", QrCodeUnitSize.Size2, QrCodeCorrectionLevel.M, RotationAngle.None)
            .InitCommand(0, width, height, 1)
            .Build()
            .GetBytes();

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(PrintProtocol.Cpcl, EscPosLayoutMode.Native)]
    [InlineData(PrintProtocol.EscPos, EscPosLayoutMode.Raster)]
    [InlineData(PrintProtocol.Tspl, EscPosLayoutMode.Native)]
    [InlineData(PrintProtocol.Zpl, EscPosLayoutMode.Native)]
    public void Quiet_zone_must_fit_all_page_edges(PrintProtocol protocol, EscPosLayoutMode layout)
    {
        foreach (var edge in new[] { "left", "right", "top", "bottom" })
        {
            var x = edge == "left" ? 1 : 40;
            var y = edge == "top" ? 1 : 40;
            var quiet = edge switch
            {
                "left" => new PrintQuietZone(2, 0, 0, 0),
                "right" => new PrintQuietZone(0, 0, 500, 0),
                "top" => new PrintQuietZone(0, 2, 0, 0),
                _ => new PrintQuietZone(0, 0, 0, 200),
            };
            var document = new PrintDocument();
            document.AddPage(480, 180).Add(new PrintBarcodeElement(x, y, BarcodeType.Code128,
                "1234", 24, quiet, narrowWidth: 2, wideWidth: 2,
                humanReadable: false, reservedWidth: 180), "Barcode");

            var issue = PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII, layout)
                .Check(document).FirstIssue;

            Assert.NotNull(issue);
            Assert.Equal("Barcode", issue.ElementPath);
            Assert.Contains("静区", issue.Message);
        }
    }

    [Theory]
    [InlineData(PrintProtocol.Cpcl, EscPosLayoutMode.Native, true)]
    [InlineData(PrintProtocol.Cpcl, EscPosLayoutMode.Native, false)]
    [InlineData(PrintProtocol.EscPos, EscPosLayoutMode.Raster, true)]
    [InlineData(PrintProtocol.EscPos, EscPosLayoutMode.Raster, false)]
    [InlineData(PrintProtocol.Tspl, EscPosLayoutMode.Native, true)]
    [InlineData(PrintProtocol.Tspl, EscPosLayoutMode.Native, false)]
    [InlineData(PrintProtocol.Zpl, EscPosLayoutMode.Native, true)]
    [InlineData(PrintProtocol.Zpl, EscPosLayoutMode.Native, false)]
    public void Quiet_zone_rejects_a_neighbor_in_either_element_order(
        PrintProtocol protocol, EscPosLayoutMode layout, bool barcodeFirst)
    {
        var barcode = new PrintBarcodeElement(120, 40, BarcodeType.Code128, "1234", 24,
            new PrintQuietZone(10, 4, 4, 4), narrowWidth: 2, wideWidth: 2,
            humanReadable: false, reservedWidth: 180);
        var neighbor = new PrintRectangleElement(110, 40, 10, 24);
        var document = new PrintDocument();
        var page = document.AddPage(480, 180);
        if (barcodeFirst)
        {
            page.Add(barcode, "Barcode");
            page.Add(neighbor, "Neighbor");
        }
        else
        {
            page.Add(neighbor, "Neighbor");
            page.Add(barcode, "Barcode");
        }

        var issue = PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII, layout)
            .Check(document).FirstIssue;

        Assert.NotNull(issue);
        Assert.Equal("Barcode", issue.ElementPath);
        Assert.Contains("静区", issue.Message);
        Assert.Contains("Neighbor", issue.Message);
    }

    [Theory]
    [InlineData(RotationAngle.Rotate90)]
    [InlineData(RotationAngle.Rotate180)]
    [InlineData(RotationAngle.Rotate270)]
    public void Escpos_raster_rotated_text_quiet_zone_uses_rotated_ink_bounds_and_element_order(
        RotationAngle rotation)
    {
        const int barcodeX = 100;
        const int barcodeY = 40;
        const int quietLeft = 3;
        const int fontSize = 24;
        const string value = "AB";
        var fonts = FixedTestFonts.Catalog();
        var family = EscPosRasterCanvas.ResolveFontFamilyName(value, null, false, fonts);
        var ink = EscPosRasterCanvas.MeasureTextInkBounds(value, fontSize, false, true, family,
            long.MaxValue, fonts);
        var quarterTurn = rotation == RotationAngle.Rotate90 || rotation == RotationAngle.Rotate270;
        var textWidth = quarterTurn ? ink.Height : ink.Width;
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster, new EscPosDeviceOptions { RasterFonts = fonts });

        foreach (var conflict in new[] { false, true })
        {
            foreach (var barcodeFirst in new[] { false, true })
            {
                // The text ends exactly at the quiet-zone edge in the accepted case,
                // then advances one dot into it in the rejected case.
                var textX = barcodeX - quietLeft - textWidth + (conflict ? 1 : 0);
                var document = new PrintDocument();
                var page = document.AddPage(480, 180);
                var text = new PrintTextElement(textX, barcodeY + 4, value, fontSize,
                    underline: true, rotation: rotation, fontFamily: family);
                var barcode = new PrintBarcodeElement(barcodeX, barcodeY, BarcodeType.Code128, "1234", 24,
                    new PrintQuietZone(left: quietLeft), narrowWidth: 2, wideWidth: 2,
                    humanReadable: false, reservedWidth: EscPosRasterCanvas.MeasureBarcodeWidth("1234", 2));
                if (barcodeFirst)
                {
                    page.Add(barcode, "Barcode");
                    page.Add(text, "Text");
                }
                else
                {
                    page.Add(text, "Text");
                    page.Add(barcode, "Barcode");
                }

                var issue = provider.Check(document).FirstIssue;
                if (conflict)
                {
                    Assert.NotNull(issue);
                    Assert.Equal("Barcode", issue.ElementPath);
                    Assert.Contains("静区", issue.Message);
                    Assert.Contains("Text", issue.Message);
                }
                else
                {
                    Assert.Null(issue);
                }
            }
        }
    }

    [Fact]
    public void Native_nonzero_quiet_zone_is_rejected_but_explicit_zero_keeps_legacy_origin()
    {
        var rejected = new PrintDocument();
        rejected.AddPage(480, 180).Add(new PrintBarcodeElement(0, 0, BarcodeType.Code128,
            "1234", 24, PrintQuietZone.Uniform(2), narrowWidth: 2, wideWidth: 2,
            humanReadable: false, reservedWidth: 180), "Barcode");
        var native = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Native);
        var issue = native.Check(rejected).FirstIssue;

        Assert.NotNull(issue);
        Assert.Equal("Barcode", issue.ElementPath);
        Assert.Contains("静区", issue.Message);

        var accepted = new PrintDocument();
        accepted.AddPage(480, 180).Add(new PrintBarcodeElement(0, 0, BarcodeType.Code128,
            "1234", 24, new PrintQuietZone(), narrowWidth: 2, wideWidth: 2,
            humanReadable: false, reservedWidth: 180), "Barcode");
        Assert.NotEmpty(native.Render(accepted)[0]);
    }

    [Fact]
    public void Explicit_quiet_zone_requires_declared_cpcl_qr_extent()
    {
        var document = new PrintDocument();
        document.AddPage(240, 180).Add(new PrintQrCodeElement(40, 40, "Q", 2,
            QrCodeCorrectionLevel.M, PrintQuietZone.Uniform(2)), "Qr");

        var issue = PrintProtocolProviderFactory.Create(PrintProtocol.Cpcl, Encoding.ASCII)
            .Check(document).FirstIssue;

        Assert.NotNull(issue);
        Assert.Equal("Qr", issue.ElementPath);
        Assert.Contains("声明", issue.Message);
    }

    [Theory]
    [InlineData(PrintProtocol.Cpcl, EscPosLayoutMode.Native)]
    [InlineData(PrintProtocol.Tspl, EscPosLayoutMode.Native)]
    [InlineData(PrintProtocol.Zpl, EscPosLayoutMode.Native)]
    public void Native_image_padding_bits_are_protected_by_a_barcode_quiet_zone(
        PrintProtocol protocol, EscPosLayoutMode layout)
    {
        var document = new PrintDocument();
        document.AddPage(480, 180)
            .Add(new PrintImageElement(1, 40, 1, 1, new byte[] { 0xFF }), "Image")
            .Add(new PrintBarcodeElement(8, 40, BarcodeType.Code128, "1234", 24,
                new PrintQuietZone(left: 1), narrowWidth: 2, wideWidth: 2,
                humanReadable: false, reservedWidth: 180), "Barcode");

        var issue = PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII, layout)
            .Check(document).FirstIssue;

        Assert.NotNull(issue);
        Assert.Equal("Barcode", issue.ElementPath);
        Assert.Contains("静区", issue.Message);
        Assert.Contains("Image", issue.Message);
    }

    [Theory]
    [InlineData(PrintProtocol.Cpcl)]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Native_box_endpoint_is_kept_out_of_a_barcode_quiet_zone(PrintProtocol protocol)
    {
        var document = new PrintDocument();
        document.AddPage(480, 180)
            .Add(new PrintRectangleElement(0, 40, 20, 24), "Box")
            .Add(new PrintBarcodeElement(21, 40, BarcodeType.Code128, "1234", 24,
                new PrintQuietZone(left: 2), narrowWidth: 2, wideWidth: 2,
                humanReadable: false, reservedWidth: 180), "Barcode");

        var issue = PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII)
            .Check(document).FirstIssue;

        Assert.NotNull(issue);
        Assert.Equal("Barcode", issue.ElementPath);
        Assert.Contains("静区", issue.Message);
        Assert.Contains("Box", issue.Message);
    }

    [Theory]
    [InlineData(PrintProtocol.Cpcl)]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Native_unknown_text_extent_is_rejected_when_a_quiet_zone_requires_occupancy(
        PrintProtocol protocol)
    {
        var document = new PrintDocument();
        document.AddPage(480, 180)
            .Add(new PrintTextElement(0, 40, "TEXT", 24), "Text")
            .Add(new PrintBarcodeElement(120, 40, BarcodeType.Code128, "1234", 24,
                new PrintQuietZone(left: 10), narrowWidth: 2, wideWidth: 2,
                humanReadable: false, reservedWidth: 180), "Barcode");

        var issue = PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII)
            .Check(document).FirstIssue;

        Assert.NotNull(issue);
        Assert.Equal("Text", issue.ElementPath);
        Assert.Contains("WIDTH", issue.Message.ToUpperInvariant());
    }

    private static (int Left, int Top, int Right, int Bottom) Edges(PrintQuietZone quiet)
        => (quiet.Left, quiet.Top, quiet.Right, quiet.Bottom);
}
