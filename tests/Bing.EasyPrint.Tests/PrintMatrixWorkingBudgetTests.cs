using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;
using Bing.EasyPrint.Templates;
using Xunit;

namespace Bing.EasyPrint.Tests;

public sealed class PrintMatrixWorkingBudgetTests
{
    [Fact]
    public void Code128_budget_rejection_happens_before_encoding_and_releases_scope()
    {
        using var scope = PrintResourceScope.Begin(0);

        var error = Assert.Throws<PrintResourceLimitException>(() =>
            EscPosRasterCanvas.MeasureBarcodeWidth("12345", 2));

        Assert.Equal("Matrix.Code128", error.ResourceName);
        Assert.Equal(0, scope.CurrentBytes);
    }

    [Theory]
    [InlineData(false, "Matrix.Code128")]
    [InlineData(true, "Matrix.Qr")]
    public void Matrix_budget_rejection_preserves_an_existing_canvas(bool qr, string resourceName)
    {
        var canvas = new EscPosRasterCanvas(128, 128);
        canvas.SetPixel(2, 2);
        var before = canvas.ToBytes();

        PrintResourceLimitException error;
        using (var scope = PrintResourceScope.Begin(0))
        {
            error = Assert.Throws<PrintResourceLimitException>(() =>
            {
                if (qr)
                    canvas.DrawQrCode(8, 8, "budget-qr", 2, QrCodeCorrectionLevel.M);
                else
                    canvas.DrawBarcode(8, 8, "12345", 24, 2);
            });

            Assert.Equal(resourceName, error.ResourceName);
            Assert.Equal(0, scope.CurrentBytes);
        }
        Assert.Equal(before, canvas.ToBytes());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Symbol_overflow_releases_matrix_reservation_for_followup_measure(bool qr)
    {
        var canvas = new EscPosRasterCanvas(8, 8);
        using var scope = PrintResourceScope.Begin(1024 * 1024);
        var before = scope.CurrentBytes;

        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            if (qr)
                canvas.DrawQrCode(0, 0, "overflow", 2, QrCodeCorrectionLevel.M);
            else
                canvas.DrawBarcode(0, 0, "12345", 8, 2);
        });

