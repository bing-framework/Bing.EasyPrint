using System;
using System.Collections.Generic;
using System.Text;
using Bing.EasyPrint.Printing;

namespace Bing.EasyPrint.Protocols;

public sealed partial class ZplPrintCommand : IRestrictedRawPrintCommand<ZplPrintCommand>
{
    private readonly struct ZplRawToken
    {
        internal ZplRawToken(string code, string value, int offset)
        {
            Code = code;
            Value = value;
            Offset = offset;
        }

        internal string Code { get; }
        internal string Value { get; }
        internal int Offset { get; }
    }

    /// <summary>完整校验 ZPL 控制命令与字段边界，并按原文写入。</summary>
    public ZplPrintCommand WriteRestrictedRaw(string raw)
    {
        if (raw == null) throw new ArgumentNullException(nameof(raw));
        if (raw.Length == 0) throw ZplRawError("<空批次>", 0, "批次不能为空。");
        try { ValidateEncodable(raw, nameof(raw)); }
        catch (ArgumentException exception)
        {
            throw ZplRawError("<文本编码>", 0, exception.Message, exception);
        }

        using var tokenizerReservation = RestrictedRawSyntax.ReserveTokenizerWorkingSet(
            raw, "Zpl.RestrictedRawTokenizer");
        var tokens = TokenizeZpl(raw);
        var candidate = (ZplPrintCommand)MemberwiseClone();
        var controlledClose = false;
        var closedDelayedCut = false;
        using (PrintResourceScope.BeginTemporary())
        {
            using (candidate.SuppressActionRecording())
            {
                for (var index = 0; index < tokens.Count; index++)
                {
                    var token = tokens[index];
                    try
                    {
                        switch (token.Code)
                        {
                            case "XA":
                                RequireEmpty(token);
                                if (candidate._formatOpen) throw new InvalidOperationException("当前格式必须先用 ^XZ 完整闭合。");
                                if (controlledClose) throw new InvalidOperationException("单批受控 Raw 只允许闭合一个完整格式。");
                                candidate.Start();
                                break;
                            case "XZ":
                                RequireEmpty(token);
                                if (!candidate._formatOpen) throw new InvalidOperationException("^XZ 前必须有 ^XA。");
                                if (candidate._cutter == null) candidate.End();
                                else
                                {
                                    if (!candidate._formatOpen) throw new InvalidOperationException("^XZ 前必须有 ^XA。");
                                    if (!candidate._formatPrinted) throw new InvalidOperationException("受控切刀格式在 ^XZ 前必须声明 ^PQ 打印数量。");
                                    candidate.ValidateRestrictedFormatClose();
                                    candidate._formatOpen = false;
                                    candidate._formatPrinted = true;
                                    controlledClose = true;
                                    closedDelayedCut = candidate._delayedCut;
                                }
                                break;
                            case "PW":
                                candidate.SetRestrictedLabelWidth(ParsePositive(token));
                                break;
                            case "LL":
                                candidate.SetRestrictedLabelHeight(ParsePositive(token));
                                break;
                            case "CI":
                                candidate.ValidateRestrictedCharacterSet(token.Value);
                                break;
                            case "PR":
                                if (RestrictedRawSyntax.ParseInt(token.Value.Trim(), "^PR") <= 0)
                                    throw new ArgumentOutOfRangeException(nameof(token), "^PR 必须为正数。");
                                break;
                            case "PO":
                                if (token.Value.Trim() != "N" && token.Value.Trim() != "I")
                                    throw new NotSupportedException("^PO 只允许 N 或 I 方向。");
                                candidate._restrictedOrientation = token.Value.Trim() == "I"
                                    ? PrintOrientation.Down : PrintOrientation.None;
                                break;
                            case "MN":
                                if (!IsMediaTracking(token.Value.Trim()))
                                    throw new ArgumentException("^MN 介质方式不在受限白名单内。");
                                break;
                            case "MD":
                                var darkness = RestrictedRawSyntax.ParseInt(token.Value.Trim(), "^MD");
                                if (darkness < -30 || darkness > 30) throw new ArgumentOutOfRangeException(nameof(token), "^MD 范围为 -30 到 30。");
                                break;
                            case "PQ":
                                candidate.RequireRestrictedFormat("^PQ");
                                candidate.ValidateRestrictedPrintQuantity(token.Value);
                                break;
                            case "MM":
                                candidate.ValidateRestrictedCutMode(token.Value.Trim());
                                break;
                            case "KV":
                                candidate.ValidateRestrictedKioskCut(token.Value);
                                break;
                            case "CN":
                                candidate.ValidateRestrictedCut(token.Value.Trim());
                                break;
                            case "FO":
                                candidate.RequireRestrictedFormat("^FO");
                                candidate.ValidateRestrictedLabelSize("^FO");
                                candidate.ParseRestrictedField(tokens, ref index);
                                break;
                            default:
                                throw new NotSupportedException("未知命令、状态查询、下载或非白名单 ZPL 命令。");
                        }
                    }
                    catch (Exception exception) when (IsRawValidationException(exception))
                    {
                        throw ZplRawError("^" + token.Code, token.Offset, exception.Message, exception, raw);
                    }
                }
            }
        }

        if (controlledClose)
        {
            var boundaryEnd = checked(RecordedActionCount + 1);
            WriteRawWithCommit(raw, () => _outputBoundaries.AddFactory(() => (boundaryEnd, closedDelayedCut)));
        }
        else base.WriteRaw(raw);
        CopyRestrictedRawState(candidate);
        return this;
    }

