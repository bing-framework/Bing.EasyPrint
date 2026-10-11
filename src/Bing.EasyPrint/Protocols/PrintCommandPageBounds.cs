using System;
using Bing.EasyPrint.Printing;

namespace Bing.EasyPrint.Protocols;

internal static class PrintCommandPageBounds
{
    internal static void ValidateExtent(double pageWidth, double pageHeight, long x, long y,
        long width, long height, int rotation, string element, bool estimated,
        int referenceX = 0, int referenceY = 0, PrintOrientation orientation = PrintOrientation.None)
    {
        if (rotation != 0 && rotation != 90 && rotation != 180 && rotation != 270)
            throw new ArgumentOutOfRangeException(nameof(rotation));
        if (width < 0 || height < 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (rotation == 90 || rotation == 270)
        {
            var swap = width;
            width = height;
            height = swap;
        }

        ValidateEdges(pageWidth, pageHeight, x, y, checked(x + width), checked(y + height),
            element, estimated, referenceX, referenceY, orientation);
    }

    internal static void ValidateEdges(double pageWidth, double pageHeight, long left, long top,
        long right, long bottom, string element, bool estimated,
        int referenceX = 0, int referenceY = 0, PrintOrientation orientation = PrintOrientation.None)
    {
        var edgeLeft = (double)left + referenceX;
        var edgeRight = (double)right + referenceX;
        var edgeTop = (double)top + referenceY;
        var edgeBottom = (double)bottom + referenceY;

        switch (orientation)
        {
            case PrintOrientation.None:
                break;
            case PrintOrientation.Down:
                (edgeLeft, edgeRight) = (pageWidth - edgeRight, pageWidth - edgeLeft);
                (edgeTop, edgeBottom) = (pageHeight - edgeBottom, pageHeight - edgeTop);
                break;
            case PrintOrientation.Right:
                (edgeLeft, edgeRight, edgeTop, edgeBottom, pageWidth, pageHeight) =
                    (pageHeight - edgeBottom, pageHeight - edgeTop,
                        edgeLeft, edgeRight, pageHeight, pageWidth);
                break;
            case PrintOrientation.Left:
                (edgeLeft, edgeRight, edgeTop, edgeBottom, pageWidth, pageHeight) =
                    (edgeTop, edgeBottom, pageWidth - edgeRight,
                        pageWidth - edgeLeft, pageHeight, pageWidth);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(orientation));
        }

        if (edgeLeft < 0 || edgeTop < 0 || edgeRight > pageWidth || edgeBottom > pageHeight)
        {
            var estimate = estimated ? "估算的" : string.Empty;
            throw new ArgumentOutOfRangeException(nameof(element),
                $"{estimate}{element}边界超出标签页范围。页面尺寸为 {pageWidth:0.###} × {pageHeight:0.###} dots。");
        }
    }
}
