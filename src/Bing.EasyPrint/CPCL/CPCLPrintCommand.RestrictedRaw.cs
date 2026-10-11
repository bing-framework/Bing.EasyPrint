using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Bing.EasyPrint.Core;
using Bing.EasyPrint.Protocols;
using Bing.EasyPrint.Printing;

namespace Bing.EasyPrint.CPCL;

/// <summary>
/// CPCL 受限原始文本命令支持。
/// </summary>
// ReSharper disable once InconsistentNaming
public partial class CPCLPrintCommand
{
    /// <summary>
    /// 校验并写入 CPCL 受限原始文本命令。
    /// </summary>
    /// <param name="raw">一条或多条 CPCL 文本命令。</param>
    /// <returns>当前打印命令实例。</returns>
    public CPCLPrintCommand WriteRestrictedRaw(string raw)
    {
        if (raw == null)
            throw new ArgumentNullException(nameof(raw));
        if (raw.Length == 0)
            throw CpclRestrictedRawParser.Error("<empty>", 1, "受限 Raw 不能为空。");

        EnsureRestrictedRawProgramCanBeMixed();
        using var tokenizerReservation = RestrictedRawSyntax.ReserveTokenizerWorkingSet(
            raw, "CPCL.RestrictedRawTokenizer");
        var parser = new CpclRestrictedRawParser(_encoding, CommandInfo, _restrictedFormatState);
        var result = parser.Parse(raw);

        if (result.HasBoundaryCommand)
        {
            if (!result.HasBegin || !result.HasPageWidth || !result.HasPrint)
                throw CpclRestrictedRawParser.Error("<program>", result.LastLineNumber,
                    "包含任务边界的批次必须完整包含 !、PAGE-WIDTH 和 PRINT。");
            if (Content.Begin != null || Content.PageWidth != null || Content.End != null ||
                Content.Content.Count != 0)
                throw new ArgumentException(
                    "CPCL 受限 Raw 包含任务边界时不能与已有命令记录混合。",
                    nameof(raw));

            using var operation = PrintResourceScope.BeginTemporary();
            var lease = PrintStorageBudget.Reserve(
                PrintStorageBudget.TextBytes(raw), "Cpcl.RestrictedRaw");
            try
            {
                operation?.CommitReservations();
            }
            catch
            {
                lease.Dispose();
                throw;
            }

            _restrictedRawProgramLease?.Dispose();
            _restrictedRawProgramLease = lease;
            _restrictedRawProgram = raw;
            ApplyRestrictedCommandInfo(result);
            return this;
        }

        EnsureRestrictedRawProgramCanBeMixed();
        if (!raw.EndsWith("\r\n", StringComparison.Ordinal))
            throw CpclRestrictedRawParser.Error("<line separator>", result.LastLineNumber,
                "仅含页面正文的受限 Raw 后会自动追加 PRINT，末行必须以 CRLF 结束。");

        Content.Add(new RawMetadata(raw, false));
        ApplyRestrictedCommandInfo(result);
        return this;
    }

    /// <summary>
    /// 将受限 Raw 中解析出的页面信息同步到命令状态。
    /// </summary>
    private void ApplyRestrictedCommandInfo(CpclRestrictedParseResult result)
    {
        if (result.HasBegin)
        {
            CommandInfo.Offset = result.Offset;
            CommandInfo.Height = result.Height;
            CommandInfo.Qty = result.Quantity;
            CommandInfo.HasBegin = true;
        }

        if (result.HasPageWidth)
        {
            CommandInfo.Width = result.Width;
        }

        if (result.HasPrint)
            CommandInfo.HasEnd = true;
        CommandInfo.PagerRotate = result.Orientation;
        _restrictedFormatState = result.FormatState.Clone();
    }
}

internal enum CpclFieldAlignment
{
    Left,
    Center,
    Right
}

internal sealed class CpclRestrictedFormatState
{
    internal int HorizontalScale { get; private set; } = 1;
    internal int VerticalScale { get; private set; } = 1;
    internal int BarcodeTextCell { get; private set; }
    internal int BarcodeTextOffset { get; private set; }
    internal int CharacterSpacing { get; set; }
    internal CpclFieldAlignment Alignment { get; private set; } = CpclFieldAlignment.Left;
    internal int? AlignmentEnd { get; private set; }
    internal bool MagnificationKnown { get; set; } = true;
    internal bool BarcodeTextKnown { get; set; } = true;
    internal bool AlignmentKnown { get; set; } = true;
    internal bool SpacingKnown { get; set; } = true;
    internal bool RotationKnown { get; set; } = true;
    internal bool HasDrawn { get; set; }

    internal CpclRestrictedFormatState Clone() => (CpclRestrictedFormatState)MemberwiseClone();

    internal void SetMagnification(int horizontal, int vertical)
    {
        HorizontalScale = Math.Max(1, horizontal);
        VerticalScale = Math.Max(1, vertical);
        MagnificationKnown = true;
    }

    internal void Reset()
    {
        HorizontalScale = 1;
        VerticalScale = 1;
        BarcodeTextCell = 0;
        BarcodeTextOffset = 0;
        CharacterSpacing = 0;
        Alignment = CpclFieldAlignment.Left;
        AlignmentEnd = null;
        MagnificationKnown = true;
        BarcodeTextKnown = true;
        AlignmentKnown = true;
        SpacingKnown = true;
        RotationKnown = true;
        HasDrawn = false;
    }

    internal void SetBarcodeText(bool enabled, int fontCell, int offset)
    {
        BarcodeTextCell = enabled ? fontCell : 0;
        BarcodeTextOffset = enabled ? offset : 0;
        BarcodeTextKnown = true;
    }

    internal void SetAlignment(CpclFieldAlignment alignment, int? end)
    {
        Alignment = alignment;
        AlignmentEnd = end;
        AlignmentKnown = true;
    }

    internal void MarkUnknown()
    {
        MagnificationKnown = false;
        BarcodeTextKnown = false;
        AlignmentKnown = false;
        SpacingKnown = false;
        RotationKnown = false;
        HasDrawn = false;
    }
}

/// <summary>
/// CPCL 受限原始解析结果。
/// </summary>
internal sealed class CpclRestrictedParseResult
{
    internal bool HasBegin;
    internal bool HasPageWidth;
    internal bool HasPrint;
    internal bool HasBoundaryCommand;
    internal int Offset;
    internal int Width;
    internal int Height;
    internal int Quantity = 1;
    internal int Orientation;
    internal int LastLineNumber;
    internal CpclRestrictedFormatState FormatState;
}

/// <summary>
/// CPCL 受限文本解析器。
/// </summary>
internal sealed class CpclRestrictedRawParser
{
    private readonly Encoding _encoding;
    private readonly CPCLCommandInfo _commandInfo;
    private readonly List<string> _lines = new List<string>();
    private readonly CpclGeometryState _state;
    private readonly HashSet<int> _fontGroups = new HashSet<int>();
    private CpclRestrictedParseResult _result;
    private int _index;

    internal CpclRestrictedRawParser(Encoding encoding, CPCLCommandInfo commandInfo,
        CpclRestrictedFormatState formatState)
    {
        _encoding = encoding ?? throw new ArgumentNullException(nameof(encoding));
        _commandInfo = commandInfo ?? throw new ArgumentNullException(nameof(commandInfo));
        if (!Enum.IsDefined(typeof(PrintOrientation), commandInfo.PagerRotate))
            throw Error("<state>", 1, "现有 CPCL 页面方向无法验证。");
        _state = new CpclGeometryState(commandInfo, formatState);
    }

    internal CpclRestrictedParseResult Parse(string raw)
    {
        ValidateRawText(raw);
        SplitLines(raw);
        if (_lines.Count == 0)
            throw Error("<empty>", 1, "受限 Raw 不能为空。");

        _result = new CpclRestrictedParseResult
        {
            Offset = _commandInfo.Offset,
            Width = _commandInfo.Width,
            Height = _commandInfo.Height,
            Quantity = _commandInfo.Qty < 1 ? 1 : _commandInfo.Qty,
            Orientation = _commandInfo.PagerRotate
        };

        while (_index < _lines.Count)
        {
            var lineNumber = _index + 1;
            _result.LastLineNumber = lineNumber;
            var line = _lines[_index++];
            if (string.IsNullOrWhiteSpace(line))
                throw Error("<blank>", lineNumber, "命令行不能为空。");
            if (_state.PrintSeen)
                throw Error(GetCommand(line), lineNumber, "PRINT 必须是批次中的最后一条命令。");
            try
            {
                ParseCommand(line, lineNumber);
            }
            catch (OverflowException exception)
            {
                throw Error(GetCommand(line), lineNumber, "参数计算超出受限范围。", exception);
            }
            catch (ArgumentOutOfRangeException exception) when (exception.ParamName != "raw")
            {
                throw Error(GetCommand(line), lineNumber, exception.Message, exception);
            }
        }

        if (_result.HasPrint && (!_state.HasHeight || !_state.HasWidth))
            throw Error("PRINT", _lines.Count, "包含 PRINT 的批次必须明确任务高度和页面宽度。");
        if (_result.HasBoundaryCommand && (!_result.HasBegin || !_result.HasPageWidth || !_result.HasPrint))
            throw Error("<program>", _lines.Count, "包含任务边界的批次必须完整包含 !、PAGE-WIDTH 和 PRINT。");

        _result.Offset = _state.Offset;
        _result.Width = _state.Width;
        _result.Height = _state.Height;
        _result.Quantity = _state.Quantity;
        _result.Orientation = _state.OrientationValue;
        _result.FormatState = _state.FormatState;
        return _result;
    }

