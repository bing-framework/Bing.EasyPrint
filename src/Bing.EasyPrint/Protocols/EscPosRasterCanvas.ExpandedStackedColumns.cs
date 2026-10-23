using System;
using System.Threading;
using Bing.EasyPrint.Printing;

namespace Bing.EasyPrint.Protocols;

public sealed partial class EscPosRasterCanvas
{
    internal static void ValidateExpandedStackedColumns(BarcodeType type, int? columns, bool composite = false)
    {
        if (!columns.HasValue) return;
        if (type != BarcodeType.Gs1DataBarExpandedStacked)
            throw new NotSupportedException("ExpandedStackedColumns 仅适用于 ExpandedStacked 条码。");
        var minimum = composite ? 2 : 1;
        if (columns.Value < minimum || columns.Value > 11)
            throw new ArgumentOutOfRangeException(nameof(columns), $"ExpandedStackedColumns 必须在 {minimum}–11 范围内。");
    }

    /// <summary>测量指定列数的 ExpandedStacked 编码区尺寸，不含 HRI、静区或旋转。</summary>
    /// <param name="data">与普通 Expanded 相同的 GS1 输入；不能为空。</param>
    /// <param name="rowHeight">每条数据行高度，单位为点，至少为模块宽度；建议 34X。</param>
    /// <param name="moduleWidth">模块宽度，单位为点，必须大于零。</param>
    /// <param name="columns">每行字符对数上限，范围 1–11；默认入口使用两列。</param>
    /// <param name="cancellationToken">在编码循环中观察的取消令牌。</param>
    /// <returns>包含行间三个 1X 分隔行的宽高，单位为点；不保留临时编码资源。</returns>
    /// <exception cref="ArgumentException">输入格式、容量或尺寸无效。</exception>
    /// <exception cref="OperationCanceledException">调用方请求取消。</exception>
    /// <exception cref="PrintResourceLimitException">当前作用域的资源预算不足。</exception>
    public static (int Width, int Height) MeasureExpandedStackedSize(string data, int rowHeight,
        int moduleWidth, int columns, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateExpandedStackedColumns(BarcodeType.Gs1DataBarExpandedStacked, columns);
        if (moduleWidth <= 0) throw new ArgumentOutOfRangeException(nameof(moduleWidth));
        if (rowHeight < moduleWidth) throw new ArgumentOutOfRangeException(nameof(rowHeight));
        using var operation = PrintResourceScope.BeginTemporary();
        var (matrix, first, last) = EncodeBarcode(BarcodeType.Gs1DataBarExpandedStacked, data, cancellationToken, columns);
        return (CheckedSymbolSize(last - first + 1, moduleWidth, nameof(moduleWidth)),
            ExpandedStackedHeight(matrix.Height, rowHeight, moduleWidth));
    }

    /// <summary>绘制指定列数的 ExpandedStacked，失败或取消时回滚本次绘制。</summary>
    /// <param name="x">旋转后编码区左上角 X 坐标，单位为点。</param>
    /// <param name="y">旋转后编码区左上角 Y 坐标，单位为点。</param>
    /// <param name="data">与普通 Expanded 相同的 GS1 输入；不能为空。</param>
    /// <param name="height">每条数据行高度，单位为点，至少 1X，建议 34X。</param>
    /// <param name="columns">每行字符对数上限，范围 1–11。</param>
    /// <param name="moduleWidth">模块点宽，默认 2，必须大于零。</param>
    /// <param name="humanReadable">是否绘制可读文字，默认 false。</param>
    /// <param name="humanReadableReserveHeight">可读文字预留点高，默认 0；关闭可读文字时必须为 0。</param>
    /// <param name="fontFamily">可读文字字体或别名；null 使用画布默认字体。</param>
    /// <param name="humanReadableFontSize">可读文字点字号，默认 12，范围 1–255。</param>
    /// <param name="rotation">顺时针角度，默认 0；支持 0、90、180、270。</param>
    /// <exception cref="ArgumentException">输入、尺寸、角度无效，或输出超过画布。</exception>
    /// <exception cref="OperationCanceledException">画布的调用方取消令牌已取消。</exception>
    /// <exception cref="PrintResourceLimitException">当前作用域的资源预算不足。</exception>
    public void DrawExpandedStackedBarcode(int x, int y, string data, int height, int columns,
        int moduleWidth = 2, bool humanReadable = false, int humanReadableReserveHeight = 0,
        string fontFamily = null, int humanReadableFontSize = DefaultHumanReadableFontSize, int rotation = 0)
        => DrawBarcodeWithColumns(BarcodeType.Gs1DataBarExpandedStacked, x, y, data, height, moduleWidth,
            humanReadable, humanReadableReserveHeight, fontFamily, humanReadableFontSize, rotation, columns);

    internal void DrawBarcodeWithColumns(BarcodeType type, int x, int y, string data, int height, int moduleWidth,
        bool humanReadable, int humanReadableReserveHeight, string fontFamily, int humanReadableFontSize,
        int rotation, int? columns)
        => DrawBarcode(type, x, y, data, height, moduleWidth, humanReadable,
            humanReadableReserveHeight, fontFamily, humanReadableFontSize, rotation, true, columns);
}