    private List<ZplRawToken> TokenizeZpl(string raw)
    {
        var tokens = new List<ZplRawToken>();
        var offset = 0;
        while (offset < raw.Length)
        {
            if (char.IsWhiteSpace(raw[offset])) { offset++; continue; }
            if (raw[offset] == '~')
                throw ZplRawError("~", offset, "tilde 命令、状态查询与控制文件不属于受限文本子集。", raw: raw);
            if (raw[offset] != '^' || offset + 2 >= raw.Length)
                throw ZplRawError("<文本>", offset, "ZPL 命令必须以 ^ 和两个 ASCII 命令字符开始。", raw: raw);
            if (!IsAsciiLetter(raw[offset + 1]) || !IsAsciiLetterOrDigit(raw[offset + 2]))
                throw ZplRawError("^?", offset, "命令前缀后必须是合法的 ASCII 命令码。", raw: raw);

            var code = raw.Substring(offset + 1, 2).ToUpperInvariant();
            var start = offset + 3;
            var end = start;
            while (end < raw.Length && raw[end] != '^' && raw[end] != '~') end++;
            if (end < raw.Length && raw[end] == '~')
                throw ZplRawError("~", end, "字段或控制流中不允许夹带 tilde 命令。", raw: raw);
            tokens.Add(new ZplRawToken(code, raw.Substring(start, end - start), offset));
            offset = end;
        }
        return tokens;
    }