    private void ParseCommand(string rawLine, int lineNumber)
    {
        var line = rawLine.TrimStart();
        var command = GetCommand(line);
        switch (command.ToUpperInvariant())
        {
            case "!":
                ParseBegin(line, lineNumber);
                return;
            case "PAGE-WIDTH":
            case "PW":
                ParsePageWidth(line, lineNumber);
                return;
            case "PRINT":
                RequireExactFields(line, 1, command, lineNumber);
                _state.PrintSeen = true;
                _result.HasPrint = true;
                _result.HasBoundaryCommand = true;
                return;
            case "T":
            case "TEXT":
            case "T90":
            case "TEXT90":
            case "T180":
            case "TEXT180":
            case "T270":
            case "TEXT270":
            case "VT":
            case "VTEXT":
                ParseText(line, lineNumber, command, true);
                return;
            case "L":
            case "LINE":
            case "IL":
                ParseLineCommand(line, lineNumber, command, false);
                return;
            case "BOX":
                ParseBox(line, lineNumber, command);
                return;
            case "B":
            case "VB":
                ParseBarcode(line, lineNumber, command);
                return;
            case "EG":
            case "VEG":
            case "CG":
            case "VCG":
                ParseGraphics(line, lineNumber, command);
                return;
            case "CONCAT":
            case "VCONCAT":
                ParseConcat(line, lineNumber, command);
                return;
            case "ML":
                ParseMultiline(line, lineNumber, command);
                return;
            case "BT":
                ParseBarcodeText(line, lineNumber, command);
                return;
            case "FG":
                ParseFontGroup(line, lineNumber, command);
                return;
            default:
                ParseControl(line, lineNumber, command);
                return;
        }
    }

    private void ParseBegin(string line, int lineNumber)
    {
        var fields = Fields(line);
        if (fields.Length != 6 || fields[0] != "!")
            throw Error("!", lineNumber, "格式头必须为 ! offset hres vres height qty。");
        if (lineNumber != 1)
            throw Error("!", lineNumber, "任务头必须是受限 Raw 批次的第一条命令。");
        var offset = ParseInt(fields[1], "!", lineNumber);
        if (offset < 0)
            throw Error("!", lineNumber, "任务偏移量不能为负数。");
        var horizontalResolution = ParsePositiveInt(fields[2], "!", lineNumber);
        var verticalResolution = ParsePositiveInt(fields[3], "!", lineNumber);
        if (horizontalResolution > 1200 || verticalResolution > 1200)
            throw Error("!", lineNumber, "打印分辨率不能超过 1200 dpi。");
        var height = ParsePositiveInt(fields[4], "!", lineNumber);
        var quantity = ParsePositiveInt(fields[5], "!", lineNumber);
        if (quantity > 1024)
            throw Error("!", lineNumber, "打印份数不能超过 1024。");
        if (_result.HasBegin)
            throw Error("!", lineNumber, "受限批次只能包含一个格式头。");

        _state.SetHeader(offset, horizontalResolution, verticalResolution, height, quantity);
        _result.HasBegin = true;
        _result.Orientation = _state.OrientationValue;
        _result.HasBoundaryCommand = true;
    }

    private void ParsePageWidth(string line, int lineNumber)
    {
        var fields = Fields(line);
        if (fields.Length != 2)
            throw Error(fields.Length == 0 ? "PAGE-WIDTH" : fields[0], lineNumber,
                "PAGE-WIDTH 必须只包含一个正整数参数。");
        var width = ParsePositiveInt(fields[1], fields[0], lineNumber);
        if (_result.HasPageWidth)
            throw Error(fields[0], lineNumber, "受限批次只能包含一个页面宽度命令。");
        if (_state.DrawSeen)
            throw Error(fields[0], lineNumber, "PAGE-WIDTH 必须位于所有绘制命令之前。");
        _state.SetWidth(width);
        _result.HasPageWidth = true;
        _result.HasBoundaryCommand = true;
    }

    private void ParseText(string line, int lineNumber, string command, bool requireData)
    {
        string data;
        var fields = Prefix(line, 5, out data);
        if (fields.Length != 5 || !string.Equals(fields[0], command, StringComparison.OrdinalIgnoreCase))
            throw Error(command, lineNumber, "文本命令参数不完整。");
        var font = ParseFont(fields[1], command, lineNumber);
        var size = ParseInt(fields[2], command, lineNumber);
        if (size != 0)
            throw Error(command, lineNumber, "受限文本只接受内置字体的 size=0；其它字体尺寸无法可靠估算。");
        var x = ParseInt(fields[3], command, lineNumber);
        var y = ParseInt(fields[4], command, lineNumber);
        if (requireData && data.Length == 0)
            throw Error(command, lineNumber, "文本命令必须包含文本数据。");
        if (x < 0 || y < 0)
            throw Error(command, lineNumber, "文本坐标不能为负数。");
        _state.RequireTextState(command, lineNumber);
        var width = MeasureTextWidth(data, font, _state.HorizontalScale, _state.CharacterSpacing);
        var height = checked((long)font * _state.VerticalScale);
        var rotation = GetTextRotation(command);
        var alignedX = _state.ResolveAlignedX(x, width, rotation, command, lineNumber);
        _state.RequireExtent(alignedX, y, width, height, rotation, command, lineNumber, true);
    }

    private void ParseLineCommand(string line, int lineNumber, string command, bool box)
    {
        var fields = Fields(line);
        if (fields.Length != 6)
            throw Error(command, lineNumber, "线命令必须包含五个参数。");
        var x0 = ParseInt(fields[1], command, lineNumber);
        var y0 = ParseInt(fields[2], command, lineNumber);
        var x1 = ParseInt(fields[3], command, lineNumber);
        var y1 = ParseInt(fields[4], command, lineNumber);
        var width = ParsePositiveInt(fields[5], command, lineNumber);
        if (x0 < 0 || y0 < 0 || x1 < 0 || y1 < 0)
            throw Error(command, lineNumber, "线坐标不能为负数。");
        _state.RequireRectangle(x0, y0, x1, y1, width, command, lineNumber,
            estimated: true, expandStroke: !box);
    }

    private void ParseBox(string line, int lineNumber, string command)
    {
        ParseLineCommand(line, lineNumber, command, true);
    }

    private void ParseBarcode(string line, int lineNumber, string command)
    {
        string remainder;
        var prefix = Prefix(line, 2, out remainder);
        if (prefix.Length != 2)
            throw Error(command, lineNumber, "条码命令参数不完整。");
        var subtype = prefix[1].ToUpperInvariant();
        switch (subtype)
        {
            case "QR":
                ParseQr(line, lineNumber, command);
                return;
            case "PDF-417":
                ParsePdf417(line, lineNumber, command);
                return;
            case "MAXICODE":
                ParseMaxicode(line, lineNumber, command);
                return;
            case "AZTEC":
                ParseAztec(line, lineNumber, command);
                return;
            case "RSS":
                ParseRss(line, lineNumber, command);
                return;
        }

        string data;
        var fields = Prefix(line, 7, out data);
        if (fields.Length != 7 || data.Length == 0)
            throw Error(command, lineNumber, "一维条码命令参数不完整。");
        var type = ParseBarcodeType(fields[1], command, lineNumber);
        var narrowWidth = ParsePositiveInt(fields[2], command, lineNumber);
        var ratio = ParsePositiveInt(fields[3], command, lineNumber);
        var height = ParsePositiveInt(fields[4], command, lineNumber);
        var x = ParseInt(fields[5], command, lineNumber);
        var y = ParseInt(fields[6], command, lineNumber);
        if (x < 0 || y < 0)
            throw Error(command, lineNumber, "条码坐标不能为负数。");
        _state.RequireTextState(command, lineNumber, true);
        if (narrowWidth > 32 || ratio > 3 || height > 4096)
            throw Error(command, lineNumber, "条码宽度比例或高度超出受限范围。");
        ValidateBarcodeData(type, data, command, lineNumber);
        var estimatedWidth = EstimateCpclBarcodeWidth(type, data, narrowWidth, ratio, command, lineNumber);
        var estimatedHeight = checked(height + _state.BarcodeTextHeight);
        if (_state.BarcodeTextCell > 0)
            estimatedWidth = Math.Max(estimatedWidth,
                MeasureTextWidth(data, _state.BarcodeTextCell, _state.HorizontalScale,
                    _state.CharacterSpacing));
        var rotation = command.Equals("VB", StringComparison.OrdinalIgnoreCase) ? 90 : 0;
        _state.RequireExtent(x, y, estimatedWidth, estimatedHeight, rotation, command, lineNumber, true);
    }