        Assert.Equal(before, scope.CurrentBytes);
        if (qr)
            Assert.True(EscPosRasterCanvas.MeasureQrCodeSize("followup", 2, QrCodeCorrectionLevel.M) > 0);
        else
            Assert.True(EscPosRasterCanvas.MeasureBarcodeWidth("12345", 2) > 0);
        Assert.Equal(before, scope.CurrentBytes);
    }

    [Theory]
    [InlineData("DataMatrix")]
    [InlineData("PDF417")]
    public void Template_matrix_budget_rejection_recovers_and_preserves_output(string format)
    {
        var template = MatrixTemplate(format);
        var renderer = new TemplateRenderer();

        var width = format == "PDF417" ? 256 : 120;
        var matrixBytes = PrintMatrixBudget.MatrixCodeBytes(width, 80, format == "PDF417");
        // 允许前置布局元数据；额度仍小于码图自身的预留，拒绝必须发生在编码前。
        var failure = Record.Exception(() => renderer.RenderWithLayout(template, null,
            options: Options(matrixBytes - 1)));

        Assert.NotNull(failure);
        var limit = FindException<PrintResourceLimitException>(failure);
        Assert.NotNull(limit);
        Assert.Equal("Template.MatrixEncoding", limit.ResourceName);

        var bounded = renderer.RenderWithLayout(template, null, options: Options(8 * 1024 * 1024));
        var unbounded = renderer.RenderWithLayout(template, null, options: Options(null));

        Assert.Equal(unbounded.Pages.Count, bounded.Pages.Count);
        for (var pageIndex = 0; pageIndex < unbounded.Pages.Count; pageIndex++)
        {
            var expected = unbounded.Pages[pageIndex];
            var actual = bounded.Pages[pageIndex];
            Assert.Equal(expected.Width, actual.Width);
            Assert.Equal(expected.Height, actual.Height);
            Assert.Equal(expected.Data, actual.Data);
            Assert.Equal(expected.Elements.Count, actual.Elements.Count);
            for (var elementIndex = 0; elementIndex < expected.Elements.Count; elementIndex++)
                AssertLayoutEqual(expected.Elements[elementIndex], actual.Elements[elementIndex]);
        }
    }

    [Fact]
    public void Pre_cancelled_qr_measure_does_not_encode_or_reserve_matrix_budget()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var scope = PrintResourceScope.Begin(1024 * 1024);

        Assert.Throws<OperationCanceledException>(() =>
            EscPosRasterCanvas.MeasureQrCodeSize("pre-cancel", 2, QrCodeCorrectionLevel.M,
                cancellation.Token));

        Assert.Equal(0, scope.CurrentBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Encoding_failure_releases_matrix_budget_for_a_same_scope_retry(bool qr)
    {
        using var scope = PrintResourceScope.Begin(1024 * 1024);
        Assert.Throws<ArgumentException>(() =>
        {
            if (qr)
                EscPosRasterCanvas.MeasureQrCodeSize(new string('a', 4000), 1, QrCodeCorrectionLevel.H);
            else
                EscPosRasterCanvas.MeasureBarcodeWidth("\u0100", 1);
        });
        Assert.Equal(0, scope.CurrentBytes);
        if (qr)
            Assert.True(EscPosRasterCanvas.MeasureQrCodeSize("OK", 1, QrCodeCorrectionLevel.M) > 0);
        else
            Assert.True(EscPosRasterCanvas.MeasureBarcodeWidth("OK", 1) > 0);
        Assert.Equal(0, scope.CurrentBytes);
    }

    private static TemplateRenderOptions Options(long? maxWorkingBytes)
    {
        return new TemplateRenderOptions
        {
            Encoding = Encoding.UTF8,
            MaxWorkingBytes = maxWorkingBytes
        };
    }

    private static BpTemplate MatrixTemplate(string format)
    {
        var width = format == "PDF417" ? 256 : 120;
        return new BpTemplate
        {
            Version = 2,
            Width = 300,
            Height = 150,
            Rows = new List<BpRow>
            {
                new BpRow
                {
                    Section = "Body",
                    Cells = new List<BpCell>
                    {
                        new BpCell
                        {
                            Elements = new List<BpElement>
                            {
                                new BpMatrixCodeElement
                                {
                                    Format = format,
                                    Width = width,
                                    Height = 80,
                                    Field = new BgField { Value = "ORDER-12345" }
                                }
                            }
                        }
                    }
                }
            }
        };
    }

    private static TException FindException<TException>(Exception error)
        where TException : Exception
    {
        for (var current = error; current != null; current = current.InnerException)
            if (current is TException match) return match;
        return null;
    }

    private static void AssertLayoutEqual(TemplateLayoutElement expected, TemplateLayoutElement actual)
    {
        Assert.Equal(expected.SourcePath, actual.SourcePath);
        Assert.Equal(expected.DefinitionPath, actual.DefinitionPath);
        Assert.Equal(expected.ComponentInstancePath, actual.ComponentInstancePath);
        Assert.Equal(expected.FragmentIndex, actual.FragmentIndex);
        Assert.Equal(expected.Kind, actual.Kind);
        Assert.Equal(expected.Type, actual.Type);
        Assert.Equal(expected.InstanceIndices.ToArray(), actual.InstanceIndices.ToArray());
        Assert.Equal(expected.X, actual.X);
        Assert.Equal(expected.Y, actual.Y);
        Assert.Equal(expected.Width, actual.Width);
        Assert.Equal(expected.Height, actual.Height);
        Assert.Equal(expected.Text, actual.Text);
        Assert.Equal(expected.ResourceKey, actual.ResourceKey);
        Assert.Equal(expected.Rotation, actual.Rotation);
    }
}