    private void ParseRestrictedField(IReadOnlyList<ZplRawToken> tokens, ref int index)
    {
        var origin = ParsePair(tokens[index].Value, "^FO");
        var fieldStart = index;
        var cursor = index + 1;
        if (cursor >= tokens.Count) throw new FormatException("^FO 字段没有完整的绘制命令。");

        if (tokens[cursor].Code.StartsWith("A", StringComparison.Ordinal))
        {
            var font = tokens[cursor].Code.Substring(1);
            var arguments = RestrictedRawSyntax.ParseDelimited(tokens[cursor].Value, 3);
            if (arguments[0].Length != 1) throw new FormatException("^A 字段必须含单个方向字符。");
            var rotation = ParseOrientation(arguments[0][0]);
            cursor++;
            var data = ReadFieldData(tokens, ref cursor);
            RequireToken(tokens, ref cursor, "FS");
            RequireFieldEnd(cursor, fieldStart);
            Text(origin.X, origin.Y, data,
                RestrictedRawSyntax.ParseInt(arguments[1], "^A 字高"),
                RestrictedRawSyntax.ParseInt(arguments[2], "^A 字宽"), font, rotation);
            index = cursor - 1;
            return;
        }

        if (tokens[cursor].Code == "GB")
        {
            var arguments = RestrictedRawSyntax.ParseDelimited(tokens[cursor].Value.Trim(), tokens[cursor].Value.Contains(",B,") ? 5 : 3);
            var width = RestrictedRawSyntax.ParseInt(arguments[0], "^GB 宽度");
            var height = RestrictedRawSyntax.ParseInt(arguments[1], "^GB 高度");
            var thickness = RestrictedRawSyntax.ParseInt(arguments[2], "^GB 线宽");
            cursor++;
            RequireToken(tokens, ref cursor, "FS");
            RequireFieldEnd(cursor, fieldStart);
            if (arguments.Length == 5)
            {
                if (arguments[3] != "B" || arguments[4] != "0" || width <= 0 || height <= 0)
                    throw new ArgumentException("^GB 填充矩形参数不受支持。");
                FillRectangle(origin.X, origin.Y, width, height);
            }
            else if (width == thickness)
                Line(origin.X, origin.Y, origin.X, checked(origin.Y + height - 1), thickness);
            else if (height == thickness)
                Line(origin.X, origin.Y, checked(origin.X + width - 1), origin.Y, thickness);
            else
                Box(origin.X, origin.Y, checked(origin.X + width), checked(origin.Y + height), thickness);
            index = cursor - 1;
            return;
        }

        if (tokens[cursor].Code == "BY")
        {
            var moduleWidth = ParsePositive(tokens[cursor]);
            cursor++;
            if (cursor >= tokens.Count) throw new FormatException("^BY 后缺少完整条码字段。");
            var barcode = tokens[cursor++];
            var data = ReadFieldData(tokens, ref cursor);
            RequireToken(tokens, ref cursor, "FS");
            RequireFieldEnd(cursor, fieldStart);
            ValidateRestrictedBarcode(barcode, origin.X, origin.Y, moduleWidth, data);
            index = cursor - 1;
            return;
        }

        if (tokens[cursor].Code == "BR")
        {
            var barcode = tokens[cursor++];
            var data = ReadFieldData(tokens, ref cursor);
            RequireToken(tokens, ref cursor, "FS");
            RequireFieldEnd(cursor, fieldStart);
            ValidateRestrictedBarcode(barcode, origin.X, origin.Y, 0, data);
            index = cursor - 1;
            return;
        }

        if (tokens[cursor].Code == "BQ")
        {
            var arguments = RestrictedRawSyntax.ParseDelimited(tokens[cursor].Value.Trim(), 3);
            var rotation = ParseOrientation(arguments[0].Length == 0 ? 'N' : arguments[0][0]);
            if (arguments[1] != "2") throw new NotSupportedException("受限 ^BQ 只允许标准 Model 2。");
            cursor++;
            var data = ReadFieldData(tokens, ref cursor);
            if (data.Length < 3 || data[1] != 'A' || data[2] != ',')
                throw new FormatException("^BQ ^FD 必须使用纠错级别和 A 数据模式。");
            QrCode(origin.X, origin.Y, data.Substring(3),
                RestrictedRawSyntax.ParseInt(arguments[2], "^BQ 放大倍数"), data[0], rotation);
            RequireToken(tokens, ref cursor, "FS");
            RequireFieldEnd(cursor, fieldStart);
            index = cursor - 1;
            return;
        }

        if (tokens[cursor].Code == "GF")
        {
            var graphic = tokens[cursor++];
            if (graphic.Value.Length < 2 || char.ToUpperInvariant(graphic.Value[0]) != 'A' || graphic.Value[1] != ',')
                throw new NotSupportedException("受限图形只允许 ASCII 十六进制 ^GFA。");
            var comma = graphic.Value.LastIndexOf(',');
            if (comma < 0) throw new FormatException("^GFA 缺少行宽或图像数据。");
            var header = RestrictedRawSyntax.ParseDelimited(graphic.Value.Substring(2, comma - 2), 3);
            var hex = graphic.Value.Substring(comma + 1);
            if (!RestrictedRawSyntax.IsHex(hex)) throw new ArgumentException("^GFA 数据必须为 ASCII 十六进制。");
            var total = RestrictedRawSyntax.ParseInt(header[0], "^GFA 总字节数");
            if (RestrictedRawSyntax.ParseInt(header[1], "^GFA 未压缩字节数") != total)
                throw new ArgumentException("受限 ^GFA 只允许压缩字节数与总字节数一致的 ASCII 数据。");
            var rowBytes = RestrictedRawSyntax.ParseInt(header[2], "^GFA 行字节数");
            RequireToken(tokens, ref cursor, "FS");
            RequireFieldEnd(cursor, fieldStart);
            GraphicField(origin.X, origin.Y, total, rowBytes, hex);
            index = cursor - 1;
            return;
        }

        throw new NotSupportedException("^FO 后只允许受支持的文字、线框、条码、二维码或 ^GFA 字段。");
    }

