using System;
using System.Text;
using Bing.EasyPrint.Printing;

namespace Bing.EasyPrint.Protocols;

public sealed partial class EscPosPrintCommand : IRestrictedRawPrintCommand<EscPosPrintCommand>
{
    /// <summary>完整扫描 ESC/POS Native 文本命令，并按原文写入。</summary>
    public EscPosPrintCommand WriteRestrictedRaw(string raw)
    {
        if (raw == null) throw new ArgumentNullException(nameof(raw));
        if (raw.Length == 0) throw RawError("<空批次>", 0, "批次不能为空。");
        if (LayoutMode != EscPosLayoutMode.Native)
            throw RawError("<布局模式>", 0, "受限文本命令只适用于 ESC/POS Native；Raster 二进制输出不属于该语法子集。");
        if (_activeRasterPage != null)
            throw RawError("<RasterPage>", 0, "活动光栅页必须先结束。");
        try { ValidateEncodable(raw, nameof(raw)); }
        catch (ArgumentException exception)
        {
            throw RawError("<文本编码>", 0, exception.Message, exception);
        }

        using var tokenizerReservation = RestrictedRawSyntax.ReserveTokenizerWorkingSet(
            raw, "EscPos.RestrictedRawTokenizer");
        var candidate = (EscPosPrintCommand)MemberwiseClone();
        using (PrintResourceScope.BeginTemporary())
        {
            var bytes = GetEncodedBytes(raw, nameof(raw));
            using (candidate.SuppressActionRecording())
                candidate.ParseRestrictedBytes(bytes);
        }

        base.WriteRaw(raw);
        CopyRestrictedRawState(candidate);
        return this;
    }

    private void ParseRestrictedBytes(byte[] bytes)
    {
        var offset = 0;
        while (offset < bytes.Length)
        {
            var commandOffset = offset;
            var command = DescribeByte(bytes[offset]);
            try
            {
                var value = bytes[offset];
                if (value == 0x0A)
                {
                    NewLine();
                    offset++;
                    continue;
                }
                if (value == 0x0D)
                {
                    if (offset + 1 >= bytes.Length || bytes[offset + 1] != 0x0A)
                        throw new NotSupportedException("单独 CR 不属于当前结构化命令子集；只接受 CRLF 换行。");
                    NewLine();
                    offset += 2;
                    continue;
                }
                if (value == 0x09)
                {
                    RequireAvailableWidth("水平制表符");
                    if (GetRestrictedTabWidth() is not int tabWidth)
                        throw new NotSupportedException("水平制表符需要机型 Profile 或显式 NativeTabWidthDots 以估算制表位。");
                    Tab();
                    ValidateRestrictedLineWidth(0, "ESC/POS 水平制表符位置估算");
                    offset++;
                    continue;
                }
                if (value == 0x1B || value == 0x1D || value == 0x1C)
                {
                    ParseControl(bytes, ref offset);
                    continue;
                }
                if (value < 0x20 || value == 0x7F)
                    throw new NotSupportedException($"控制字节 0x{value:X2} 不属于当前结构化命令子集。");

                var end = offset + 1;
                while (end < bytes.Length && !IsCommandBoundary(bytes[end])) end++;
                var text = DecodeText(bytes, offset, end - offset);
                ValidateRestrictedTextWidth(text);
                WriteText(text);
                offset = end;
            }
            catch (Exception exception) when (IsRawValidationException(exception))
            {
                throw RawError(command, commandOffset, exception.Message, exception);
            }
        }
    }

