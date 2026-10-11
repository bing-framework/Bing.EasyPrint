using System;

namespace Bing.EasyPrint.Protocols;

/// <summary>校验设备 Profile 声明的 GS1 单符号 payload 字符上限。</summary>
internal static class Gs1BarcodeCapacity
{
    internal static string NormalizePayload(string data, string name)
    {
        if (data == null) throw new ArgumentNullException(name);
        return Gs1ApplicationIdentifierParser.IsBracketed(data)
            ? Gs1ApplicationIdentifierParser.NormalizeForProtocol(data, name, 255)
            : data;
    }

    internal static void Validate(BarcodeType type, string data, int? maximumCharacters, string name)
    {
        if (!maximumCharacters.HasValue) return;
        var normalized = NormalizePayload(data, name);
        if (normalized.Length > maximumCharacters.Value)
            throw new NotSupportedException(
                $"设备声明的 {type} 单符号 payload 上限为 {maximumCharacters.Value} 个字符，当前为 {normalized.Length} 个。");
    }

    internal static void ValidateLimit(int? maximumCharacters, string name)
    {
        if (maximumCharacters.HasValue && maximumCharacters.Value <= 0)
            throw new ArgumentOutOfRangeException(name, "GS1 payload 字符上限必须为正数。");
    }
}