    private void ValidateRestrictedBarcode(ZplRawToken token, int x, int y, int moduleWidth, string data)
    {
        var fields = RestrictedRawSyntax.ParseDelimited(token.Value.Trim(), 4, 7);
        var orientation = ParseOrientation(fields[0].Length == 0 ? 'N' : fields[0][0]);
        switch (token.Code)
        {
            case "BC":
                RequireBarcodeFields(fields, 5, token.Code);
                RequireBarcodeValue(fields[3], "N", token.Code);
                RequireBarcodeValue(fields[4], "N", token.Code);
                var code128Height = RestrictedRawSyntax.ParseInt(fields[1], "条码高度");
                var code128Hri = ParseBarcodeFlag(fields[2], token.Code);
                if (data.StartsWith(">;>8", StringComparison.Ordinal))
                    BarcodeUccEan128(x, y, DecodeZplGs1Data(data.Substring(4)), code128Height, code128Hri, moduleWidth, orientation);
                else Barcode(x, y, data, code128Height, code128Hri, moduleWidth, orientation);
                return;
            case "B3":
            case "B1":
            case "BP":
                RequireBarcodeFields(fields, 5, token.Code);
                RequireBarcodeValue(fields[1], "N", token.Code);
                RequireBarcodeValue(fields[4], "N", token.Code);
                var fixedHeight = RestrictedRawSyntax.ParseInt(fields[2], "条码高度");
                var fixedHri = ParseBarcodeFlag(fields[3], token.Code);
                if (token.Code == "B3") Barcode39(x, y, data, fixedHeight, fixedHri, moduleWidth, orientation);
                else if (token.Code == "B1") BarcodeCode11(x, y, data, fixedHeight, fixedHri, moduleWidth, orientation);
                else BarcodePlessey(x, y, data, fixedHeight, fixedHri, moduleWidth, orientation);
                return;
            case "B4":
                RequireBarcodeFields(fields, 4, token.Code);
                var heightMultiplier = RestrictedRawSyntax.ParseInt(fields[1], "Code 49 高度倍数");
                var actualHeight = checked(heightMultiplier * moduleWidth);
                var interpretation = fields[2] == "B" ? true : fields[2] == "N" ? false
                    : throw new FormatException("^B4 可读文字标记只允许 B 或 N。");
                if (fields[3].Length != 1) throw new FormatException("^B4 起始模式必须为一个字符。");
                BarcodeCode49(x, y, data, actualHeight, interpretation, moduleWidth, fields[3][0], orientation);
                return;
            case "BR":
                if (fields.Length != 5 && fields.Length != 6) throw new FormatException("^BR 字段数量无效。");
                var symbology = RestrictedRawSyntax.ParseInt(fields[1], "^BR 制式");
                var dataBarWidth = RestrictedRawSyntax.ParseInt(fields[2], "^BR 模块宽度");
                if (moduleWidth != 0 && dataBarWidth != moduleWidth)
                    throw new ArgumentException("^BY 与 ^BR 模块宽度必须一致。");
                RequireBarcodeValue(fields[3], "1", token.Code);
                var dataBarHeight = RestrictedRawSyntax.ParseInt(fields[4], "条码高度");
                if (symbology == 6)
                {
                    RequireBarcodeFields(fields, 6, token.Code);
                    var segmentWidth = RestrictedRawSyntax.ParseInt(fields[5], "^BR 分段宽度");
                    BarcodeGs1DataBarExpanded(x, y, data, dataBarHeight, dataBarWidth, segmentWidth, orientation);
                }
                else
                {
                    RequireBarcodeFields(fields, 5, token.Code);
                    if (symbology == 1) BarcodeGs1DataBarOmnidirectional(x, y, data, dataBarHeight, dataBarWidth, orientation);
                    else if (symbology == 2) BarcodeGs1DataBarTruncated(x, y, data, dataBarHeight, dataBarWidth, orientation);
                    else if (symbology == 5) BarcodeGs1DataBarLimited(x, y, data, dataBarHeight, dataBarWidth, orientation);
                    else throw new NotSupportedException("受限 ^BR 只允许 Omnidirectional、Truncated、Limited 或 Expanded。");
                }
                return;
            case "BA":
                RequireBarcodeFields(fields, 5, token.Code);
                RequireBarcodeValue(fields[3], "N", token.Code);
                Barcode93(x, y, data, RestrictedRawSyntax.ParseInt(fields[1], "条码高度"),
                    ParseBarcodeFlag(fields[2], token.Code), moduleWidth, ParseBarcodeFlag(fields[4], token.Code), orientation);
                return;
            case "B5":
                RequireBarcodeFields(fields, 4, token.Code);
                RequireBarcodeValue(fields[3], "N", token.Code);
                BarcodePlanet(x, y, data, RestrictedRawSyntax.ParseInt(fields[1], "条码高度"),
                    ParseBarcodeFlag(fields[2], token.Code), moduleWidth, orientation);
                return;
            case "BZ":
                if (fields.Length == 5)
                {
                    RequireBarcodeValue(fields[3], "N", token.Code);
                    if (fields[4] != "3") throw new NotSupportedException("^BZ 拓展类型只允许结构化 API 的 Intelligent Mail 类型 3。");
                    BarcodeIntelligentMail(x, y, data, RestrictedRawSyntax.ParseInt(fields[1], "条码高度"),
                        ParseBarcodeFlag(fields[2], token.Code), moduleWidth, orientation);
                }
                else
                {
                    RequireBarcodeFields(fields, 4, token.Code);
                    RequireBarcodeValue(fields[3], "N", token.Code);
                    BarcodePostnet(x, y, data, RestrictedRawSyntax.ParseInt(fields[1], "条码高度"),
                        ParseBarcodeFlag(fields[2], token.Code), moduleWidth, orientation);
                }
                return;
            case "B2":
                RequireBarcodeFields(fields, 5, token.Code);
                RequireBarcodeValue(fields[3], "N", token.Code);
                var i2of5Height = RestrictedRawSyntax.ParseInt(fields[1], "条码高度");
                var i2of5Hri = ParseBarcodeFlag(fields[2], token.Code);
                if (ParseBarcodeFlag(fields[4], token.Code))
                    BarcodeI2OF5C(x, y, data, i2of5Height, i2of5Hri, moduleWidth, orientation);
                else BarcodeI2OF5(x, y, data, i2of5Height, i2of5Hri, moduleWidth, orientation);
                return;
            case "B8":
            case "BE":
                RequireBarcodeFields(fields, 4, token.Code);
                RequireBarcodeValue(fields[3], "N", token.Code);
                var retailHeight = RestrictedRawSyntax.ParseInt(fields[1], "条码高度");
                var retailHri = ParseBarcodeFlag(fields[2], token.Code);
                if (token.Code == "B8") BarcodeEan8(x, y, data, retailHeight, retailHri, moduleWidth, orientation);
                else BarcodeEan13(x, y, data, retailHeight, retailHri, moduleWidth, orientation);
                return;
            case "BU":
            case "B9":
                RequireBarcodeFields(fields, 5, token.Code);
                RequireBarcodeValue(fields[3], "N", token.Code);
                var upcHeight = RestrictedRawSyntax.ParseInt(fields[1], "条码高度");
                var upcHri = ParseBarcodeFlag(fields[2], token.Code);
                var upcCheckDigit = ParseBarcodeFlag(fields[4], token.Code);
                if (token.Code == "BU") BarcodeUpcA(x, y, data, upcHeight, upcHri, moduleWidth, upcCheckDigit, orientation);
                else BarcodeUpcE(x, y, DecodeUpcEData(data), upcHeight, upcHri, moduleWidth, upcCheckDigit, orientation);
                return;
            case "BK":
                RequireBarcodeFields(fields, 7, token.Code);
                RequireBarcodeValue(fields[1], "N", token.Code);
                RequireBarcodeValue(fields[4], "N", token.Code);
                if (fields[5].Length != 1 || fields[6].Length != 1) throw new FormatException("^BK 起止符必须各为一个字符。");
                BarcodeCodabar(x, y, fields[5] + data + fields[6],
                    RestrictedRawSyntax.ParseInt(fields[2], "条码高度"), ParseBarcodeFlag(fields[3], token.Code),
                    moduleWidth, orientation);
                return;
            case "BM":
                RequireBarcodeFields(fields, 6, token.Code);
                RequireBarcodeValue(fields[4], "N", token.Code);
                if (fields[1].Length != 1) throw new FormatException("^BM 校验方案必须为一个字符。");
                BarcodeMsi(x, y, data, RestrictedRawSyntax.ParseInt(fields[2], "条码高度"),
                    ParseBarcodeFlag(fields[3], token.Code), moduleWidth, fields[1][0],
                    ParseBarcodeFlag(fields[5], token.Code), orientation);
                return;
            default: throw new NotSupportedException($"ZPL 条码命令 ^{token.Code} 不属于当前受限子集。");
        }
    }