    private void ParseControl(byte[] bytes, ref int offset)
    {
        var prefix = bytes[offset++];
        if (prefix == 0x1B)
        {
            var opcode = ReadByte(bytes, ref offset, "ESC 子命令");
            switch (opcode)
            {
                case 0x40:
                    Initialize();
                    _restrictedPrintAreaWidthUnits = null;
                    _restrictedWidthDots = Capabilities.MaxPrintableWidthDots;
                    return;
                case 0x74:
                    SelectCodePage(ReadByte(bytes, ref offset, "ESC t 代码页编号"));
                    return;
                case 0x61:
                    Align(ReadByte(bytes, ref offset, "ESC a 对齐方式"));
                    return;
                case 0x45:
                    var bold = ReadByte(bytes, ref offset, "ESC E 字重");
                    if (bold > 1) throw new ArgumentOutOfRangeException("ESC E 只允许 0 或 1。");
                    Bold(bold != 0);
                    return;
                case 0x2D:
                    var underline = ReadByte(bytes, ref offset, "ESC - 下划线");
                    if (underline > 1) throw new ArgumentOutOfRangeException("ESC - 只允许 0 或 1。");
                    Underline(underline != 0);
                    return;
                case 0x64:
                    Feed(ReadByte(bytes, ref offset, "ESC d 进纸行数"));
                    return;
                case 0x4A:
                    FeedMotionUnits(ReadByte(bytes, ref offset, "ESC J 进纸量"));
                    return;
                default:
                    throw new NotSupportedException($"ESC 0x{opcode:X2} 命令不在白名单内。");
            }
        }

        if (prefix == 0x1C)
        {
            var opcode = ReadByte(bytes, ref offset, "FS 子命令");
            if (opcode == 0x26) SetCharacterMode(EscPosCharacterMode.FsKanji);
            else if (opcode == 0x2E) SetCharacterMode(EscPosCharacterMode.SingleByte);
            else throw new NotSupportedException($"FS 0x{opcode:X2} 命令不在白名单内。");
            return;
        }

        var groupOpcode = ReadByte(bytes, ref offset, "GS 子命令");
        switch (groupOpcode)
        {
            case 0x48:
                offset -= 2;
                ParseBarcode(bytes, ref offset);
                return;
            case 0x42:
                var reverse = ReadByte(bytes, ref offset, "GS B 反白状态");
                if (reverse > 1) throw new ArgumentOutOfRangeException("GS B 只允许 0 或 1。");
                Reverse(reverse != 0);
                return;
            case 0x21:
                var size = ReadByte(bytes, ref offset, "GS ! 字符倍数");
                var width = (size >> 4) + 1;
                var height = (size & 0x0F) + 1;
                if (width > 8 || height > 8) throw new ArgumentOutOfRangeException("GS ! 保留位必须为零。");
                TextSize(width, height);
                return;
            case 0x57:
                var low = ReadByte(bytes, ref offset, "GS W 宽度低字节");
                var high = ReadByte(bytes, ref offset, "GS W 宽度高字节");
                SetPrintAreaWidth(low | high << 8);
                return;
            case 0x56:
                ParseCut(bytes, ref offset);
                return;
            case 0x6B:
                ParseBarcode(bytes, ref offset);
                return;
            case 0x28:
                offset -= 2;
                ParseQrCode(bytes, ref offset);
                return;
            default:
                throw new NotSupportedException($"GS 0x{groupOpcode:X2} 命令不在白名单内；光栅和状态命令被拒绝。");
        }
    }

    private void ParseCut(byte[] bytes, ref int offset)
    {
        var mode = ReadByte(bytes, ref offset, "GS V 切紙模式");
        if (mode == 0 || mode == 1)
        {
            Cut(new PrintCutOptions { Mode = mode == 0 ? PrintCutMode.Full : PrintCutMode.Partial });
            return;
        }
        if (mode == 65 || mode == 66)
        {
            var feed = ReadByte(bytes, ref offset, "GS V 切前进纸量");
            Cut(new PrintCutOptions
            {
                Mode = mode == 65 ? PrintCutMode.Full : PrintCutMode.Partial,
                FeedToCutPosition = true,
                FeedBeforeCutAmount = feed,
                FeedBeforeCutUnit = PrintFeedUnit.MotionUnits
            });
            return;
        }
        throw new NotSupportedException("GS V 只允许当前 API 定义的全切、半切及带补偿形式。");
    }

    private void ParseBarcode(byte[] bytes, ref int offset)
    {
        var textPosition = ReadSequenceValue(bytes, ref offset, 0x1D, 0x48, "GS H HRI 状态");
        var height = ReadSequenceValue(bytes, ref offset, 0x1D, 0x68, "GS h 条高");
        var width = ReadSequenceValue(bytes, ref offset, 0x1D, 0x77, "GS w 模块宽");
        RequirePrefix(bytes, ref offset, 0x1D, 0x6B, "GS k 条码头");
        var symbology = ReadByte(bytes, ref offset, "GS k 条码类型");
        var length = ReadByte(bytes, ref offset, "GS k 数据长度");
        if (length == 0 || offset + length > bytes.Length)
            throw new ArgumentException("GS k 长度字段与批次剩余数据不匹配。");
        var data = DecodeBarcodePayload(bytes, offset, length, symbology);
        offset += length;
        var type = BarcodeTypeFromSymbology(symbology);
        if (!type.HasValue) throw new NotSupportedException("该 GS k 条码类型没有可靠的现有宽度估算器。");
        var extent = PrintProtocolProviderBase.EstimateNativeBarcodeExtent(PrintProtocol.EscPos,
            type.Value, data, height, width, width, textPosition != 0, 0);
        ValidateRestrictedLineWidth(extent.Width, "ESC/POS 原生条码估算");
        Barcode(data, symbology, height, width, textPosition);
        _restrictedLineWidthDots = 0;
    }

