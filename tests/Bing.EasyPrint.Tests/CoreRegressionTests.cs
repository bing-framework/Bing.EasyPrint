using System;
using System.IO;
using System.Text;
using Bing.EasyPrint.CPCL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace Bing.EasyPrint.Tests;

public class CoreRegressionTests
{
    [Fact]
    public void Build_IsIdempotent()
    {
        var command = new CPCLPrintCommand(Encoding.ASCII)
            .SetPage(200, 300)
            .DrawText(10, 20, "sample", 24, 0, false, false, false);

        var first = command.Build().GetBytes();
        var second = command.Build().GetBytes();

        Assert.Equal(first, second);
    }

    [Fact]
    public void FailedBuild_PreservesLastSuccessAndCanRetry()
    {
        var command = new CPCLPrintCommand(Encoding.ASCII)
            .SetPage(200, 300)
            .DrawText(10, 20, "sample", 24, 0, false, false, false);
        var successfulWriter = command.Build();
        var successfulBytes = successfulWriter.GetBytes();
        var failure = new ThrowingMetadata();
        command.Content.Add(failure);

        Assert.Throws<InvalidOperationException>(() => command.Build());
        Assert.Same(successfulWriter, command.Writer);
        Assert.Equal(successfulBytes, command.Writer.GetBytes());

        command.Content.Content.Remove(failure);
        Assert.Equal(successfulBytes, command.Build().GetBytes());
    }

    [Fact]
    public void AutomaticHeader_SupportsAndRefreshesMultiDigitOffsetAndQuantity()
    {
        var command = new CPCLPrintCommand(Encoding.ASCII)
            .SetPage(200, 300)
            .SetOffset(10);

        var first = Encoding.ASCII.GetString(command.Build().GetBytes());
        Assert.Contains("! 10 200 200 300 1", first);

        command.SetOffset(12).SetQty(3);
        var second = Encoding.ASCII.GetString(command.Build().GetBytes());
        Assert.Contains("! 12 200 200 300 3", second);
    }

    [Fact]
    public void DrawImage_DoesNotMutateInputImageAndHandlesPaddedWidth()
    {
        using var image = new Image<Rgba32>(9, 1, new Rgba32(12, 34, 56, 255));
        var originalPixel = image[0, 0];
        var command = new CPCLPrintCommand(Encoding.ASCII)
            .SetPage(200, 100)
            .DrawImage(0, 0, image);

        Assert.Equal(originalPixel, image[0, 0]);
        var output = Encoding.ASCII.GetString(command.Build().GetBytes());

        Assert.Equal(originalPixel, image[0, 0]);
        Assert.Contains("EG 2 1", output);
    }

    [Fact]
    public void DrawImage_StreamRemainsOpenAndPositionIsRestored()
    {
        var png = CreatePng(2, 2);
        using var stream = new MemoryStream(png);
        stream.Position = 0;
        var command = new CPCLPrintCommand(Encoding.ASCII).SetPage(200, 100);

        command.DrawImage(0, 0, stream);

        Assert.True(stream.CanRead);
        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public void DrawImage_EnforcesEncodedSizeAndPixelLimits()
    {
        var png = CreatePng(9, 1);

        Assert.Throws<ArgumentException>(() => new CPCLPrintCommand(Encoding.ASCII)
            .DrawImage(0, 0, png, new ImageProcessingOptions { MaxEncodedBytes = png.Length - 1 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CPCLPrintCommand(Encoding.ASCII)
            .DrawImage(0, 0, png, new ImageProcessingOptions { MaxPixels = 8 }));
    }

    [Fact]
    public void DrawDashLine_UsesStartAndLengthAsExclusiveBoundary()
    {
        var command = new CPCLPrintCommand(Encoding.ASCII)
            .SetPage(200, 100)
            .DrawDashLine(10, 20, 31);

        var output = Encoding.ASCII.GetString(command.Build().GetBytes());

        Assert.Contains("T 24 0 10 10 -", output);
        Assert.Contains("T 24 0 26 10 -", output);
        Assert.DoesNotContain("T 24 0 42 10 -", output);
    }

    private static byte[] CreatePng(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height, new Rgba32(12, 34, 56, 255));
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }

    [Theory]
    [InlineData(1, true, "80")]
    [InlineData(2, true, "C0")]
    [InlineData(9, true, "FF80")]
    [InlineData(9, false, "0000")]
    public void ImagePacking_UsesBlackBitsAndWhitePadding(int width, bool black, string expected)
    {
        var channel = (byte)(black ? 0 : 255);
        using var image = new Image<Rgba32>(width, 1, new Rgba32(channel, channel, channel));
        var output = new CPCLPrintCommand(Encoding.ASCII).SetPage(200, 100).DrawImage(0, 0, image).Build().ToString();
        Assert.Contains($"EG {(width + 7) / 8} 1 0 0 {expected}\r\n", output);
    }

    [Fact]
    public void Resetting_page_does_not_clear_previous_successful_result()
    {
        var command = new CPCLPrintCommand(Encoding.ASCII).SetPage(100, 100).Text(24, 0, 0, 0, "old");
        var result = command.Build(); var bytes = result.GetBytes();
        command.SetPage(200, 200).Text(24, 0, 0, 0, "new").Build();
        Assert.Equal(bytes, result.GetBytes());
    }

    private sealed class ThrowingMetadata : MetadataBase
    {
        public override MetadataType MetadataType => MetadataType.Component;

        public override void Build(CPCLPrintCommand command) => throw new InvalidOperationException("Expected test failure.");
    }
}