    private static bool ParseBarcodeFlag(string value, string command)
        => value == "Y" ? true : value == "N" ? false
            : throw new FormatException($"^{command} 标志只允许 Y 或 N。");

    private static void RequireBarcodeFields(string[] fields, int expected, string command)
    {
        if (fields.Length != expected) throw new FormatException($"^{command} 参数字段数量必须为 {expected}。");
    }

    private static void RequireBarcodeValue(string value, string expected, string command)
    {
        if (value != expected) throw new NotSupportedException($"^{command} 该参数只能使用结构化 API 子集值 {expected}。");
    }

    private static string DecodeUpcEData(string expanded)
    {
        if (expanded.Length != 10) throw new ArgumentException("ZPL ^B9 数据必须是 10 位展开式 UPC-E 数据。");
        for (var index = 0; index < expanded.Length; index++)
            if (expanded[index] < '0' || expanded[index] > '9')
                throw new ArgumentException("ZPL ^B9 展开式数据只能包含 ASCII 数字。");

        var candidates = new List<string>();
        if (expanded.Substring(3, 4) == "0000")
            candidates.Add(new string(new[] { expanded[0], expanded[1], expanded[7], expanded[8], expanded[9], expanded[2] }));
        if (expanded.Substring(3, 5) == "00000")
            candidates.Add(new string(new[] { expanded[0], expanded[1], expanded[2], expanded[8], expanded[9], '3' }));
        if (expanded.Substring(4, 5) == "00000")
            candidates.Add(new string(new[] { expanded[0], expanded[1], expanded[2], expanded[3], expanded[9], '4' }));
        if (expanded.Substring(5, 4) == "0000")
            candidates.Add(expanded.Substring(0, 5) + expanded[9]);

        foreach (var candidate in candidates)
        {
            try
            {
                if (NormalizeUpcEData(candidate, nameof(expanded)) == expanded) return candidate;
            }
            catch (ArgumentException)
            {
            }
        }

        throw new ArgumentException("ZPL ^B9 数据不符合当前 UPC-E 压缩子集。");
    }