    private void ParseQrCode(byte[] bytes, ref int offset)
    {
        var model = ReadQrPacket(bytes, ref offset, 4);
        if (model[0] != 0x31 || model[1] != 0x41 || model[2] != 0x32 || model[3] != 0)
            throw new ArgumentException("二维码初始化包不属于结构化 API 子集。");
        var module = ReadQrPacket(bytes, ref offset, 3);
        if (module[0] != 0x31 || module[1] != 0x43)
            throw new ArgumentException("二维码模块尺寸包顺序或功能码无效。");
        var correction = ReadQrPacket(bytes, ref offset, 3);
        if (correction[0] != 0x31 || correction[1] != 0x45)
            throw new ArgumentException("二维码纠错包顺序或功能码无效。");
        var dataPacket = ReadQrPacket(bytes, ref offset, null);
        if (dataPacket.Length < 4 || dataPacket[0] != 0x31 || dataPacket[1] != 0x50 || dataPacket[2] != 0x30)
            throw new ArgumentException("二维码存储数据包格式无效。");
        for (var index = 3; index < dataPacket.Length; index++)
            if (dataPacket[index] < 0x20 || dataPacket[index] == 0x7F)
                throw new NotSupportedException("二维码包不允许控制字节或任意二进制载荷。");
        var print = ReadQrPacket(bytes, ref offset, 3);
        if (print[0] != 0x31 || print[1] != 0x51 || print[2] != 0x30)
            throw new ArgumentException("二维码打印包必须完整且位于数据包之后。");
        var text = DecodeText(dataPacket, 3, dataPacket.Length - 3);
        var correctionLevel = correction[2];
        ValidateRestrictedQrWidth(text, module[2], correctionLevel);
        QrCode(text, module[2], correctionLevel);
        _restrictedLineWidthDots = 0;
    }

    private byte[] ReadQrPacket(byte[] bytes, ref int offset, int? expectedLength)
    {
        RequirePrefix(bytes, ref offset, 0x1D, 0x28, "GS ( k 二维码包");
        if (ReadByte(bytes, ref offset, "GS ( k 功能字节") != 0x6B)
            throw new ArgumentException("GS ( k 二维码包标识无效。");
        var low = ReadByte(bytes, ref offset, "二维码包长度低字节");
        var high = ReadByte(bytes, ref offset, "二维码包长度高字节");
        var length = low | high << 8;
        if (expectedLength.HasValue && length != expectedLength.Value)
            throw new ArgumentException("二维码固定参数包长度无效。");
        if (length == 0 || offset + length > bytes.Length)
            throw new ArgumentException("二维码包长度字段与批次剩余数据不匹配。");
        var payload = new byte[length];
        Array.Copy(bytes, offset, payload, 0, length);
        offset += length;
        return payload;
    }

    private void ValidateRestrictedTextWidth(string text)
    {
        var width = EstimateNativeTextWidth(text);
        ValidateRestrictedLineWidth(checked(width * _restrictedTextWidthScale), "ESC/POS Native 文字宽度估算");
    }

    private void ValidateRestrictedQrWidth(string data, byte moduleSize, byte correction)
    {
        RequireAvailableWidth("二维码");
        var level = correction == 48 ? QrCodeCorrectionLevel.L : correction == 49
            ? QrCodeCorrectionLevel.M : correction == 50 ? QrCodeCorrectionLevel.Q
            : correction == 51 ? QrCodeCorrectionLevel.H
            : throw new ArgumentOutOfRangeException(nameof(correction), "二维码纠错等级只允许 48 到 51。");
        var extent = EscPosRasterCanvas.MeasureQrCodeSize(data, moduleSize, level);
        ValidateRestrictedLineWidth(extent, "ESC/POS 原生二维码尺寸估算");
    }

    private void ValidateRestrictedLineWidth(long additionalWidth, string element)
    {
        var limit = RequireAvailableWidth(element);
        var total = checked(_restrictedLineWidthDots + additionalWidth);
        if (total > limit)
            throw new ArgumentOutOfRangeException(nameof(additionalWidth),
                $"{element}超过 ESC/POS 可打印宽度 {limit} dots；该设备字体/HRI/条码范围为保守估算。");
    }

    private int RequireAvailableWidth(string element)
        => _restrictedWidthDots is int width && width > 0
            ? width
            : throw new NotSupportedException($"{element}需要机型 Profile 或 GS W 声明可用打印宽度。");

