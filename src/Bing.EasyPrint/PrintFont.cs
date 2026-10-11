using System;

namespace Bing.EasyPrint;

/// <summary>
/// 指定文本使用的字体来源。
/// </summary>
public enum PrintFontKind
{
    /// <summary>
    /// 使用目标协议或设备的默认字体。
    /// </summary>
    Default = 0,

    /// <summary>
    /// 使用目标设备内置的字体标识。
    /// </summary>
    Device = 1,

    /// <summary>
    /// 使用主机字体进行渲染；固定字体配置中名称引用资源别名，否则引用系统字体族。
    /// </summary>
    Host = 2,
}

/// <summary>
/// 描述协议无关文本元素的字体来源和名称。
/// </summary>
public sealed class PrintFont
{
    /// <summary>
    /// 获取字体来源类型。
    /// </summary>
    public PrintFontKind Kind { get; }

    /// <summary>
    /// 获取主机字体名称/固定资源别名或设备字体标识；默认字体没有名称。
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 获取使用目标协议默认字体的不可变字体描述。
    /// </summary>
    public static PrintFont Default { get; } = new PrintFont();

    /// <summary>
    /// 创建字体描述。
    /// </summary>
    /// <param name="kind">字体来源类型。</param>
    /// <param name="name">主机字体名称或设备字体标识；默认字体必须为 <see langword="null" />。</param>
    /// <exception cref="ArgumentOutOfRangeException">字体来源类型不是已定义的 <see cref="PrintFontKind" /> 值。</exception>
    /// <exception cref="ArgumentException">名称与字体来源类型的约束不匹配。</exception>
    public PrintFont(PrintFontKind kind = PrintFontKind.Default, string name = null)
    {
        if (!Enum.IsDefined(typeof(PrintFontKind), kind))
            throw new ArgumentOutOfRangeException(nameof(kind));

        if (kind == PrintFontKind.Default)
        {
            if (name != null)
                throw new ArgumentException("默认字体不能指定名称。", nameof(name));
        }
        else if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("设备字体和主机字体必须指定非空白名称。", nameof(name));
        }

        Kind = kind;
        Name = name;
    }
}
