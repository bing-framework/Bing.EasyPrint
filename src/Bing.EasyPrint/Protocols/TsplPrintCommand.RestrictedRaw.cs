using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Bing.EasyPrint.Printing;

namespace Bing.EasyPrint.Protocols;

public sealed partial class TsplPrintCommand : IRestrictedRawPrintCommand<TsplPrintCommand>
{
    private int _lastRestrictedRawActionCount = -1;
    private bool _restrictedRawTailTerminated;

    /// <summary>完整校验 TSPL 文本命令批次，并按原文写入。</summary>
    public TsplPrintCommand WriteRestrictedRaw(string raw)
    {
        if (raw == null) throw new ArgumentNullException(nameof(raw));
        if (raw.Length == 0) throw RestrictedRawSyntax.Error(Protocol, "<空批次>", 1, "批次不能为空。");
        if (RecordedActionCount == _lastRestrictedRawActionCount && !_restrictedRawTailTerminated &&
            raw[0] != '\r' && raw[0] != '\n')
            throw RestrictedRawSyntax.Error(Protocol, "<行分隔符>", 1,
                "上一批受限 Raw 的末行没有结束符；后续批次必须以 CR 或 LF 开始。");

        try { ValidateEncodable(raw, nameof(raw)); }
        catch (ArgumentException exception)
        {
            throw RestrictedRawSyntax.Error(Protocol, "<文本编码>", 1, exception.Message, exception);
        }

        using var tokenizerReservation = RestrictedRawSyntax.ReserveTokenizerWorkingSet(
            raw, "Tspl.RestrictedRawTokenizer");
        var candidate = (TsplPrintCommand)MemberwiseClone();
        var pendingMediaOptions = _pendingMediaOptions?.Clone();
        var lines = new List<RestrictedRawLine>(RestrictedRawSyntax.ReadLines(raw));
        var observedMediaCommands = new List<string>();
        using (PrintResourceScope.BeginTemporary())
        {
            using (candidate.SuppressActionRecording())
            {
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line.Text)) continue;
                    var command = RestrictedRawSyntax.CommandName(line.Text);
                    try
                    {
                        if (IsDrawingCommand(command))
                            ValidatePendingMediaBeforeDrawing(pendingMediaOptions, observedMediaCommands);
                        candidate.ParseRestrictedRawLine(line.Text.Trim(), line.Number);
                        TrackPendingMediaCommand(pendingMediaOptions, observedMediaCommands, command);
                    }
                    catch (Exception exception) when (IsRawValidationException(exception))
                    {
                        throw RestrictedRawSyntax.Error(Protocol, command, line.Number,
                            exception.Message, exception);
                    }
                }
            }
        }

        candidate._pendingMediaOptions = pendingMediaOptions;
        base.WriteRaw(raw);
        CopyRestrictedRawState(candidate);
        _lastRestrictedRawActionCount = RecordedActionCount;
        _restrictedRawTailTerminated = raw[raw.Length - 1] == '\r' || raw[raw.Length - 1] == '\n';
        return this;
    }

    private void ParseRestrictedRawLine(string line, int lineNumber)
    {
        var split = 0;
        while (split < line.Length && !char.IsWhiteSpace(line[split])) split++;
        var command = line.Substring(0, split).ToUpperInvariant();
        var argument = split == line.Length ? string.Empty : line.Substring(split).Trim();

        switch (command)
        {
            case "SIZE":
                ParseSize(argument);
                return;
            case "CODEPAGE":
                ValidateCodePage(argument);
                return;
            case "CLS":
                RequireNoArgument(command, argument);
                ClearImageBuffer();
                return;
            case "GAP":
                ApplyPair(command, argument, (first, second) => Gap(first, second));
                return;
            case "DIRECTION":
                Direction(RestrictedRawSyntax.ParseInt(argument, "DIRECTION"));
                return;
            case "REFERENCE":
                ApplyPair(command, argument, (first, second) => Reference(first, second));
                return;
            case "SPEED":
                Speed(RestrictedRawSyntax.ParseInt(argument, "SPEED"));
                return;
            case "DENSITY":
                Density(RestrictedRawSyntax.ParseInt(argument, "DENSITY"));
                return;
            case "TEXT":
                ParseText(argument);
                return;
            case "BAR":
                ParseBar(argument);
                return;
            case "BOX":
                ParseBox(argument);
                return;
            case "BARCODE":
                ParseBarcode(argument);
                return;
            case "QRCODE":
                ParseQrCode(argument);
                return;
            case "BITMAP":
                ParseBitmap(argument);
                return;
            case "FEED":
                Feed(argument.Length == 0 ? 0 : RestrictedRawSyntax.ParseInt(argument, "FEED"));
                return;
            case "PRINT":
                ParsePrint(argument);
                return;
            case "CUT":
                RequireNoArgument(command, argument);
                Cut();
                return;
            case "SELFTEST":
                RequireNoArgument(command, argument);
                SelfTest();
                return;
            case "EOJ":
                RequireNoArgument(command, argument);
                if (_cutter == null) throw new NotSupportedException("EOJ 仅允许出现在已声明切刀 Profile 的命令中。");
                WriteTsplLine("EOJ");
                return;
            case "SET":
                ParseCutterSetting(argument);
                return;
            default:
                throw new NotSupportedException("未知命令或不在受限语法白名单内。");
        }
    }

    private void ParseSize(string argument)
    {
        var dimensions = RestrictedRawSyntax.ParseDelimited(argument, 2);
        ParseDimension(dimensions[0], out var width, out var widthUnit);
        ParseDimension(dimensions[1], out var height, out var heightUnit);
        if (!string.Equals(widthUnit, heightUnit, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("SIZE 的宽高必须使用相同单位。");
        Size(width, height, widthUnit.ToLowerInvariant());
    }

    private static void ParseDimension(string value, out int dimension, out string unit)
    {
        var parts = value.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2) throw new FormatException("SIZE 尺寸必须由数值和 mm/dot 单位组成。");
        dimension = RestrictedRawSyntax.ParseInt(parts[0], "SIZE");
        unit = parts[1];
        if (!string.Equals(unit, "mm", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(unit, "dot", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("SIZE 单位只允许 mm 或 dot。");
    }

    private void ValidateCodePage(string value)
    {
        var expected = Encoding.CodePage == Encoding.UTF8.CodePage ? "UTF-8"
            : Encoding.CodePage == 936 ? "936"
            : Encoding.CodePage == Encoding.ASCII.CodePage ? "USA" : null;
        if (expected == null || !string.Equals(value, expected, StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException("CODEPAGE 必须与当前命令编码一致；未知代码页严格拒绝。");
        if (value.Equals("UTF-8", StringComparison.OrdinalIgnoreCase) && Encoding.CodePage != Encoding.UTF8.CodePage)
            throw new NotSupportedException("UTF-8 CODEPAGE 与当前编码不一致。");
    }

    private void ParseText(string argument)
    {
        RequirePageSize("TEXT");
        var fields = RestrictedRawSyntax.ParseDelimited(argument, 7);
        Text(RestrictedRawSyntax.ParseInt(fields[0], "TEXT x"),
            RestrictedRawSyntax.ParseInt(fields[1], "TEXT y"), fields[2],
            RestrictedRawSyntax.ParseInt(fields[3], "TEXT rotation"),
            RestrictedRawSyntax.ParseInt(fields[4], "TEXT xScale"),
            RestrictedRawSyntax.ParseInt(fields[5], "TEXT yScale"), fields[6]);
    }

    private void ParseBar(string argument)
    {
        RequirePageSize("BAR");
        var fields = RestrictedRawSyntax.ParseDelimited(argument, 4);
        FillRectangle(RestrictedRawSyntax.ParseInt(fields[0], "BAR x"),
            RestrictedRawSyntax.ParseInt(fields[1], "BAR y"),
            RestrictedRawSyntax.ParseInt(fields[2], "BAR width"),
            RestrictedRawSyntax.ParseInt(fields[3], "BAR height"));
    }

    private void ParseBox(string argument)
    {
        RequirePageSize("BOX");
        var fields = RestrictedRawSyntax.ParseDelimited(argument, 5);
        Box(RestrictedRawSyntax.ParseInt(fields[0], "BOX x0"),
            RestrictedRawSyntax.ParseInt(fields[1], "BOX y0"),
            RestrictedRawSyntax.ParseInt(fields[2], "BOX x1"),
            RestrictedRawSyntax.ParseInt(fields[3], "BOX y1"),
            RestrictedRawSyntax.ParseInt(fields[4], "BOX width"));
    }

    private void ParseBarcode(string argument)
    {
        RequirePageSize("BARCODE");
        var fields = RestrictedRawSyntax.ParseDelimited(argument, 9);
        Barcode(RestrictedRawSyntax.ParseInt(fields[0], "BARCODE x"),
            RestrictedRawSyntax.ParseInt(fields[1], "BARCODE y"), fields[2],
            RestrictedRawSyntax.ParseInt(fields[3], "BARCODE height"),
            RestrictedRawSyntax.ParseInt(fields[4], "BARCODE humanReadable"),
            RestrictedRawSyntax.ParseInt(fields[5], "BARCODE rotation"),
            RestrictedRawSyntax.ParseInt(fields[6], "BARCODE narrow"),
            RestrictedRawSyntax.ParseInt(fields[7], "BARCODE wide"), fields[8]);
    }

    private void ParseQrCode(string argument)
    {
        RequirePageSize("QRCODE");
        var fields = RestrictedRawSyntax.ParseDelimited(argument, 7);
        var levels = new[] { "L", "M", "Q", "H" };
        var level = Array.IndexOf(levels, fields[2].ToUpperInvariant());
        if (level < 0) throw new ArgumentException("QRCODE 纠错等级必须是 L、M、Q 或 H。");
        QrCode(RestrictedRawSyntax.ParseInt(fields[0], "QRCODE x"),
            RestrictedRawSyntax.ParseInt(fields[1], "QRCODE y"), level,
            RestrictedRawSyntax.ParseInt(fields[3], "QRCODE cellWidth"), fields[4],
            RestrictedRawSyntax.ParseInt(fields[5], "QRCODE rotation"), fields[6]);
    }

    private void ParseBitmap(string argument)
    {
        RequirePageSize("BITMAP");
        throw new NotSupportedException(
            "TSPL BITMAP 的线上载荷是二进制；ASCII HEX 不是等价的原始命令字节。请使用 Bitmap(..., asciiHex) 生成位图，或用可信 WriteRawBytes 写入完整二进制命令。");
    }

    private void ParsePrint(string argument)
    {
        var fields = RestrictedRawSyntax.ParseDelimited(argument, 2);
        Print(RestrictedRawSyntax.ParseInt(fields[0], "PRINT sets"),
            RestrictedRawSyntax.ParseInt(fields[1], "PRINT copies"));
    }

    private void ParseCutterSetting(string argument)
    {
        if (_cutter == null) throw new NotSupportedException("SET 切刀命令要求显式的 TSPL 切刀 Profile。");
        if (string.Equals(argument, "CUTTER OFF", StringComparison.OrdinalIgnoreCase))
        {
            if (!_cutter.SupportedCutModes.Contains(PrintCutMode.Full))
                throw new NotSupportedException("当前 Profile 未声明全切能力。");
            WriteTsplLine("SET CUTTER OFF");
            return;
        }
        if (string.Equals(argument, "PARTIAL_CUTTER OFF", StringComparison.OrdinalIgnoreCase))
        {
            if (!_cutter.SupportedCutModes.Contains(PrintCutMode.Partial))
                throw new NotSupportedException("当前 Profile 未声明半切能力。");
            WriteTsplLine("SET PARTIAL_CUTTER OFF");
            return;
        }
        if (string.Equals(argument, "CUTTER BATCH", StringComparison.OrdinalIgnoreCase) &&
            _cutter.Strategy == TsplCutStrategy.Batch && _cutter.SupportedCutModes.Contains(PrintCutMode.Full))
        {
            WriteTsplLine("SET CUTTER BATCH");
            return;
        }
        if (string.Equals(argument, "PARTIAL_CUTTER BATCH", StringComparison.OrdinalIgnoreCase) &&
            _cutter.Strategy == TsplCutStrategy.Batch && _cutter.SupportedCutModes.Contains(PrintCutMode.Partial))
        {
            WriteTsplLine("SET PARTIAL_CUTTER BATCH");
            return;
        }
        throw new NotSupportedException("SET 参数不属于当前 Profile 已声明的切刀操作。");
    }

    private void ApplyPair(string command, string value, Action<int, int> apply)
    {
        var fields = RestrictedRawSyntax.ParseDelimited(value, 2);
        apply(RestrictedRawSyntax.ParseInt(fields[0], command + " x"),
            RestrictedRawSyntax.ParseInt(fields[1], command + " y"));
    }

    private void RequirePageSize(string command)
    {
        if (!_hasPageSize) throw new InvalidOperationException($"{command} 绘制命令前必须先声明 SIZE。");
    }

    private static bool IsDrawingCommand(string command)
        => command.Equals("TEXT", StringComparison.OrdinalIgnoreCase) ||
           command.Equals("BAR", StringComparison.OrdinalIgnoreCase) ||
           command.Equals("BOX", StringComparison.OrdinalIgnoreCase) ||
           command.Equals("BARCODE", StringComparison.OrdinalIgnoreCase) ||
           command.Equals("QRCODE", StringComparison.OrdinalIgnoreCase) ||
           command.Equals("BITMAP", StringComparison.OrdinalIgnoreCase);

    private static void ValidatePendingMediaBeforeDrawing(TsplMediaOptions pending, List<string> observed)
    {
        if (pending == null) return;
        RequirePendingMediaCommand(pending.GapDistance.HasValue || pending.GapOffset.HasValue, "GAP", observed);
        RequirePendingMediaCommand(pending.Direction.HasValue, "DIRECTION", observed);
        RequirePendingMediaCommand(pending.ReferenceX.HasValue || pending.ReferenceY.HasValue, "REFERENCE", observed);
        RequirePendingMediaCommand(pending.Speed.HasValue, "SPEED", observed);
        RequirePendingMediaCommand(pending.Density.HasValue, "DENSITY", observed);
    }

    private static void RequirePendingMediaCommand(bool required, string command, List<string> observed)
    {
        if (required && !observed.Contains(command))
            throw new InvalidOperationException($"尚有延迟介质设置；绘制前必须在 Raw 批次中显式提供 {command}。原始字节不会自动插入介质命令。");
    }

    private static void TrackPendingMediaCommand(TsplMediaOptions pending, List<string> observed, string command)
    {
        if (pending == null) return;
        switch (command.ToUpperInvariant())
        {
            case "GAP":
                if (pending.GapDistance.HasValue || pending.GapOffset.HasValue)
                {
                    pending.GapDistance = null;
                    pending.GapOffset = null;
                    observed.Add("GAP");
                }
                break;
            case "DIRECTION":
                if (pending.Direction.HasValue)
                {
                    pending.Direction = null;
                    observed.Add("DIRECTION");
                }
                break;
            case "REFERENCE":
                if (pending.ReferenceX.HasValue || pending.ReferenceY.HasValue)
                {
                    pending.ReferenceX = null;
                    pending.ReferenceY = null;
                    observed.Add("REFERENCE");
                }
                break;
            case "SPEED":
                if (pending.Speed.HasValue)
                {
                    pending.Speed = null;
                    observed.Add("SPEED");
                }
                break;
            case "DENSITY":
                if (pending.Density.HasValue)
                {
                    pending.Density = null;
                    observed.Add("DENSITY");
                }
                break;
        }
    }

    private static void RequireNoArgument(string command, string argument)
    {
        if (argument.Length != 0) throw new FormatException($"{command} 不接受参数。");
    }

    private void CopyRestrictedRawState(TsplPrintCommand source)
    {
        _capabilities = source._capabilities;
        _pendingMediaOptions = source._pendingMediaOptions;
        _sizeWritten = source._sizeWritten;
        _allowRasterizedLines = source._allowRasterizedLines;
        _allowRasterizedTextStyles = source._allowRasterizedTextStyles;
        _profile = source._profile;
        _cutter = source._cutter;
        _pageWidth = source._pageWidth;
        _pageHeight = source._pageHeight;
        _pageUnit = source._pageUnit;
        _hasPageSize = source._hasPageSize;
        _direction = source._direction;
        _referenceX = source._referenceX;
        _referenceY = source._referenceY;
        _validatePageBounds = source._validatePageBounds;
        _hasStructuredOutput = source._hasStructuredOutput;
    }

    private static bool IsRawValidationException(Exception exception)
        => exception is ArgumentException || exception is NotSupportedException ||
           exception is InvalidOperationException || exception is OverflowException ||
           exception is FormatException;
}