    private int? GetRestrictedTabWidth()
        => _restrictedTabWidthDots ?? (_profile?.Text == null
            ? (int?)null : checked(_profile.Text.HalfCellWidthDots * 8));

    private int? ConvertWidthUnitsToDots(int widthUnits)
    {
        if (widthUnits == 0) return _profile?.PrintableWidthDots;
        var dpi = _profile?.HorizontalDpi ?? Capabilities.Dpi;
        var unitsPerInch = _profile?.MotionUnitsPerInch ?? Capabilities.Dpi;
        var converted = checked((long)widthUnits * dpi / unitsPerInch);
        var width = converted > int.MaxValue ? int.MaxValue : (int)converted;
        if (_profile?.PrintableWidthDots is int maximum) width = Math.Min(width, maximum);
        return width > 0 ? width : (int?)null;
    }

    private int EstimateNativeTextWidth(string text)
    {
        if (_profile?.Text != null) return _profile.Text.MeasureTextWidth(text);
        long width = 0;
        foreach (var scalar in PrintUnicodeScalars.Enumerate(text))
        {
            var value = char.ConvertFromUtf32(scalar);
            width = checked(width + (Encoding.GetByteCount(value) > 1 ? 24 : 12));
        }
        return width > int.MaxValue ? int.MaxValue : (int)width;
    }

    private void TrackRestrictedText(string text)
        => _restrictedLineWidthDots = checked(_restrictedLineWidthDots +
            (long)EstimateNativeTextWidth(text) * _restrictedTextWidthScale);

    private void AdvanceRestrictedTab()
    {
        var tabWidth = GetRestrictedTabWidth();
        if (!tabWidth.HasValue || tabWidth.Value <= 0) return;
        _restrictedLineWidthDots = checked((_restrictedLineWidthDots / tabWidth.Value + 1) * tabWidth.Value);
    }

    private static void RequirePrefix(byte[] bytes, ref int offset, byte first, byte second, string name)
    {
        if (ReadByte(bytes, ref offset, name + " 前缀") != first || ReadByte(bytes, ref offset, name + " 命令") != second)
            throw new ArgumentException($"{name} 序列不完整或顺序无效。");
    }

    private static byte ReadByte(byte[] bytes, ref int offset, string name)
    {
        if (offset >= bytes.Length) throw new ArgumentException($"{name} 缺少参数字节。");
        return bytes[offset++];
    }

    private static byte ReadSequenceValue(byte[] bytes, ref int offset, byte first, byte second, string name)
    {
        RequirePrefix(bytes, ref offset, first, second, name);
        return ReadByte(bytes, ref offset, name + " 参数");
    }

    private string DecodeText(byte[] bytes, int offset, int count)
        => Encoding.GetString(bytes, offset, count);

    private string DecodeBarcodePayload(byte[] bytes, int offset, int length, byte symbology)
    {
        for (var index = offset; index < offset + length; index++)
        {
            var value = bytes[index];
            if (value == 0x1D && (symbology == 74 || symbology == 78)) continue;
            if (value < 0x20 || value > 0x7E)
                throw new NotSupportedException("GS k 任意二進制载荷不属于受限 Raw 子集。");
        }
        return Encoding.GetString(bytes, offset, length);
    }

    private static bool IsCommandBoundary(byte value)
        => value < 0x20 || value == 0x7F;

    private static string DescribeByte(byte value)
        => value == 0x1B ? "ESC" : value == 0x1D ? "GS" : value == 0x1C ? "FS"
            : value == 0x0A ? "LF" : value == 0x09 ? "HT" : $"0x{value:X2}";

    private void CopyRestrictedRawState(EscPosPrintCommand source)
    {
        _restrictedWidthDots = source._restrictedWidthDots;
        _restrictedTabWidthDots = source._restrictedTabWidthDots;
        _restrictedPrintAreaWidthUnits = source._restrictedPrintAreaWidthUnits;
        _restrictedLineWidthDots = source._restrictedLineWidthDots;
        _restrictedAlignment = source._restrictedAlignment;
        _restrictedTextWidthScale = source._restrictedTextWidthScale;
    }

    private ArgumentException RawError(string command, int byteOffset, string reason, Exception inner = null)
        => new ArgumentException($"{Protocol} 受限 Raw 命令 '{command}' 字节偏移 {byteOffset} 验证失败：{reason}", "raw", inner);

    private static bool IsRawValidationException(Exception exception)
        => exception is ArgumentException || exception is NotSupportedException ||
           exception is InvalidOperationException || exception is OverflowException ||
           exception is FormatException;
}