    private void ParseRss(string line, int lineNumber, string command)
    {
        throw Error(command, lineNumber, "RSS 编码和分段 footprint 依赖固件实现，当前受限子集不接受该变体。");
    }

    private void ParseQr(string line, int lineNumber, string command)
    {
        var fields = Fields(line);
        if (fields.Length != 8 || !string.Equals(fields[1], "QR", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(fields[4], "M", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(fields[6], "U", StringComparison.OrdinalIgnoreCase))
            throw Error(command, lineNumber, "QR 命令必须使用 B/VB QR x y M model U unit 格式。");
        var x = ParseInt(fields[2], command, lineNumber);
        var y = ParseInt(fields[3], command, lineNumber);
        var model = ParsePositiveInt(fields[5], command, lineNumber);
        var unit = ParsePositiveInt(fields[7], command, lineNumber);
        if (model > 2 || unit > 32 || x < 0 || y < 0)
            throw Error(command, lineNumber, "QR 参数超出受限范围。");
        var payload = ConsumeBlock("ENDQR", command, lineNumber, true);
        if (payload.Count != 1)
            throw Error(command, lineNumber, "受限 QR 只接受单段 ASCII 模式数据。");
        ParseQrPayload(payload[0], unit, command, lineNumber, out var qrSize);
        if (model == 1 && qrSize > checked(unit * 21))
            throw Error(command, lineNumber, "model 1 QR 数据超过版本 1 容量。");
        _state.RequireExtent(x, y, qrSize, qrSize, command.Equals("VB", StringComparison.OrdinalIgnoreCase) ? 90 : 0,
            command, lineNumber, true);
    }

    private void ParsePdf417(string line, int lineNumber, string command)
    {
        var fields = Fields(line);
        if (fields.Length != 12 || !string.Equals(fields[1], "PDF-417", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(fields[4], "XD", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(fields[6], "YD", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(fields[8], "C", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(fields[10], "S", StringComparison.OrdinalIgnoreCase))
            throw Error(command, lineNumber, "PDF417 命令参数格式不正确。");
        var x = ParseInt(fields[2], command, lineNumber);
        var y = ParseInt(fields[3], command, lineNumber);
        var width = ParsePositiveInt(fields[5], command, lineNumber);
        var height = ParsePositiveInt(fields[7], command, lineNumber);
        var columns = ParsePositiveInt(fields[9], command, lineNumber);
        var security = ParseInt(fields[11], command, lineNumber);
        if (width > 32 || height > 32 || columns > 30 || security < 0 || security > 8 || x < 0 || y < 0)
            throw Error(command, lineNumber, "PDF417 参数超出受限范围。");
        var payload = ConsumeBlock("ENDPDF", command, lineNumber, true);
        var encodedLength = GetPdf417PayloadLength(payload, command, lineNumber);
        var rows = checked((encodedLength + (1L << (security + 1)) + 3 + columns - 1) / columns);
        if (rows > 90 || rows * columns > 929)
            throw Error(command, lineNumber, "PDF417 数据与纠错码超过 90 行或 929 个码字容量。");
        var footprintWidth = checked((long)(17 * columns + 69) * width);
        var footprintHeight = checked(rows * height);
        _state.RequireExtent(x, y, footprintWidth, footprintHeight,
            command.Equals("VB", StringComparison.OrdinalIgnoreCase) ? 90 : 0,
            command, lineNumber, true);
    }

    private void ParseMaxicode(string line, int lineNumber, string command)
    {
        var fields = Fields(line);
        if (fields.Length != 4 || !string.Equals(fields[1], "MAXICODE", StringComparison.OrdinalIgnoreCase))
            throw Error(command, lineNumber, "MAXICODE 命令必须使用 B/VB MAXICODE x y 格式。");
        var x = ParseInt(fields[2], command, lineNumber);
        var y = ParseInt(fields[3], command, lineNumber);
        if (x < 0 || y < 0)
            throw Error(command, lineNumber, "MAXICODE 坐标不能为负数。");
        var payload = ConsumeBlock("ENDMAXICODE", command, lineNumber, true, true);
        if (payload.Count > 26)
            throw Error(command, lineNumber, "MAXICODE 字段超过当前受限字典上限。");
        var fieldsSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var upsMode = false;
        foreach (var row in payload)
            ValidateMaxicodeRow(row, x, y, fieldsSeen, ref upsMode, command, lineNumber);
        if (!fieldsSeen.Contains("POST"))
            throw Error(command, lineNumber, "受限 MAXICODE 必须包含 POST 字段。");
        if (upsMode && fieldsSeen.Contains("MSG"))
            throw Error(command, lineNumber, "UPS5=1 时不能同时指定 MSG 字段。");
        if (!upsMode && HasUpsMaxicodeField(fieldsSeen))
            throw Error(command, lineNumber, "UPS 数据字段必须先声明 UPS5 1。");
        var width = Math.Max(1, (int)Math.Ceiling(_state.HorizontalResolution * 1.25));
        var height = Math.Max(1, (int)Math.Ceiling(_state.VerticalResolution * 1.25));
        if (_state.HorizontalResolution < 150 || _state.VerticalResolution < 150)
            throw Error(command, lineNumber, "MaxiCode 需要至少 150 dpi 的 CPCL 任务分辨率；设备外形尺寸按 1.25 英寸估算。");
        _state.RequireExtent(x, y, width, height, 0, command, lineNumber, true);
    }

    private void ParseAztec(string line, int lineNumber, string command)
    {
        var fields = Fields(line);
        if (fields.Length != 8 || !string.Equals(fields[1], "AZTEC", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(fields[4], "XD", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(fields[6], "EC", StringComparison.OrdinalIgnoreCase))
            throw Error(command, lineNumber, "AZTEC 命令参数格式不正确。");
        var x = ParseInt(fields[2], command, lineNumber);
        var y = ParseInt(fields[3], command, lineNumber);
        var width = ParsePositiveInt(fields[5], command, lineNumber);
        var ec = ParseInt(fields[7], command, lineNumber);
        if (width > 36 || ec < 0 || ec > 99 || x < 0 || y < 0)
            throw Error(command, lineNumber, "AZTEC 参数超出受限范围。");
        var payload = ConsumeBlock("ENDAZTEC", command, lineNumber, true, alternateTerminator: "ENDQR");
        var byteCount = 0;
        foreach (var item in payload)
        {
            if (LooksLikeCommandLine(item))
                throw Error(command, lineNumber, "AZTEC 块数据不能夹带命令样式的第二条指令。");
            byteCount = checked(byteCount + GetStrictByteCount(item, command, lineNumber));
        }
        if (byteCount > 2894)
            throw Error(command, lineNumber, "AZTEC 受限 payload 超过受支持容量。");
        var maximumSymbol = checked(151L * width);
        _state.RequireExtent(x, y, maximumSymbol, maximumSymbol,
            command.Equals("VB", StringComparison.OrdinalIgnoreCase) ? 90 : 0,
            command, lineNumber, true);
    }

    private void ParseGraphics(string line, int lineNumber, string command)
    {
        string data;
        var fields = Prefix(line, 5, out data);
        if (fields.Length != 5 || data.Length == 0)
            throw Error(command, lineNumber, "图形命令参数不完整。");
        var width = ParsePositiveInt(fields[1], command, lineNumber);
        var height = ParsePositiveInt(fields[2], command, lineNumber);
        var x = ParseInt(fields[3], command, lineNumber);
        var y = ParseInt(fields[4], command, lineNumber);
        if (x < 0 || y < 0)
            throw Error(command, lineNumber, "图形坐标不能为负数。");
        var expectedBytes = checked((long)width * height);
        if (command.Equals("EG", StringComparison.OrdinalIgnoreCase) ||
            command.Equals("VEG", StringComparison.OrdinalIgnoreCase))
        {
            if (data.Length != checked(expectedBytes > int.MaxValue / 2 ? int.MaxValue : (int)expectedBytes * 2))
                throw Error(command, lineNumber, "扩展图形数据长度与宽高不符。");
            foreach (var character in data)
                if (!IsHex(character))
                    throw Error(command, lineNumber, "扩展图形只能包含十六进制字符。");
        }
        else
        {
            if (GetStrictByteCount(data, command, lineNumber) != expectedBytes)
                throw Error(command, lineNumber, "压缩图形编码字节数与宽高不符。");
        }
        _state.RequireExtent(x, y, checked((long)width * 8), height, 0, command, lineNumber, false);
    }

    private void ParseConcat(string line, int lineNumber, string command)
    {
        var fields = Fields(line);
        if (fields.Length != 3)
            throw Error(command, lineNumber, "CONCAT 命令必须包含 x 和 y。");
        var x = ParseInt(fields[1], command, lineNumber);
        var y = ParseInt(fields[2], command, lineNumber);
        var payload = ConsumeBlock("ENDCONCAT", command, lineNumber, true);
        _state.RequireTextState(command, lineNumber);
        var extent = ParseConcatExtent(payload, command, lineNumber);
        var rotation = command.Equals("VCONCAT", StringComparison.OrdinalIgnoreCase) ? 90 : 0;
        var alignedX = _state.ResolveAlignedX(x, extent.Width, rotation, command, lineNumber);
        _state.RequireExtent(alignedX, y, extent.Width, extent.Height,
            rotation,
            command, lineNumber, true);
    }

    private void ParseMultiline(string line, int lineNumber, string command)
    {
        var fields = Fields(line);
        if (fields.Length != 2)
            throw Error(command, lineNumber, "ML 命令必须包含行高。");
        var lineHeight = ParsePositiveInt(fields[1], command, lineNumber);
        if (_index >= _lines.Count)
            throw Error(command, lineNumber, "ML 缺少文本命令和结束命令。");
        var textLineNumber = _index + 1;
        var textLine = _lines[_index++].Trim();
        var textCommand = GetCommand(textLine).ToUpperInvariant();
        if (textCommand != "T" && textCommand != "TEXT" && textCommand != "T90" &&
            textCommand != "TEXT90" && textCommand != "T180" && textCommand != "TEXT180" &&
            textCommand != "T270" && textCommand != "TEXT270" && textCommand != "VT" &&
            textCommand != "VTEXT")
            throw Error(textCommand, textLineNumber, "ML 只接受文本命令。");
        string inlineData;
        var textFields = Prefix(textLine, 5, out inlineData);
        if (textFields.Length != 5)
            throw Error(textCommand, textLineNumber, "ML 文本命令参数不完整。");
        if (inlineData.Length != 0)
            throw Error(textCommand, textLineNumber, "ML 文本命令头不能夹带首行文本数据。");
        var font = ParseFont(textFields[1], textCommand, textLineNumber);
        if (ParseInt(textFields[2], textCommand, textLineNumber) != 0)
            throw Error(textCommand, textLineNumber, "ML 只接受 size=0 的内置字体。");
        var x = ParseInt(textFields[3], textCommand, textLineNumber);
        var y = ParseInt(textFields[4], textCommand, textLineNumber);
        if (x < 0 || y < 0)
            throw Error(textCommand, textLineNumber, "ML 坐标不能为负数。");
        _state.RequireTextState(textCommand, textLineNumber);
        var count = 0;
        var maxTextWidth = 0L;
        var cellHeight = checked((long)font * _state.VerticalScale);
        while (_index < _lines.Count)
        {
            var payloadLineNumber = _index + 1;
            var payload = _lines[_index++];
            if (IsTerminator(payload, "ENDML"))
                break;
            ValidatePayload(payload, command, payloadLineNumber, "ENDML");
            count++;
            maxTextWidth = Math.Max(maxTextWidth,
                MeasureTextWidth(payload, font, _state.HorizontalScale,
                    _state.CharacterSpacing));
        }
        if (_index == 0 || _index > _lines.Count ||
            !IsPreviousTerminator("ENDML"))
            throw Error(command, lineNumber, "ML 缺少 ENDML。");
        var lines = Math.Max(1, count);
        var height = checked((long)lineHeight * (lines - 1) + cellHeight);
        var rotation = GetTextRotation(textCommand);
        var alignedX = _state.ResolveAlignedX(x, maxTextWidth, rotation, command, lineNumber);
        _state.RequireExtent(alignedX, y, maxTextWidth, height, rotation,
            command, lineNumber, true);
    }

    private void ParseBarcodeText(string line, int lineNumber, string command)
    {
        var fields = Fields(line);
        if (fields.Length == 2 && fields[1].Equals("OFF", StringComparison.OrdinalIgnoreCase))
        {
            _state.SetBarcodeText(false, 0, 0);
            return;
        }
        if (fields.Length != 4)
            throw Error(command, lineNumber, "BT 必须为 OFF 或 font size offset。");
        var font = ParseFont(fields[1], command, lineNumber);
        var size = ParseInt(fields[2], command, lineNumber);
        var offset = ParseInt(fields[3], command, lineNumber);
        if (size != 0 || offset < 0)
            throw Error(command, lineNumber, "受限 BT 只接受 size=0 和非负 offset。");
        _state.SetBarcodeText(true, font, offset);
    }

    private void ParseFontGroup(string line, int lineNumber, string command)
    {
        var fields = Fields(line);
        if (fields.Length < 2 || ((fields.Length - 2) % 2) != 0)
            throw Error(command, lineNumber, "FG 必须包含字体组及成对的字体参数。");
        var group = ParseInt(fields[1], command, lineNumber);
        if (group < 0 || group > 9)
            throw Error(command, lineNumber, "FG 字体组必须在 0 到 9 之间。");
        if (_fontGroups.Add(group) && _fontGroups.Count > 10)
            throw Error(command, lineNumber, "最多允许定义十个 FG 字体组。");
        for (var i = 2; i < fields.Length; i += 2)
        {
            ParseFont(fields[i], command, lineNumber);
            if (ParseInt(fields[i + 1], command, lineNumber) != 0)
                throw Error(command, lineNumber, "受限 FG 只接受 size=0 的内置字体参数。");
        }
    }

    private void ParseControl(string line, int lineNumber, string command)
    {
        var fields = Fields(line);
        switch (command.ToUpperInvariant())
        {
            case "FORM":
            case "JOURNAL":
            case "PACE":
            case "AUTO-PACE":
            case "NO-PACE":
            case "REWIND-ON":
            case "REWIND-OFF":
            case "CUT":
            case "PARTIAL-CUT":
                RequireExactFields(line, 1, command, lineNumber);
                return;
            case "CENTER":
            case "RIGHT":
                if (fields.Length > 2)
                    throw Error(command, lineNumber, "对齐命令参数过多。");
                var end = fields.Length == 2
                    ? ParseNonNegativeInt(fields[1], command, lineNumber)
                    : (int?)null;
                _state.SetAlignment(command.Equals("CENTER", StringComparison.OrdinalIgnoreCase)
                    ? CpclFieldAlignment.Center
                    : CpclFieldAlignment.Right, end);
                return;
            case "LEFT":
                RequireExactFields(line, 1, command, lineNumber);
                _state.SetAlignment(CpclFieldAlignment.Left, null);
                return;
            case "ENCODING":
            case "COUNTRY":
                if (fields.Length != 2)
                    throw Error(command, lineNumber, "命令必须包含一个编码或国家参数。");
                ParseToken(fields[1], command, lineNumber);
                return;
            case "CONTRAST":
                RequireRange(fields, command, lineNumber, 2, 0, 3);
                return;
            case "TONE":
                RequireRange(fields, command, lineNumber, 2, -99, 200);
                return;
            case "SPEED":
                RequireRange(fields, command, lineNumber, 2, 0, 5);
                return;
            case "SETSP":
                RequireRange(fields, command, lineNumber, 2, 0, 999);
                _state.SetCharacterSpacing(ParseInt(fields[1], command, lineNumber));
                return;
            case "WAIT":
            case "PRE-TENSION":
            case "POST-TENSION":
            case "PREFEED":
            case "POSTFEED":
            case "CUT-AT":
            case "BEEP":
                RequireNonNegative(fields, command, lineNumber, 2);
                return;
            case "PRESENT-AT":
                RequireNonNegative(fields, command, lineNumber, 3);
                return;
            case "ON-OUT-OF-PAPER":
                if (fields.Length != 3)
                    throw Error(command, lineNumber, "ON-OUT-OF-PAPER 必须包含操作和重试次数。");
                ParseToken(fields[1], command, lineNumber);
                ParseNonNegativeInt(fields[2], command, lineNumber);
                return;
            case "ON-FEED":
                if (fields.Length != 2)
                    throw Error(command, lineNumber, "ON-FEED 必须包含操作参数。");
                ParseToken(fields[1], command, lineNumber);
                return;
            case "UNDERLINE":
                if (fields.Length != 2 ||
                    (!fields[1].Equals("ON", StringComparison.OrdinalIgnoreCase) &&
                     !fields[1].Equals("OFF", StringComparison.OrdinalIgnoreCase)))
                    throw Error(command, lineNumber, "UNDERLINE 只接受 ON 或 OFF。");
                return;
            case "SETBOLD":
                RequireRange(fields, command, lineNumber, 2, 0, 1);
                return;
            case "SETMAG":
                RequireRange(fields, command, lineNumber, 3, 0, 16);
                _state.SetTextScale(ParseInt(fields[1], command, lineNumber),
                    ParseInt(fields[2], command, lineNumber));
                return;
            case "R":
                if (fields.Length != 2)
                    throw Error(command, lineNumber, "R 必须包含旋转角度。");
                var rotate = ParseInt(fields[1], command, lineNumber);
                if (rotate != 0 && rotate != 90 && rotate != 180 && rotate != 270)
                    throw Error(command, lineNumber, "R 只接受 0、90、180 或 270。");
                _state.SetRotation(rotate, lineNumber);
                _result.Orientation = _state.OrientationValue;
                return;
            case "PATTERN":
                RequireRange(fields, command, lineNumber, 2, 100, 106);
                return;
            case "COUNT":
                if (fields.Length != 2)
                    throw Error(command, lineNumber, "COUNT 必须包含增量参数。");
                ParseInt(fields[1], command, lineNumber);
                return;
            default:
                throw Error(command, lineNumber, "该命令未纳入 CPCL 受限 Raw 语法。");
        }
    }

    private int ParseFont(string value, string command, int lineNumber)
    {
        if (value == "24") return 24;
        if (value == "55" || value == "0") return 16;
        throw Error(command, lineNumber, "受限子集只接受可估算的 CPCL 内置字体 0、24 或 55。");
    }

    private BarcodeType ParseBarcodeType(string value, string command, int lineNumber)
    {
        switch (value.ToUpperInvariant())
        {
            case "128": return BarcodeType.Code128;
            case "39": return BarcodeType.Code39;
            case "93": return BarcodeType.Code93;
            case "CODABAR": return BarcodeType.Codabar;
            case "EAN8": return BarcodeType.Ean8;
            case "EAN13": return BarcodeType.Ean13;
            case "UPCA": return BarcodeType.UpcA;
            case "UPCE": return BarcodeType.UpcE;
            case "I2OF5": return BarcodeType.I2OF5;
            case "I2OF5C": return BarcodeType.I2OF5C;
            case "I2OF5G": return BarcodeType.I2OF5G;
            case "UCCEAN128": return BarcodeType.UccEan128;
            case "MSI": return BarcodeType.Msi;
            case "POSTNET": return BarcodeType.Postnet;
            case "FIM": return BarcodeType.Fim;
            case "QR":
            case "PDF-417":
            case "MAXICODE":
            case "AZTEC":
                throw Error(command, lineNumber, "二维条码必须使用对应的完整块语法。");
            case "RSS":
                throw Error(command, lineNumber, "RSS 编码和分段 footprint 依赖固件实现，当前受限子集不接受该变体。");
            default:
                throw Error(command, lineNumber, "条码类型未包含在 CPCL 受限白名单中。");
        }
    }

    private static void ValidateBarcodeData(BarcodeType type, string data, string command, int lineNumber)
    {
        foreach (var character in data)
            if (character < 32 || character > 126)
                throw Error(command, lineNumber, "受限条码数据只接受可打印 ASCII。");

        var digitsOnly = true;
        foreach (var character in data)
            if (character < '0' || character > '9') digitsOnly = false;

        var valid = type switch
        {
            BarcodeType.Ean8 => digitsOnly && (data.Length == 7 || data.Length == 8),
            BarcodeType.Ean13 => digitsOnly && (data.Length == 12 || data.Length == 13),
            BarcodeType.UpcA => digitsOnly && (data.Length == 11 || data.Length == 12),
            BarcodeType.UpcE => digitsOnly && (data.Length == 6 || data.Length == 7 || data.Length == 8),
            BarcodeType.I2OF5 => digitsOnly,
            BarcodeType.I2OF5C => digitsOnly,
            BarcodeType.I2OF5G => digitsOnly,
            BarcodeType.Msi => digitsOnly,
            BarcodeType.Postnet => HasOnly(data, "0123456789-"),
            BarcodeType.Fim => data.Length == 1 && HasOnly(data, "ABCDE"),
            BarcodeType.Code39 => HasOnly(data, "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ-. $/+%*"),
            BarcodeType.Code93 => HasOnly(data, "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ-. $/+%*"),
            BarcodeType.Codabar => HasOnly(data, "0123456789-$:/.+ABCD"),
            BarcodeType.Code128 => true,
            BarcodeType.UccEan128 => true,
            _ => false
        };
        if (!valid)
            throw Error(command, lineNumber, "条码数据与 CPCL 条码制式的受限字符集或长度不符。");
    }

    private static bool HasOnly(string value, string allowed)
    {
        foreach (var character in value)
            if (allowed.IndexOf(character) < 0) return false;
        return value.Length > 0;
    }

    private static long EstimateCpclBarcodeWidth(BarcodeType type, string data, int narrowWidth,
        int ratio, string command, int lineNumber)
    {
        var wideWidth = checked((long)narrowWidth * ratio);
        long estimate;
        switch (type)
        {
            case BarcodeType.Ean8: estimate = checked(67L * narrowWidth); break;
            case BarcodeType.Ean13:
            case BarcodeType.UpcA: estimate = checked(95L * narrowWidth); break;
            case BarcodeType.UpcE: estimate = checked(51L * narrowWidth); break;
            case BarcodeType.I2OF5:
            case BarcodeType.I2OF5C:
            case BarcodeType.I2OF5G:
                var digits = data.Length + (type == BarcodeType.I2OF5 ? 0 :
                    type == BarcodeType.I2OF5C ? 1 : 2);
                digits += digits % 2;
                estimate = checked((long)(3 * digits + 6) * narrowWidth +
                                   (long)(2 * digits + 1) * wideWidth);
                break;
            case BarcodeType.Fim:
                estimate = checked(17L * narrowWidth);
                break;
            case BarcodeType.Code128:
            case BarcodeType.UccEan128:
                estimate = EscPosRasterCanvas.MeasureBarcodeWidth(type, data, narrowWidth);
                break;
            default:
                estimate = checked((long)data.Length * 18 * wideWidth + 128 * wideWidth);
                break;
        }
        if (estimate <= 0)
            throw Error(command, lineNumber, "无法估算该条码 footprint。");
        return estimate;
    }

    private void ParseQrPayload(string payload, int moduleSize, string command, int lineNumber,
        out int symbolSize)
    {
        var comma = payload.IndexOf(',');
        if (comma < 2 || comma == payload.Length - 1)
            throw Error(command, lineNumber, "QR 数据行必须为纠错级别、可选掩码、A 模式和非空数据。");
        var prefix = payload.Substring(0, comma);
        var level = prefix[0] switch
        {
            'L' => QrCodeCorrectionLevel.L,
            'M' => QrCodeCorrectionLevel.M,
            'Q' => QrCodeCorrectionLevel.Q,
            'H' => QrCodeCorrectionLevel.H,
            _ => throw Error(command, lineNumber, "QR 纠错级别必须为 L、M、Q 或 H。")
        };
        var modeIndex = 1;
        if (prefix.Length == 3)
        {
            if (prefix[1] < '0' || prefix[1] > '8')
                throw Error(command, lineNumber, "QR 掩码编号必须为 0 到 8。");
            modeIndex++;
        }
        else if (prefix.Length != 2)
        {
            throw Error(command, lineNumber, "QR 受限数据行只接受一个字符段。");
        }
        if (prefix[modeIndex] != 'A')
            throw Error(command, lineNumber, "QR 受限子集只接受 A 模式。");

        var data = payload.Substring(comma + 1);
        const string qrAlphanumeric = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ $%*+-./:";
        if (!HasOnly(data, qrAlphanumeric))
            throw Error(command, lineNumber, "QR A 模式数据含有不支持的字符。");
        symbolSize = EscPosRasterCanvas.MeasureQrCodeSize(data, moduleSize, level);
    }

    private long GetPdf417PayloadLength(List<string> payload, string command, int lineNumber)
    {
        long length = payload.Count;
        foreach (var item in payload)
        {
            if (item.Length == 0 || LooksLikeCommandLine(item))
                throw Error(command, lineNumber, "PDF417 受限数据只接受非空 ASCII 数据行。");
            length = checked(length + GetStrictByteCount(item, command, lineNumber));
        }
        return length;
    }

    private void ValidateMaxicodeRow(string row, int x, int y, HashSet<string> seen,
        ref bool upsMode, string command, int lineNumber)
    {
        string data;
        var fields = Prefix(row, 1, out data);
        if (fields.Length != 1 || data.Length == 0)
            throw Error(command, lineNumber, "MAXICODE 数据行必须包含受支持的字段标签和非空数据。");
        var field = fields[0].ToUpperInvariant();
        if (!seen.Add(field))
            throw Error(command, lineNumber, "受限 MAXICODE 不接受重复字段。");

        switch (field)
        {
            case "X":
                var maxicodeX = ParseNonNegativeInt(data, command, lineNumber);
                if (maxicodeX > 65535 || maxicodeX != x)
                    throw Error(command, lineNumber, "MAXICODE 内嵌 X 必须与外层坐标一致。");
                return;
            case "Y":
                var maxicodeY = ParseNonNegativeInt(data, command, lineNumber);
                if (maxicodeY > 65535 || maxicodeY != y)
                    throw Error(command, lineNumber, "MAXICODE 内嵌 Y 必须与外层坐标一致。");
                return;
            case "H":
                ParseNonNegativeInt(data, command, lineNumber);
                return;
            case "FILLC":
            case "NOTEXT":
            case "EOT":
            case "RS":
            case "GS":
                RequireTextLength(data, 1, 1, "MAXICODE " + field, command, lineNumber);
                return;
            case "ZIPPER":
                RequireBinarySetting(data, "ZIPPER", command, lineNumber);
                return;
            case "UPS5":
                RequireBinarySetting(data, "UPS5", command, lineNumber);
                upsMode = data == "1";
                return;
            case "LPMS":
                RequireTextLength(data, 3, 3, "MAXICODE LPMS", command, lineNumber);
                return;
            case "POST":
                RequireTextLength(data, 1, 9, "MAXICODE POST", command, lineNumber);
                if (!HasOnly(data, "0123456789-"))
                    throw Error(command, lineNumber, "MAXICODE POST 只接受数字和连字符。");
                return;
            case "CC":
            case "SC":
                var numeric = ParseNonNegativeInt(data, command, lineNumber);
                if (numeric > 65535)
                    throw Error(command, lineNumber, "MAXICODE " + field + " 超过五位数范围。");
                return;
            case "MSG":
                RequireTextLength(data, 1, 88, "MAXICODE MSG", command, lineNumber);
                return;
            case "SCAC": RequireTextLength(data, 1, 9, "MAXICODE SCAC", command, lineNumber); return;
            case "HEAD": RequireTextLength(data, 1, 9, "MAXICODE HEAD", command, lineNumber); return;
            case "SHIPPER": RequireTextLength(data, 1, 6, "MAXICODE SHIPPER", command, lineNumber); return;
            case "PICKDAY": RequireTextLength(data, 1, 3, "MAXICODE PICKDAY", command, lineNumber); return;
            case "SHIPID": RequireTextLength(data, 1, 30, "MAXICODE SHIPID", command, lineNumber); return;
            case "NX": RequireTextLength(data, 1, 7, "MAXICODE NX", command, lineNumber); return;
            case "WEIGH": RequireTextLength(data, 1, 3, "MAXICODE WEIGH", command, lineNumber); return;
            case "VAL": RequireTextLength(data, 1, 1, "MAXICODE VAL", command, lineNumber); return;
            case "STADDR": RequireTextLength(data, 1, 35, "MAXICODE STADDR", command, lineNumber); return;
            case "TN":
                RequireTextLength(data, 2, 18, "MAXICODE TN", command, lineNumber);
                if (!data.StartsWith("1Z", StringComparison.OrdinalIgnoreCase) ||
                    !HasOnly(data.Substring(2), "0123456789"))
                    throw Error(command, lineNumber, "MAXICODE TN 只接受 1Z 加数字的受限格式。");
                return;
            case "CITY": RequireTextLength(data, 1, 20, "MAXICODE CITY", command, lineNumber); return;
            case "ST": RequireTextLength(data, 2, 2, "MAXICODE ST", command, lineNumber); return;
            case "EXTRA": RequireTextLength(data, 1, 81, "MAXICODE EXTRA", command, lineNumber); return;
            default:
                throw Error(command, lineNumber, "MAXICODE 字段不在受限 ASCII 字段白名单中。");
        }
    }

    private static bool HasUpsMaxicodeField(HashSet<string> fields)
    {
        return fields.Contains("SCAC") || fields.Contains("HEAD") || fields.Contains("SHIPPER") ||
               fields.Contains("PICKDAY") || fields.Contains("SHIPID") || fields.Contains("NX") ||
               fields.Contains("WEIGH") || fields.Contains("VAL") || fields.Contains("STADDR") ||
               fields.Contains("TN") || fields.Contains("CITY") || fields.Contains("ST") ||
               fields.Contains("EXTRA");
    }

    private static void RequireBinarySetting(string value, string field, string command, int lineNumber)
    {
        if (value != "0" && value != "1")
            throw Error(command, lineNumber, "MAXICODE " + field + " 只允许 0 或 1。");
    }

    private static void RequireTextLength(string value, int minimum, int maximum, string field,
        string command, int lineNumber)
    {
        if (value.Length < minimum || value.Length > maximum)
            throw Error(command, lineNumber, field + " 字段长度超出受限范围。");
        foreach (var character in value)
            if (character < 32 || character > 126)
                throw Error(command, lineNumber, field + " 只接受可打印 ASCII。");
    }

    private (long Width, long Height) ParseConcatExtent(List<string> payload, string command, int lineNumber)
    {
        long width = 0;
        long height = 0;
        foreach (var row in payload)
        {
            string data;
            var fields = Prefix(row, 3, out data);
            if (fields.Length != 3 || data.Length == 0)
                throw Error(command, lineNumber, "CONCAT 数据行必须包含字体、size、offset 和文本。");
            var font = ParseFont(fields[0], command, lineNumber);
            if (ParseInt(fields[1], command, lineNumber) != 0)
                throw Error(command, lineNumber, "受限 CONCAT 只接受 size=0 的内置字体。");
            var offset = ParseNonNegativeInt(fields[2], command, lineNumber);
            var cell = checked((long)font * _state.VerticalScale);
            width = checked(width + MeasureTextWidth(data, font, _state.HorizontalScale,
                _state.CharacterSpacing) + offset);
            height = Math.Max(height, cell);
        }
        return (width, height);
    }

    private static int GetTextRotation(string command)
    {
        return command.Equals("T90", StringComparison.OrdinalIgnoreCase) ||
               command.Equals("TEXT90", StringComparison.OrdinalIgnoreCase) ||
               command.Equals("T270", StringComparison.OrdinalIgnoreCase) ||
               command.Equals("TEXT270", StringComparison.OrdinalIgnoreCase) ||
               command.Equals("VT", StringComparison.OrdinalIgnoreCase) ||
               command.Equals("VTEXT", StringComparison.OrdinalIgnoreCase) ? 90 : 0;
    }

    private static bool LooksLikeCommandLine(string line)
    {
        var firstSpace = line.IndexOf(' ');
        if (firstSpace <= 0 || firstSpace == line.Length - 1) return false;
        var token = line.Substring(0, firstSpace);
        var allCommandCharacters = true;
        foreach (var character in token)
            if (!((character >= 'A' && character <= 'Z') ||
                  (character >= '0' && character <= '9') || character == '-'))
                allCommandCharacters = false;
        if (!allCommandCharacters) return false;
        var next = firstSpace + 1;
        return next < line.Length && line[next] >= '0' && line[next] <= '9';
    }

    private static long MeasureTextWidth(string text, int fontCell, int horizontalScale,
        int characterSpacing)
    {
        long width = 0;
        var cellWidth = checked((long)fontCell * horizontalScale);
        foreach (var character in text)
            width = checked(width + (character > 126 ? cellWidth : (cellWidth + 1) / 2));
        if (text.Length > 1)
            width = checked(width + (long)characterSpacing * (text.Length - 1));
        return width;
    }

    private List<string> ConsumeBlock(string terminator, string command, int lineNumber,
        bool requirePayload, bool taggedRows = false, string alternateTerminator = null)
    {
        var payload = new List<string>();
        while (_index < _lines.Count)
        {
            var payloadLineNumber = _index + 1;
            var current = _lines[_index++];
            if (IsTerminator(current, terminator) ||
                (alternateTerminator != null && IsTerminator(current, alternateTerminator)))
            {
                if (payload.Count == 0 && requirePayload)
                    throw Error(command, payloadLineNumber, "块命令必须包含数据行。");
                return payload;
            }
            if (taggedRows)
                ValidateBlockCharacters(current, command, payloadLineNumber);
            else
                ValidatePayload(current, command, payloadLineNumber, terminator);
            payload.Add(current);
        }
        throw Error(command, lineNumber, "块命令缺少 " + terminator +
            (alternateTerminator == null ? string.Empty : " 或 " + alternateTerminator) + "。");
    }

    private void ValidatePayload(string payload, string command, int lineNumber, string terminator)
    {
        if (string.IsNullOrWhiteSpace(payload))
            throw Error(command, lineNumber, "块数据行不能为空。");
        var trimmed = payload.Trim();
        if (IsTerminator(trimmed, terminator))
            return;
        var payloadCommand = GetCommand(trimmed);
        if (IsRecognizedCommand(payloadCommand) || payloadCommand.StartsWith("END", StringComparison.OrdinalIgnoreCase))
            throw Error(payloadCommand, lineNumber, "块数据不能夹带 CPCL 控制命令。");
        ValidateBlockCharacters(payload, command, lineNumber);
    }

    private static void ValidateBlockCharacters(string payload, string command, int lineNumber)
    {
        foreach (var character in payload)
            if (char.IsControl(character) || character > 126)
                throw Error(command, lineNumber, "受限块数据只接受可打印 ASCII，不能包含控制或二进制字符。");
    }

    private static bool IsRecognizedCommand(string command)
    {
        switch (command.ToUpperInvariant())
        {
            case "!":
            case "PAGE-WIDTH":
            case "PW":
            case "PRINT":
            case "T":
            case "TEXT":
            case "T90":
            case "TEXT90":
            case "T180":
            case "TEXT180":
            case "T270":
            case "TEXT270":
            case "VT":
            case "VTEXT":
            case "L":
            case "LINE":
            case "IL":
            case "BOX":
            case "B":
            case "VB":
            case "EG":
            case "VEG":
            case "CG":
            case "VCG":
            case "CONCAT":
            case "VCONCAT":
            case "ML":
            case "BT":
            case "FG":
            case "FORM":
            case "JOURNAL":
            case "PACE":
            case "AUTO-PACE":
            case "NO-PACE":
            case "REWIND-ON":
            case "REWIND-OFF":
            case "CUT":
            case "PARTIAL-CUT":
            case "CUT-AT":
            case "ENCODING":
            case "COUNTRY":
            case "CENTER":
            case "LEFT":
            case "RIGHT":
            case "UNDERLINE":
            case "SETBOLD":
            case "SETMAG":
            case "R":
            case "COUNT":
            case "PATTERN":
            case "BEEP":
            case "WAIT":
            case "SPEED":
            case "SETSP":
            case "PREFEED":
            case "POSTFEED":
            case "PRESENT-AT":
            case "ON-FEED":
            case "ON-OUT-OF-PAPER":
            case "CONTRAST":
            case "TONE":
                return true;
            default:
                return false;
        }
    }

    private static bool IsTerminator(string value, string terminator)
    {
        return string.Equals(value.Trim(), terminator, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsPreviousTerminator(string terminator)
    {
        return _index > 0 && IsTerminator(_lines[_index - 1], terminator);
    }

    private void SplitLines(string raw)
    {
        var start = 0;
        var lineNumber = 1;
        for (var i = 0; i < raw.Length; i++)
        {
            if (raw[i] == '\r')
            {
                if (i + 1 >= raw.Length || raw[i + 1] != '\n')
                {
                    GetRawLocation(raw, i, out var command, out lineNumber);
                    throw Error(command, lineNumber, "CPCL 受限 Raw 只接受 CRLF 行结束符。");
                }
                _lines.Add(raw.Substring(start, i - start));
                i++;
                start = i + 1;
                lineNumber++;
            }
            else if (raw[i] == '\n')
            {
                GetRawLocation(raw, i, out var command, out lineNumber);
                throw Error(command, lineNumber, "CPCL 受限 Raw 只接受 CRLF 行结束符。");
            }
        }
        if (start < raw.Length)
            _lines.Add(raw.Substring(start));
    }

    private void ValidateRawText(string raw)
    {
        if (raw.Length == 0)
            throw Error("<empty>", 1, "受限 Raw 不能为空。");
        var strictEncoding = (Encoding)_encoding.Clone();
        strictEncoding.EncoderFallback = EncoderFallback.ExceptionFallback;
        try
        {
            strictEncoding.GetByteCount(raw);
        }
        catch (EncoderFallbackException exception)
        {
            GetRawLocation(raw, Math.Max(0, exception.Index), out var command, out var lineNumber);
            throw Error(command, lineNumber,
                "数据含有无法使用 " + _encoding.WebName + " 编码的字符。", exception);
        }

        for (var index = 0; index < raw.Length; index++)
        {
            var character = raw[index];
            if (character == '\r' || character == '\n')
                continue;
            if (char.IsControl(character))
            {
                GetRawLocation(raw, index, out var command, out var lineNumber);
                throw Error(command, lineNumber, "受限 Raw 不能包含控制字符。");
            }
        }
    }

    private static void GetRawLocation(string raw, int offset, out string command, out int lineNumber)
    {
        var lineStart = 0;
        lineNumber = 1;
        var end = Math.Min(offset, raw.Length);
        for (var index = 0; index < end; index++)
        {
            if (raw[index] == '\r' && index + 1 < end && raw[index + 1] == '\n')
            {
                index++;
                lineStart = index + 1;
                lineNumber++;
            }
            else if (raw[index] == '\n')
            {
                lineStart = index + 1;
                lineNumber++;
            }
        }

        var lineEnd = lineStart;
        while (lineEnd < raw.Length && raw[lineEnd] != '\r' && raw[lineEnd] != '\n') lineEnd++;
        command = GetCommand(raw.Substring(lineStart, lineEnd - lineStart));
        if (command.Length == 0) command = "<text>";
    }

    private int GetStrictByteCount(string value, string command, int lineNumber)
    {
        var strictEncoding = (Encoding)_encoding.Clone();
        strictEncoding.EncoderFallback = EncoderFallback.ExceptionFallback;
        try
        {
            return strictEncoding.GetByteCount(value);
        }
        catch (EncoderFallbackException exception)
        {
            throw Error(command, lineNumber, "数据无法使用当前 CPCL 编码表示。", exception);
        }
    }

    private static string GetCommand(string line)
    {
        var value = line.Trim();
        var separator = value.IndexOf(' ');
        return separator < 0 ? value : value.Substring(0, separator);
    }

    private static string[] Fields(string line)
    {
        return line.Trim().Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries);
    }

    private static string[] Prefix(string line, int count, out string tail)
    {
        var value = line.TrimStart();
        var fields = new List<string>();
        var cursor = 0;
        while (fields.Count < count)
        {
            while (cursor < value.Length && value[cursor] == ' ')
                cursor++;
            if (cursor >= value.Length)
                break;
            var start = cursor;
            while (cursor < value.Length && value[cursor] != ' ')
                cursor++;
            fields.Add(value.Substring(start, cursor - start));
        }
        while (cursor < value.Length && value[cursor] == ' ')
            cursor++;
        tail = cursor < value.Length ? value.Substring(cursor) : string.Empty;
        return fields.ToArray();
    }

    private static void RequireExactFields(string line, int count, string command, int lineNumber)
    {
        if (Fields(line).Length != count)
            throw Error(command, lineNumber, "命令参数数量不正确。");
    }

    private static void RequireNonNegative(string[] fields, string command, int lineNumber, int count)
    {
        if (fields.Length != count)
            throw Error(command, lineNumber, "命令参数数量不正确。");
        for (var i = 1; i < fields.Length; i++)
            ParseNonNegativeInt(fields[i], command, lineNumber);
    }

    private static void RequireRange(string[] fields, string command, int lineNumber, int count,
        int minimum, int maximum, int? secondMinimum = null)
    {
        if (fields.Length != count)
            throw Error(command, lineNumber, "命令参数数量不正确。");
        for (var i = 1; i < fields.Length; i++)
        {
            var value = ParseInt(fields[i], command, lineNumber);
            var min = i == 2 && secondMinimum.HasValue ? secondMinimum.Value : minimum;
            if (value < min || value > maximum)
                throw Error(command, lineNumber, "命令参数超出受限范围。");
        }
    }

    private static void ParseToken(string value, string command, int lineNumber)
    {
        if (string.IsNullOrEmpty(value))
            throw Error(command, lineNumber, "参数不能为空。");
        foreach (var character in value)
            if (char.IsWhiteSpace(character) || char.IsControl(character))
                throw Error(command, lineNumber, "参数必须是单个可打印标记。");
    }

    private static int ParsePositiveInt(string value, string command, int lineNumber)
    {
        var result = ParseInt(value, command, lineNumber);
        if (result <= 0)
            throw Error(command, lineNumber, "参数必须为正整数。");
        return result;
    }

    private static int ParseNonNegativeInt(string value, string command, int lineNumber)
    {
        var result = ParseInt(value, command, lineNumber);
        if (result < 0)
            throw Error(command, lineNumber, "参数不能为负数。");
        return result;
    }

    private static int ParseInt(string value, string command, int lineNumber)
    {
        int result;
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
            throw Error(command, lineNumber, "参数必须为整数。");
        return result;
    }

    private static bool IsHex(char value)
    {
        return (value >= '0' && value <= '9') || (value >= 'a' && value <= 'f') ||
               (value >= 'A' && value <= 'F');
    }

    internal static ArgumentException Error(string command, int lineNumber, string reason, Exception inner = null)
    {
        return new ArgumentException(
            "CPCL 受限 Raw 命令 " + command + " 第 " + lineNumber + " 行校验失败：" + reason,
            "raw", inner);
    }
}

/// <summary>
/// CPCL 受限原始命令的页面几何状态。
/// </summary>
internal sealed class CpclGeometryState
{
    private readonly CpclRestrictedFormatState _formatState;
    private PrintOrientation _orientation;

    internal int Offset { get; private set; }
    internal int Width { get; private set; }
    internal int Height { get; private set; }
    internal int Quantity { get; private set; }
    internal int HorizontalResolution { get; private set; } = 200;
    internal int VerticalResolution { get; private set; } = 200;
    internal bool HasWidth { get; private set; }
    internal bool HasHeight { get; private set; }
    internal bool PrintSeen { get; set; }
    internal bool DrawSeen => _formatState.HasDrawn;
    internal int HorizontalScale => _formatState.HorizontalScale;
    internal int VerticalScale => _formatState.VerticalScale;
    internal int BarcodeTextCell => _formatState.BarcodeTextCell;
    internal int BarcodeTextHeight => checked(_formatState.BarcodeTextCell * _formatState.VerticalScale +
        _formatState.BarcodeTextOffset);
    internal int CharacterSpacing => _formatState.CharacterSpacing;
    internal int OrientationValue => (int)_orientation;
    internal CpclRestrictedFormatState FormatState => _formatState.Clone();

    internal CpclGeometryState(CPCLCommandInfo commandInfo, CpclRestrictedFormatState formatState)
    {
        _formatState = formatState?.Clone() ?? throw new ArgumentNullException(nameof(formatState));
        Offset = commandInfo.Offset;
        Width = commandInfo.Width;
        Height = commandInfo.Height;
        Quantity = commandInfo.Qty < 1 ? 1 : commandInfo.Qty;
        HasWidth = Width > 0;
        HasHeight = Height > 0;
        _orientation = (PrintOrientation)commandInfo.PagerRotate;
    }

    internal void SetHeader(int offset, int horizontalResolution, int verticalResolution, int height, int quantity)
    {
        Offset = offset;
        HorizontalResolution = horizontalResolution;
        VerticalResolution = verticalResolution;
        Height = height;
        Quantity = quantity;
        _orientation = PrintOrientation.None;
        HasHeight = true;
        _formatState.Reset();
    }

    internal void SetWidth(int width)
    {
        Width = width;
        HasWidth = true;
    }

    internal void SetTextScale(int horizontal, int vertical)
    {
        _formatState.SetMagnification(horizontal, vertical);
    }

    internal void SetBarcodeText(bool enabled, int fontCell, int offset)
    {
        _formatState.SetBarcodeText(enabled, fontCell, offset);
    }

    internal void SetAlignment(CpclFieldAlignment alignment, int? end)
        => _formatState.SetAlignment(alignment, end);

    internal void SetCharacterSpacing(int spacing)
    {
        _formatState.CharacterSpacing = spacing;
        _formatState.SpacingKnown = true;
    }

    internal void SetRotation(int rotation, int lineNumber)
    {
        if (_formatState.HasDrawn)
            throw CpclRestrictedRawParser.Error("R", lineNumber,
                "R 必须位于绘制命令之前，才能验证整页方向。");
        _orientation = rotation switch
        {
            0 => PrintOrientation.None,
            90 => PrintOrientation.Left,
            180 => PrintOrientation.Down,
            270 => PrintOrientation.Right,
            _ => throw new ArgumentOutOfRangeException(nameof(rotation))
        };
        _formatState.RotationKnown = true;
    }

    internal int ResolveAlignedX(int x, long width, int rotation, string command, int lineNumber)
    {
        if (!HasWidth)
            throw CpclRestrictedRawParser.Error(command, lineNumber,
                "文字对齐需要先明确页面宽度。");
        if (!_formatState.AlignmentKnown)
            throw CpclRestrictedRawParser.Error(command, lineNumber,
                "前序原始命令可能更改字段对齐；受限 Raw 必须先声明 LEFT、CENTER 或 RIGHT。");
        if (rotation != 0 && _formatState.Alignment != CpclFieldAlignment.Left)
            throw CpclRestrictedRawParser.Error(command, lineNumber,
                "旋转文本与 CENTER/RIGHT 的字段范围无法可靠估算。");
        var end = _formatState.AlignmentEnd ?? Width;
        if (_formatState.Alignment != CpclFieldAlignment.Left &&
            (end < x || width > (long)end - x))
            throw CpclRestrictedRawParser.Error(command, lineNumber,
                "CENTER/RIGHT 文本超出声明的字段范围。");
        var aligned = _formatState.Alignment switch
        {
            CpclFieldAlignment.Left => (long)x,
            CpclFieldAlignment.Center => x + ((long)end - x - width) / 2,
            CpclFieldAlignment.Right => (long)end - width,
            _ => throw new ArgumentOutOfRangeException()
        };
        if (aligned < 0 || aligned > int.MaxValue)
            throw CpclRestrictedRawParser.Error(command, lineNumber,
                "按当前字段对齐设置计算出的横坐标超出可验证范围。");
        return (int)aligned;
    }

    internal void RequireTextState(string command, int lineNumber, bool barcode = false)
    {
        if (!_formatState.MagnificationKnown)
            throw CpclRestrictedRawParser.Error(command, lineNumber,
                "前序原始命令可能更改 SETMAG；受限 Raw 必须先声明有效 SETMAG。");
        if (!_formatState.SpacingKnown)
            throw CpclRestrictedRawParser.Error(command, lineNumber,
                "前序原始命令可能更改字符间距；受限 Raw 必须先声明 SETSP。");
        if (!_formatState.AlignmentKnown)
            throw CpclRestrictedRawParser.Error(command, lineNumber,
                "前序原始命令可能更改字段对齐；受限 Raw 必须先声明 LEFT、CENTER 或 RIGHT。");
        if (barcode && !_formatState.BarcodeTextKnown)
            throw CpclRestrictedRawParser.Error(command, lineNumber,
                "前序原始命令可能更改 BT；受限 Raw 必须先声明 BT 或 BT OFF。");
    }

    internal void RequireExtent(int x, int y, long width, long height, int rotation,
        string command, int lineNumber, bool estimated)
    {
        if (!HasWidth || !HasHeight)
            throw CpclRestrictedRawParser.Error(command, lineNumber,
                "绘制命令需要先明确页面宽度和任务高度。");
        if (!_formatState.RotationKnown)
            throw CpclRestrictedRawParser.Error(command, lineNumber,
                "前序原始命令可能更改页面旋转；受限 Raw 必须先声明 R。");
        if (x < 0 || y < 0)
            throw CpclRestrictedRawParser.Error(command, lineNumber, "绘制坐标不能为负数。");
        try
        {
            PrintCommandPageBounds.ValidateExtent(Width, Height, x, y, width, height, rotation,
                command, estimated, Offset, 0, _orientation);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw CpclRestrictedRawParser.Error(command, lineNumber, exception.Message, exception);
        }
        _formatState.HasDrawn = true;
    }

    internal void RequireRectangle(int x0, int y0, int x1, int y1, int lineWidth,
        string command, int lineNumber, bool estimated = false, bool expandStroke = true)
    {
        if (!HasWidth || !HasHeight)
            throw CpclRestrictedRawParser.Error(command, lineNumber,
                "绘制命令需要先明确页面宽度和任务高度。");
        if (lineWidth < 0 || x1 < x0 || y1 < y0)
            throw CpclRestrictedRawParser.Error(command, lineNumber, "绘制范围或线宽参数无效。");
        try
        {
            PrintCommandPageBounds.ValidateEdges(Width, Height,
                x0, y0,
                (long)x1 + (expandStroke ? lineWidth : 0),
                (long)y1 + (expandStroke ? lineWidth : 0),
                command, estimated, Offset, 0, _orientation);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw CpclRestrictedRawParser.Error(command, lineNumber, exception.Message, exception);
        }
        _formatState.HasDrawn = true;
    }
}
