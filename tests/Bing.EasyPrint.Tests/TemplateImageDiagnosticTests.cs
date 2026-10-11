using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Templates;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace Bing.EasyPrint.Tests;

public sealed class TemplateImageDiagnosticTests
{
    [Theory]
    [InlineData(PrintProtocol.Cpcl)]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    [InlineData(PrintProtocol.EscPos)]
    public void Invalid_image_layout_reports_source_path_and_releases_working_budget(PrintProtocol protocol)
    {
        using var budget = PrintResourceScope.Begin(1024 * 1024);
        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().RenderWithLayout(
            ImageTemplate(), null, new InvalidImageResources(), LayoutOptions(protocol)));

        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.SourcePath);
        Assert.IsType<System.ArgumentException>(error.InnerException);
        Assert.Equal("logo", error.ResourceName);
        Assert.Null(error.FieldPath);
        AssertNoPixelDiagnostics(error);
        Assert.Equal(0, budget.CurrentBytes);
    }

    [Fact]
    public void Missing_image_resource_keeps_legacy_error_classification_without_pixel_metadata()
    {
        using var budget = PrintResourceScope.Begin(1024 * 1024);
        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().RenderWithLayout(
            ImageTemplate(), null, new MissingImageResources(), LayoutOptions(PrintProtocol.Zpl)));

        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.SourcePath);
        Assert.IsType<System.ArgumentException>(error.InnerException);
        Assert.Equal("logo", error.ResourceName);
        AssertNoPixelDiagnostics(error);
        Assert.Equal(0, budget.CurrentBytes);
    }

    [Theory]
    [InlineData(PrintProtocol.Cpcl)]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    [InlineData(PrintProtocol.EscPos)]
    public void Out_of_bounds_crop_reports_source_dimensions_and_requested_crop(PrintProtocol protocol)
    {
        using var budget = PrintResourceScope.Begin(1024 * 1024);
        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().RenderWithLayout(
            CroppedImageTemplate(), null, new ValidImageResources(CreatePng(4, 3)), LayoutOptions(protocol)));

        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.SourcePath);
        Assert.Equal("logo", error.ResourceName);
        Assert.Equal(4, error.SourcePixelWidth);
        Assert.Equal(3, error.SourcePixelHeight);
        Assert.Equal(3, error.CropX);
        Assert.Equal(2, error.CropY);
        Assert.Equal(2, error.CropWidth);
        Assert.Equal(2, error.CropHeight);
        Assert.Null(error.RenderedPixelWidth);
        Assert.Null(error.RenderedPixelHeight);
        Assert.Contains("4×3", error.Message);
        Assert.Equal(0, budget.CurrentBytes);
    }

    [Fact]
    public void Legacy_cpcl_render_keeps_original_image_exception_type()
    {
        Assert.Throws<System.ArgumentException>(() => new TemplateRenderer().Render(
            ImageTemplate(), null, new InvalidImageResources(),
            new TemplateRenderOptions { Encoding = Encoding.UTF8 }));
    }

    private static BpTemplate ImageTemplate() => new BpTemplate
    {
        Width = 80, Height = 40,
        Rows = new List<BpRow> { new BpRow
        {
            Cells = new List<BpCell> { new BpCell
            {
                Elements = new List<BpElement>
                {
                    new BpImageElement { Width = 8, Height = 8, Field = new BgField { Value = "logo" } }
                }
            } }
        } }
    };

    private static BpTemplate CroppedImageTemplate() => new BpTemplate
    {
        Width = 80, Height = 40,
        Rows = new List<BpRow> { new BpRow
        {
            Cells = new List<BpCell> { new BpCell
            {
                Elements = new List<BpElement>
                {
                    new BpImageElement
                    {
                        Width = 2,
                        Height = 2,
                        Field = new BgField { Value = "logo" },
                        Crop = new BpImageCrop { X = 3, Y = 2, Width = 2, Height = 2 }
                    }
                }
            } }
        } }
    };

    private static TemplateRenderOptions LayoutOptions(PrintProtocol protocol) => new TemplateRenderOptions
    {
        Protocol = protocol,
        Encoding = Encoding.UTF8,
        EscPosLayout = EscPosLayoutMode.Raster,
        FeedLines = 0,
        CutAfterPrint = false
    };

    private static byte[] CreatePng(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height);
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                image[x, y] = (x + y) % 2 == 0
                    ? new Rgba32(0, 0, 0, 255)
                    : new Rgba32(255, 255, 255, 255);
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }

    private static void AssertNoPixelDiagnostics(TemplateRenderException error)
    {
        Assert.Null(error.SourcePixelWidth);
        Assert.Null(error.SourcePixelHeight);
        Assert.Null(error.RenderedPixelWidth);
        Assert.Null(error.RenderedPixelHeight);
        Assert.Null(error.CropX);
        Assert.Null(error.CropY);
        Assert.Null(error.CropWidth);
        Assert.Null(error.CropHeight);
    }

    private sealed class InvalidImageResources : ITemplateResourceProvider
    {
        public Stream OpenRead(string resourceKey, CancellationToken cancellationToken)
            => new MemoryStream(Encoding.ASCII.GetBytes("this is not an image"), false);
    }

    private sealed class MissingImageResources : ITemplateResourceProvider
    {
        public Stream OpenRead(string resourceKey, CancellationToken cancellationToken) => null;
    }

    private sealed class ValidImageResources : ITemplateResourceProvider
    {
        private readonly byte[] bytes;

        internal ValidImageResources(byte[] bytes) => this.bytes = bytes;

        public Stream OpenRead(string resourceKey, CancellationToken cancellationToken)
            => new MemoryStream(bytes, false);
    }
}
