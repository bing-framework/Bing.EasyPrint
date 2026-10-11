using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using Bing.EasyPrint.Protocols;

namespace Bing.EasyPrint.Templates;

internal sealed class ProtocolTextMetrics
{
    private readonly CpclFontMetrics _cpcl;
    private readonly TextCellMetrics _cellClassifier;
    private readonly PrintProtocol _protocol;
    private readonly EscPosLayoutMode _escPosLayout;
    private readonly int _size;
    private readonly int _cellWidth;
    private readonly int _fullCellWidth;
    private readonly bool _bold;
    private readonly bool _underline;
    private readonly bool _rasterizeTextStyles;
    private readonly long _maxMeasurePixels;
    private readonly EscPosTextProfile _nativeText;
    private readonly RasterFontCatalog _rasterFonts;
    private readonly CancellationToken _cancellationToken;
    private readonly Dictionary<string, int> _rasterElementWidths = new Dictionary<string, int>();
    private readonly Dictionary<string, DeviceTextElementMetrics> _deviceTextElements;

    internal string FontFamily { get; }
    internal int LineHeight { get; }
    internal bool HasDeviceTextElementCalibrations => _deviceTextElements != null && _deviceTextElements.Count > 0;
    internal bool NeedsFullMeasure =>
        _protocol == PrintProtocol.EscPos && _escPosLayout == EscPosLayoutMode.Raster ||
        _rasterizeTextStyles && (_bold || _underline);