    private void ValidateRestrictedPrintQuantity(string value)
    {
        var fields = RestrictedRawSyntax.ParseDelimited(value.Trim(), value.Contains(",") ? 4 : 1);
        var quantity = RestrictedRawSyntax.ParseInt(fields[0], "^PQ 数量");
        if (_cutter == null)
        {
            if (fields.Length != 1)
                throw new NotSupportedException("未配置切刀 Profile 时只允许结构化 Print 生成的单参数 ^PQ。");
            Print(quantity);
            return;
        }

        var mode = GetRestrictedCutMode();
        if (mode == 'C')
        {
            if (fields.Length != 4 ||
                RestrictedRawSyntax.ParseInt(fields[1], "^PQ 每份数量") != quantity ||
                RestrictedRawSyntax.ParseInt(fields[2], "^PQ 重印数量") != 0 ||
                fields[3] != "Y")
                throw new NotSupportedException("Cutter 模式的 ^PQ 必须匹配结构化输出的 q,q,0,Y 参数。");
        }
        else if (fields.Length != 1)
            throw new NotSupportedException("当前 ZPL 裁切模式只允许单参数 ^PQ。");

        if (mode == 'K' && !_restrictedKioskUncutMillimeters.HasValue)
            throw new InvalidOperationException("Kiosk 模式的 ^PQ 前必须声明匹配的 ^KV 参数。");

        ZplCutEncoder.ValidateQuantity(quantity);
        RequireUnprintedFormat();
        _formatPrinted = true;
        _restrictedCutMode = mode;
        _delayedCut = mode == 'D';
    }

    private void ValidateRestrictedCutMode(string value)
    {
        if (_cutter == null) throw new NotSupportedException("^MM 切刀模式要求显式 ZPL Cutter Profile。");
        RequireRestrictedFormat("^MM");
        if (_formatPrinted) throw new InvalidOperationException("^PQ 后不能更改当前格式的裁切模式。");
        var mode = value.Trim();
        var expected = _cutter.Strategy == ZplCutStrategy.Cutter ? "C"
            : _cutter.Strategy == ZplCutStrategy.Delayed ? "D" : "K";
        if (mode != expected && !(mode == "T" && _cutter.SupportsTearOff))
            throw new NotSupportedException("^MM 模式与已声明的切刀 Profile 不一致。");
        _restrictedCutMode = mode[0];
        _delayedCut = mode == "D";
        _restrictedKioskUncutMillimeters = null;
        _restrictedKioskCutMarginMillimeters = null;
        _restrictedKioskCutCommandSeen = false;
    }

    private void ValidateRestrictedKioskCut(string value)
    {
        if (_cutter?.Strategy != ZplCutStrategy.Kiosk) throw new NotSupportedException("^KV 只允许用于 Kiosk Cutter Profile。");
        RequireRestrictedFormat("^KV");
        if (_formatPrinted) throw new InvalidOperationException("^PQ 后不能更改 Kiosk 裁切参数。");
        if (GetRestrictedCutMode() != 'K') throw new NotSupportedException("^KV 只允许用于当前格式的 Kiosk 模式。");
        if (_restrictedKioskUncutMillimeters.HasValue)
            throw new InvalidOperationException("每个 Kiosk 格式只能声明一次 ^KV。");

        var fields = RestrictedRawSyntax.ParseDelimited(value, 2);
        var uncut = RestrictedRawSyntax.ParseInt(fields[0], "^KV 未切长度");
        var margin = RestrictedRawSyntax.ParseInt(fields[1], "^KV 边距");
        var supportsFull = Array.IndexOf(_cutter.SupportedCutModes, PrintCutMode.Full) >= 0 && uncut == 0;
        var supportsPartial = Array.IndexOf(_cutter.SupportedCutModes, PrintCutMode.Partial) >= 0 &&
            uncut == _cutter.KioskPartialUncutMillimeters.GetValueOrDefault();
        if (margin != _cutter.KioskCutMarginMillimeters.GetValueOrDefault() ||
            (!supportsFull && !supportsPartial))
            throw new NotSupportedException("^KV 值必须与当前 Kiosk Profile 完全一致。");

        _restrictedKioskUncutMillimeters = uncut;
        _restrictedKioskCutMarginMillimeters = margin;
        _restrictedKioskCutCommandSeen = false;
    }

