using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Bing.EasyPrint.CPCL;

/// <summary>
/// 阻止结构化打印内容包含协议控制字符或独立命令。
/// </summary>
internal static class CPCLInputValidator
{
    /// <summary>
    /// 识别 CPCL 打印任务起始命令。
    /// </summary>
    private static readonly Regex BeginCommand = new Regex(
        "^!\\s[0-9]+\\s[0-9]+\\s[0-9]+\\s[0-9]+\\s[0-9]+\\s{0,1}$",
        RegexOptions.CultureInvariant);
    /// <summary>
    /// 识别可改变页面宽度的独立命令。
    /// </summary>
    private static readonly Regex PageWidthCommand = new Regex(
        "^(PAGE-WIDTH|PW)\\s[0-9]+\\s{0,1}$",
        RegexOptions.CultureInvariant);

    /// <summary>
    /// 校验结构化内容不含协议控制字符。
    /// </summary>
    /// <param name="value">待校验内容，允许 null。</param>
    /// <param name="parameterName">异常中使用的参数名。</param>
    public static void ValidateLine(string value, string parameterName)
    {
        if (value == null) return;
        foreach (var character in value)
            if (char.IsControl(character))
                throw new ArgumentException("结构化打印内容不能包含协议控制字符。", parameterName);
    }

    /// <summary>
    /// 校验单个字符不属于协议控制字符。
    /// </summary>
    /// <param name="value">待校验字符。</param>
    /// <param name="parameterName">异常中使用的参数名。</param>
    public static void ValidateCharacter(char value, string parameterName)
    {
        if (char.IsControl(value))
            throw new ArgumentException("结构化打印内容不能包含协议控制字符。", parameterName);
    }

    /// <summary>
    /// 校验协议参数是非空且不含空白的单个标记。
    /// </summary>
    /// <param name="value">待校验参数。</param>
    /// <param name="parameterName">异常中使用的参数名。</param>
    public static void ValidateToken(string value, string parameterName)
    {
        if (string.IsNullOrEmpty(value)) throw new ArgumentException("协议参数不能为空。", parameterName);
        var hasNonWhitespace = false;
        var hasWhitespace = false;
        foreach (var character in value)
        {
            if (char.IsControl(character))
                throw new ArgumentException("结构化打印内容不能包含协议控制字符。", parameterName);
            if (char.IsWhiteSpace(character))
                hasWhitespace = true;
            else
                hasNonWhitespace = true;
        }
        if (!hasNonWhitespace) throw new ArgumentException("协议参数不能为空。", parameterName);
        if (hasWhitespace) throw new ArgumentException("协议参数必须是单个标记。", parameterName);
    }

    /// <summary>
    /// 校验负载行不含任务控制命令或指定块终止符。
    /// </summary>
    /// <param name="value">待校验负载行。</param>
    /// <param name="parameterName">异常中使用的参数名。</param>
    /// <param name="terminator">可选的块终止命令。</param>
    public static void ValidatePayloadLine(string value, string parameterName, string terminator = null)
    {
        ValidateLine(value, parameterName);
        if (string.IsNullOrWhiteSpace(value))
            return;

        var line = value.Trim();
        if (string.Equals(line, "PRINT", StringComparison.OrdinalIgnoreCase) ||
            BeginCommand.IsMatch(line) || PageWidthCommand.IsMatch(line))
            throw new ArgumentException("结构化数据不能包含独立的 CPCL 控制命令。", parameterName);

        if (!string.IsNullOrEmpty(terminator) &&
            line.StartsWith(terminator, StringComparison.OrdinalIgnoreCase) &&
            (line.Length == terminator.Length || char.IsWhiteSpace(line[terminator.Length])))
            throw new ArgumentException("结构化数据不能包含块结束命令。", parameterName);
    }

    /// <summary>
    /// 逐行校验结构化负载。
    /// </summary>
    /// <param name="values">待校验的负载行集合。</param>
    /// <param name="parameterName">异常中使用的参数名。</param>
    /// <param name="terminator">该负载块的终止命令。</param>
    public static void ValidatePayloadLines(IEnumerable<string> values, string parameterName, string terminator)
    {
        if (values == null)
            throw new ArgumentNullException(parameterName);
        foreach (var value in values)
            ValidatePayloadLine(value, parameterName, terminator);
    }

    /// <summary>
    /// 校验图形数据格式与声明的宽高一致。
    /// </summary>
    /// <param name="width">每行字节数。</param>
    /// <param name="height">图形行数。</param>
    /// <param name="data">十六进制或编码后的二进制内容。</param>
    /// <param name="encoding">压缩内容的字符串编码。</param>
    /// <param name="expanded">是否使用十六进制扩展格式。</param>
    public static void ValidateGraphicsData(int width, int height, string data, System.Text.Encoding encoding, bool expanded)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        if (data == null) throw new ArgumentNullException(nameof(data));
        var expected = (long)width * height;
        if (expanded)
        {
            if (data.Length != expected * 2) throw new ArgumentException("图形数据长度与宽高不符。", nameof(data));
            foreach (var c in data)
                if (!(c >= '0' && c <= '9') && !(c >= 'a' && c <= 'f') && !(c >= 'A' && c <= 'F'))
                    throw new ArgumentException("扩展图形只能包含十六进制字符。", nameof(data));
        }
        else if (encoding.GetByteCount(data) != expected)
            throw new ArgumentException("压缩图形编码字节数与宽高不符；建议使用 DrawCompressedImage。", nameof(data));
    }
}