    internal ProtocolTextMetrics(PrintProtocol protocol, EscPosLayoutMode escPosLayout, int size, int font,
        Encoding encoding, string text, bool bold, double? deviceHalfCellWidthAt24 = null,
        string fontFamily = null, int? nativeTabWidthDots = null, double? deviceFullCellWidthAt24 = null,
        bool underline = false, long maxMeasurePixels = long.MaxValue, string deviceFontName = null,
        double? deviceLineHeightAt24 = null, DeviceFontCalibration fontCalibration = null,
        EscPosTextProfile nativeText = null, RasterFontCatalog rasterFonts = null,
        CancellationToken cancellationToken = default(CancellationToken), bool rasterizeTextStyles = false)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _cancellationToken = cancellationToken;
        _protocol = protocol;
        _escPosLayout = escPosLayout;
        _size = size;
        _bold = bold;
        _underline = underline;
        _rasterizeTextStyles = rasterizeTextStyles && (protocol == PrintProtocol.Tspl || protocol == PrintProtocol.Zpl);
        _maxMeasurePixels = maxMeasurePixels;
        _rasterFonts = rasterFonts;
        if (protocol == PrintProtocol.Cpcl)
            _cpcl = new CpclFontMetrics(size, font, encoding);
        else
        {
            if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size));
            if (font != 0)
                throw new NotSupportedException("CPCL 字体编号不能直接用于其他协议；请使用语义字体选择。");
        }
        _cellClassifier = new TextCellMetrics(encoding);
        var fixedCell = protocol == PrintProtocol.Tspl && deviceFontName != null && deviceFontName != "0"
            ? TsplFontSizing.FixedCell(deviceFontName) : (Width: 0, Height: 0);
        var scale = fixedCell.Height == 0 ? 1 : TsplFontSizing.FixedScale(deviceFontName, size);
        var calibrationScale = fixedCell.Height == 0 ? size / 24.0 :
            (double)scale / TsplFontSizing.FixedScale(deviceFontName, 24);
        var fixedLineHeight = fixedCell.Height == 0 ? 0 : checked(fixedCell.Height * scale);
        if (fontCalibration != null)
        {
            LineHeight = Math.Max(fixedLineHeight,
                checked((int)Math.Ceiling(fontCalibration.LineHeightAt24 * calibrationScale)));
            _cellWidth = Math.Max(checked(fixedCell.Width * scale),
                checked((int)Math.Ceiling(fontCalibration.HalfCellWidthAt24 * calibrationScale)));
            _fullCellWidth = checked((int)Math.Ceiling(fontCalibration.FullCellWidthAt24 * calibrationScale));
        }
        else
        {
            LineHeight = deviceLineHeightAt24.HasValue
                ? Math.Max(fixedLineHeight, checked((int)Math.Ceiling(size * deviceLineHeightAt24.Value / 24)))
                : fixedLineHeight == 0 ? size : fixedLineHeight;
            _cellWidth = protocol == PrintProtocol.EscPos && escPosLayout == EscPosLayoutMode.Native
                ? nativeTabWidthDots.GetValueOrDefault(96) / 8
                : checked((int)Math.Ceiling((fixedCell.Width == 0 ? deviceHalfCellWidthAt24 ?? 18 :
                    deviceHalfCellWidthAt24.HasValue ? deviceHalfCellWidthAt24.Value * fixedCell.Height / 24 : fixedCell.Width)
                    * (fixedCell.Width == 0 ? size / 24.0 : scale)));
            _fullCellWidth = deviceFullCellWidthAt24.HasValue
                ? checked((int)Math.Ceiling((fixedCell.Height == 0 ? size : fixedCell.Height * scale)
                    * deviceFullCellWidthAt24.Value / 24))
                : checked(_cellWidth * 2);
        }
        if (fontCalibration?.TextElements != null && !_rasterizeTextStyles)
        {
            _deviceTextElements = new Dictionary<string, DeviceTextElementMetrics>(fontCalibration.TextElements.Count);
            foreach (var entry in fontCalibration.TextElements)
            {
                var calibration = entry.Value;
                var advance = checked((int)Math.Ceiling(calibration.AdvanceAt24 * calibrationScale));
                var hasInkBounds = calibration.InkLeftAt24.HasValue;
                _deviceTextElements.Add(entry.Key, new DeviceTextElementMetrics(advance, hasInkBounds,
                    hasInkBounds ? checked((int)Math.Floor(calibration.InkLeftAt24.Value * calibrationScale)) : 0,
                    hasInkBounds ? checked((int)Math.Floor(calibration.InkTopAt24.Value * calibrationScale)) : 0,
                    hasInkBounds ? checked((int)Math.Ceiling(calibration.InkRightAt24.Value * calibrationScale)) : 0,
                    hasInkBounds ? checked((int)Math.Ceiling(calibration.InkBottomAt24.Value * calibrationScale)) : 0));
            }
        }
        if (NeedsFullMeasure)
        {
            FontFamily = EscPosRasterCanvas.ResolveFontFamilyName(text, fontFamily, bold, _rasterFonts, _cancellationToken);
            if (_rasterizeTextStyles)
            {
                var ink = EscPosRasterCanvas.MeasureTextInkBounds(text, size, bold, underline, FontFamily,
                    _maxMeasurePixels, _rasterFonts, _cancellationToken);
                if (!ink.IsEmpty)
                    LineHeight = Math.Max(LineHeight, Math.Max(0, ink.Top) + ink.Height);
            }
        }
        if (protocol == PrintProtocol.EscPos && escPosLayout == EscPosLayoutMode.Native && nativeText != null)
        {
            _nativeText = nativeText;
            _cellWidth = nativeText.HalfCellWidthDots;
            _fullCellWidth = nativeText.FullCellWidthDots;
        }
    }

    internal int Advance(string element)
    {
        _cancellationToken.ThrowIfCancellationRequested();
        if (_protocol == PrintProtocol.Cpcl) return _cpcl.Advance(element);
        if (_nativeText != null) return _nativeText.MeasureTextWidth(element, _cancellationToken);
        if (_deviceTextElements != null && _deviceTextElements.TryGetValue(element, out var deviceMetrics))
            return deviceMetrics.Advance;
        if (NeedsFullMeasure)
        {
            if (_rasterElementWidths.TryGetValue(element, out var cached)) return cached;
            var width = RasterLayoutWidth(element);
            if (_rasterElementWidths.Count < 256) _rasterElementWidths.Add(element, width);
            return width;
        }
        var cells = _cellClassifier.Advance(element);
        return checked(cells / 2 * _fullCellWidth + cells % 2 * _cellWidth);
    }

    internal long Measure(string text)
    {
        _cancellationToken.ThrowIfCancellationRequested();
        if (_nativeText != null)
        {
            var result = _nativeText.MeasureTextWidth(text, _cancellationToken);
            _cancellationToken.ThrowIfCancellationRequested();
            return result;
        }
        if (NeedsFullMeasure)
            return RasterLayoutWidth(text);
        long width = 0;
        var iterator = StringInfo.GetTextElementEnumerator(text);
        while (iterator.MoveNext()) width = checked(width + Advance(iterator.GetTextElement()));
        return width;
    }

    internal DeviceTextBounds MeasureBounds(string text)
    {
        var width = Measure(text);
        long left = 0;
        long top = 0;
        long right = width;
        long bottom = LineHeight;
        if (_deviceTextElements == null || text.Length == 0)
            return new DeviceTextBounds(0, 0, checked((int)width), LineHeight);

        long pen = 0;
        var iterator = StringInfo.GetTextElementEnumerator(text);
        while (iterator.MoveNext())
        {
            _cancellationToken.ThrowIfCancellationRequested();
            var element = iterator.GetTextElement();
            if (_deviceTextElements.TryGetValue(element, out var metrics) && metrics.HasInkBounds)
            {
                left = Math.Min(left, checked(pen + metrics.InkLeft));
                top = Math.Min(top, metrics.InkTop);
                right = Math.Max(right, checked(pen + metrics.InkRight));
                bottom = Math.Max(bottom, metrics.InkBottom);
            }
            pen = checked(pen + Advance(element));
        }
        return new DeviceTextBounds(checked((int)left), checked((int)top),
            checked((int)right), checked((int)bottom));
    }

    private int RasterLayoutWidth(string text)
    {
        if (text.Length == 0) return 0;
        var advance = EscPosRasterCanvas.MeasureTextWidth(text, _size, _bold, FontFamily, _rasterFonts, _cancellationToken);
        var ink = EscPosRasterCanvas.MeasureTextInkBounds(text, _size, _bold, _underline,
            FontFamily, _maxMeasurePixels, _rasterFonts, _cancellationToken);
        return ink.IsEmpty ? advance : Math.Max(advance, checked(Math.Max(0, -ink.Left) + ink.Right));
    }

    private readonly struct DeviceTextElementMetrics
    {
        internal int Advance { get; }
        internal bool HasInkBounds { get; }
        internal int InkLeft { get; }
        internal int InkTop { get; }
        internal int InkRight { get; }
        internal int InkBottom { get; }

        internal DeviceTextElementMetrics(int advance, bool hasInkBounds, int inkLeft, int inkTop,
            int inkRight, int inkBottom)
        {
            Advance = advance;
            HasInkBounds = hasInkBounds;
            InkLeft = inkLeft;
            InkTop = inkTop;
            InkRight = inkRight;
            InkBottom = inkBottom;
        }
    }
}

internal readonly struct DeviceTextBounds
{
    internal int Left { get; }
    internal int Top { get; }
    internal int Right { get; }
    internal int Bottom { get; }
    internal int Width => checked(Right - Left);
    internal int Height => checked(Bottom - Top);

    internal DeviceTextBounds(int left, int top, int right, int bottom)
    {
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }
}