    private void ValidateRestrictedCut(string value)
    {
        if (_cutter == null)
        {
            if (value.Length != 0) throw new FormatException("未配置切刀 Profile 时 ^CN 不接受参数。");
            Cut();
            return;
        }
        if (_cutter.Strategy != ZplCutStrategy.Kiosk)
            throw new NotSupportedException("^CN 只允许用于已声明的 Kiosk Cutter Profile。");
        RequireRestrictedFormat("^CN");
        if (!_formatPrinted) throw new InvalidOperationException("Kiosk 模式的 ^CN 前必须声明 ^PQ 打印数量。");
        if (GetRestrictedCutMode() != 'K') throw new NotSupportedException("^CN 只允许用于当前格式的 Kiosk 模式。");
        if (_restrictedKioskCutCommandSeen) throw new InvalidOperationException("每个 Kiosk 格式只能声明一次 ^CN。");
        if (value != "0" && value != "1") throw new ArgumentException("^CN 只允许 0 或 1。");
        var mode = value == "1" ? PrintCutMode.Full : PrintCutMode.Partial;
        ZplCutEncoder.Validate(new PrintCutOptions { Mode = mode }, _cutter);
        var expectedUncut = mode == PrintCutMode.Full
            ? 0
            : _cutter.KioskPartialUncutMillimeters.GetValueOrDefault();
        if (!_restrictedKioskUncutMillimeters.HasValue ||
            _restrictedKioskUncutMillimeters.Value != expectedUncut ||
            _restrictedKioskCutMarginMillimeters != _cutter.KioskCutMarginMillimeters)
            throw new InvalidOperationException("^CN 的全切/半切模式必须与前置 ^KV 参数一致。");
        _restrictedKioskCutCommandSeen = true;
        _delayedCut = false;
    }

    private char? GetRestrictedCutMode()
    {
        if (_restrictedCutMode.HasValue) return _restrictedCutMode;
        return _cutter?.Strategy switch
        {
            ZplCutStrategy.Cutter => 'C',
            ZplCutStrategy.Delayed => 'D',
            ZplCutStrategy.Kiosk => 'K',
            _ => null
        };
    }

    private void ValidateRestrictedFormatClose()
    {
        if (_cutter == null || !_formatPrinted) return;
        if (GetRestrictedCutMode() == 'K' && !_restrictedKioskCutCommandSeen)
            throw new InvalidOperationException("Kiosk 模式的受控格式必须在 ^XZ 前完成匹配的 ^CN 裁切命令。");
    }

    private void SetRestrictedLabelWidth(int width)
    {
        _profile?.ValidatePage(width, _hasLabelSize ? _labelHeight : 1);
        _labelWidth = width;
        _hasLabelSize = _labelHeight > 0;
    }

    private void SetRestrictedLabelHeight(int height)
    {
        _profile?.ValidatePage(_hasLabelSize ? _labelWidth : 1, height);
        _labelHeight = height;
        _hasLabelSize = _labelWidth > 0;
    }

    private void ValidateRestrictedCharacterSet(string value)
    {
        var characterSet = value.Trim();
        if (Encoding.CodePage == Encoding.UTF8.CodePage && characterSet == "28") return;
        if (Encoding.CodePage == Encoding.ASCII.CodePage && (characterSet == "0" || characterSet == "28")) return;
        throw new NotSupportedException("^CI 字符集必须与当前 UTF-8 或 ASCII 编码匹配。");
    }

    private void RequireRestrictedFormat(string command)
    {
        if (!_formatOpen) throw new InvalidOperationException($"{command} 绘制字段必须位于 ^XA 与 ^XZ 之间。");
    }

    private void ValidateRestrictedLabelSize(string command)
    {
        if (!_hasLabelSize) throw new InvalidOperationException($"{command} 绘制字段前必须先声明 ^PW 和 ^LL。");
    }

