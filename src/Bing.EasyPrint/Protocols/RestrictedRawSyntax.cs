using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Bing.EasyPrint.Printing;

namespace Bing.EasyPrint.Protocols;

internal readonly struct RestrictedRawLine
{
    internal RestrictedRawLine(string text, int number, int offset)
    {
        Text = text;
        Number = number;
        Offset = offset;
    }

    internal string Text { get; }
    internal int Number { get; }
    internal int Offset { get; }
}

internal static class RestrictedRawSyntax
{
    // 估算覆盖输入长度相关的字符串副本及最坏情况下的逐行/逐 token 集合槽位。
    private const long TokenizerWorkingSetBaseBytes = 512;
    private const long TokenizerWorkingSetBytesPerCharacter = 128;

    internal static IDisposable ReserveTokenizerWorkingSet(string raw, string resourceName)
    {
        if (raw == null) throw new ArgumentNullException(nameof(raw));
        var estimatedBytes = checked(TokenizerWorkingSetBaseBytes +
                                     (long)raw.Length * TokenizerWorkingSetBytesPerCharacter);
        return PrintResourceScope.Reserve(estimatedBytes, resourceName);
    }

    internal static IEnumerable<RestrictedRawLine> ReadLines(string raw)
    {
        var start = 0;
        var number = 1;
        for (var index = 0; index < raw.Length; index++)
        {
            if (raw[index] != '\r' && raw[index] != '\n') continue;
            yield return new RestrictedRawLine(raw.Substring(start, index - start), number++, start);
            if (raw[index] == '\r' && index + 1 < raw.Length && raw[index + 1] == '\n') index++;
            start = index + 1;
        }
        if (start < raw.Length)
            yield return new RestrictedRawLine(raw.Substring(start), number, start);
    }

    internal static string[] ParseDelimited(string value, int expectedCount)
        => ParseDelimited(value, expectedCount, expectedCount);

    internal static string[] ParseDelimited(string value, int minimumCount, int maximumCount)
    {
        if (minimumCount < 0 || maximumCount < minimumCount) throw new ArgumentOutOfRangeException();
        var fields = new List<string>(maximumCount);
        var field = new StringBuilder();
        var quoted = false;
        var wasQuoted = false;
        var quoteClosed = false;
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (quoted)
            {
                if (character == '"')
                {
                    quoted = false;
                    quoteClosed = true;
                }
                else field.Append(character);
                continue;
            }

            if (character == ',')
            {
                fields.Add(wasQuoted ? field.ToString() : field.ToString().Trim());
                field.Clear();
                wasQuoted = false;
                quoteClosed = false;
                continue;
            }

            if (character == '"')
            {
                if (quoteClosed || field.ToString().Trim().Length != 0)
                    throw new FormatException("引号必须完整包围单个字段，且字段中不允许转义引号。");
                field.Clear();
                quoted = true;
                wasQuoted = true;
                continue;
            }

            if (quoteClosed && !char.IsWhiteSpace(character))
                throw new FormatException("结束引号后只能出现空格或字段分隔符。");
            if (!wasQuoted) field.Append(character);
        }

        if (quoted) throw new FormatException("字段引号未闭合。");
        fields.Add(wasQuoted ? field.ToString() : field.ToString().Trim());
        if (fields.Count < minimumCount || fields.Count > maximumCount)
            throw new FormatException($"字段数量必须为 {minimumCount} 到 {maximumCount}。");
        return fields.ToArray();
    }

    internal static int ParseInt(string value, string name)
    {
        if (!int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var result))
            throw new FormatException($"{name} 必须是十进制整数。");
        return result;
    }

    internal static string CommandName(string line)
    {
        var trimmed = line.TrimStart();
        var end = 0;
        while (end < trimmed.Length && !char.IsWhiteSpace(trimmed[end])) end++;
        return end == 0 ? "<空命令>" : trimmed.Substring(0, end);
    }

    internal static ArgumentException Error(PrintProtocol protocol, string command, int line,
        string reason, Exception inner = null)
        => new ArgumentException($"{protocol} 受限 Raw 命令 '{command}' 第 {line} 行验证失败：{reason}", "raw", inner);

    internal static ArgumentException Error(PrintProtocol protocol, string command, int byteOffset,
        string reason, Encoding encoding, Exception inner = null)
        => new ArgumentException($"{protocol} 受限 Raw 命令 '{command}' 字节偏移 {byteOffset} 验证失败：{reason}", "raw", inner);

    internal static int GetByteOffset(Encoding encoding, string raw, int charOffset)
        => encoding.GetByteCount(raw.Substring(0, charOffset));

    internal static bool IsHex(string value)
    {
        if (value == null) return false;
        foreach (var character in value)
            if (!(character >= '0' && character <= '9' || character >= 'A' && character <= 'F' ||
                  character >= 'a' && character <= 'f')) return false;
        return true;
    }
}
