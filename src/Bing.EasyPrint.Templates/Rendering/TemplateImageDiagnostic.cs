using System;
using Bing.EasyPrint.Model;

namespace Bing.EasyPrint.Templates;

/// <summary>
/// 为已知图片输入错误附加资源键和像素元数据。
/// </summary>
internal static class TemplateImageDiagnostic
{
    private static readonly object Key = new object();

    internal static void Attach(Exception error, string resourceName)
        => Attach(error, resourceName, null, null, null, null, null);

    internal static void Attach(Exception error, string resourceName, BpImageCrop crop,
        int? sourcePixelWidth, int? sourcePixelHeight, int? renderedPixelWidth, int? renderedPixelHeight)
    {
        if (error == null) return;
        var existing = GetReference(error);
        if (existing == null)
        {
            error.Data[Key] = new ResourceReference(resourceName, crop,
                sourcePixelWidth, sourcePixelHeight, renderedPixelWidth, renderedPixelHeight);
            return;
        }

        // 内层已经识别到的尺寸优先；外层只补齐仍未知的字段。
        existing.Merge(resourceName, crop, sourcePixelWidth, sourcePixelHeight,
            renderedPixelWidth, renderedPixelHeight);
    }

    internal static string GetResourceName(Exception error)
    {
        return GetReference(error)?.Name;
    }

    internal static int? GetSourcePixelWidth(Exception error) => GetReference(error)?.SourcePixelWidth;
    internal static int? GetSourcePixelHeight(Exception error) => GetReference(error)?.SourcePixelHeight;
    internal static int? GetRenderedPixelWidth(Exception error) => GetReference(error)?.RenderedPixelWidth;
    internal static int? GetRenderedPixelHeight(Exception error) => GetReference(error)?.RenderedPixelHeight;
    internal static int? GetCropX(Exception error) => GetReference(error)?.CropX;
    internal static int? GetCropY(Exception error) => GetReference(error)?.CropY;
    internal static int? GetCropWidth(Exception error) => GetReference(error)?.CropWidth;
    internal static int? GetCropHeight(Exception error) => GetReference(error)?.CropHeight;

    private static ResourceReference GetReference(Exception error)
    {
        for (var current = error; current != null; current = current.InnerException)
            if (current.Data[Key] is ResourceReference reference) return reference;
        return null;
    }

    private sealed class ResourceReference
    {
        internal string Name { get; private set; }
        internal int? SourcePixelWidth { get; private set; }
        internal int? SourcePixelHeight { get; private set; }
        internal int? RenderedPixelWidth { get; private set; }
        internal int? RenderedPixelHeight { get; private set; }
        internal int? CropX { get; private set; }
        internal int? CropY { get; private set; }
        internal int? CropWidth { get; private set; }
        internal int? CropHeight { get; private set; }

        internal ResourceReference(string name, BpImageCrop crop,
            int? sourcePixelWidth, int? sourcePixelHeight,
            int? renderedPixelWidth, int? renderedPixelHeight)
        {
            Name = name;
            Merge(name, crop, sourcePixelWidth, sourcePixelHeight, renderedPixelWidth, renderedPixelHeight);
        }

        internal void Merge(string name, BpImageCrop crop, int? sourcePixelWidth, int? sourcePixelHeight,
            int? renderedPixelWidth, int? renderedPixelHeight)
        {
            if (Name == null) Name = name;
            if (crop != null)
            {
                CropX ??= crop.X;
                CropY ??= crop.Y;
                CropWidth ??= crop.Width;
                CropHeight ??= crop.Height;
            }
            SourcePixelWidth ??= sourcePixelWidth;
            SourcePixelHeight ??= sourcePixelHeight;
            RenderedPixelWidth ??= renderedPixelWidth;
            RenderedPixelHeight ??= renderedPixelHeight;
        }
    }
}