    private static int ParsePositive(ZplRawToken token)
    {
        var value = RestrictedRawSyntax.ParseInt(token.Value.Trim(), "^" + token.Code);
        if (value <= 0) throw new ArgumentOutOfRangeException(nameof(token), "页面尺寸必须为正数。");
        return value;
    }

    private static (int X, int Y) ParsePair(string value, string command)
    {
        var fields = RestrictedRawSyntax.ParseDelimited(value, 2);
        return (RestrictedRawSyntax.ParseInt(fields[0], command + " x"),
            RestrictedRawSyntax.ParseInt(fields[1], command + " y"));
    }

    private static int ParseOrientation(char value)
    {
        switch (char.ToUpperInvariant(value))
        {
            case 'N': return 0;
            case 'R': return 90;
            case 'I': return 180;
            case 'B': return 270;
            default: throw new ArgumentOutOfRangeException(nameof(value), "旋转方向只允许 N、R、I 或 B。");
        }
    }

    private static bool IsMediaTracking(string value)
        => value == "N" || value == "Y" || value == "W" || value == "M" || value == "A" || value == "V";

    private static bool IsAsciiLetter(char value)
        => value >= 'A' && value <= 'Z' || value >= 'a' && value <= 'z';

    private static bool IsAsciiLetterOrDigit(char value)
        => IsAsciiLetter(value) || value >= '0' && value <= '9';

    private static string DecodeZplGs1Data(string data)
    {
        var result = new StringBuilder(data.Length);
        for (var index = 0; index < data.Length; index++)
        {
            if (data[index] != '>') { result.Append(data[index]); continue; }
            if (index + 1 >= data.Length) throw new ArgumentException("GS1-128 数据含不完整的 ZPL 调用码。");
            var code = data[++index];
            if (code == '8') result.Append('\u001D');
            else if (code == '0') result.Append('>');
            else throw new ArgumentException("GS1-128 数据含未知的 ZPL 调用码。");
        }
        return result.ToString();
    }

    private static void RequireToken(IReadOnlyList<ZplRawToken> tokens, ref int cursor, string code)
    {
        if (cursor >= tokens.Count || tokens[cursor].Code != code)
        {
            var actual = cursor >= tokens.Count ? "输入结束" : "^" + tokens[cursor].Code;
            throw new FormatException($"字段缺少闭合 ^{code}；当前位置为 {actual}。");
        }
        if (tokens[cursor].Value.Trim().Length != 0) throw new FormatException($"^{code} 不接受字段数据。");
        cursor++;
    }

    private static string ReadFieldData(IReadOnlyList<ZplRawToken> tokens, ref int cursor)
    {
        if (cursor >= tokens.Count || tokens[cursor].Code != "FD")
            throw new FormatException("绘制字段必须包含 ^FD 数据。");
        return tokens[cursor++].Value;
    }

    private static void RequireFieldEnd(int cursor, int start)
    {
        if (cursor - start < 3 || cursor - start > 5)
            throw new FormatException("一个字段只能包含一条绘制命令和一个 ^FS 结束符。");
    }

    private static void RequireEmpty(ZplRawToken token)
    {
        if (token.Value.Trim().Length != 0) throw new FormatException($"^{token.Code} 不接受参数。");
    }

    private void CopyRestrictedRawState(ZplPrintCommand source)
    {
        _formatOpen = source._formatOpen;
        _formatPrinted = source._formatPrinted;
        _delayedCut = source._delayedCut;
        _restrictedCutMode = source._restrictedCutMode;
        _restrictedKioskUncutMillimeters = source._restrictedKioskUncutMillimeters;
        _restrictedKioskCutMarginMillimeters = source._restrictedKioskCutMarginMillimeters;
        _restrictedKioskCutCommandSeen = source._restrictedKioskCutCommandSeen;
        _labelWidth = source._labelWidth;
        _labelHeight = source._labelHeight;
        _hasLabelSize = source._hasLabelSize;
        _restrictedOrientation = source._restrictedOrientation;
        _validatePageBounds = source._validatePageBounds;
    }

    private static bool IsRawValidationException(Exception exception)
        => exception is ArgumentException || exception is NotSupportedException ||
           exception is InvalidOperationException || exception is OverflowException ||
           exception is FormatException;

    private ArgumentException ZplRawError(string command, int charOffset, string reason,
        Exception inner = null, string raw = null)
    {
        var input = raw;
        var byteOffset = input == null ? charOffset : RestrictedRawSyntax.GetByteOffset(Encoding, input, charOffset);
        return new ArgumentException($"{Protocol} 受限 Raw 命令 '{command}' 字节偏移 {byteOffset} 验证失败：{reason}",
            "raw", inner);
    }

}
