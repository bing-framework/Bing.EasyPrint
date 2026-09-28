using System.Text;
using System;
using Bing.EasyPrint.Core;

// ReSharper disable once CheckNamespace
namespace Bing.EasyPrint.CPCL;

/// <summary>
/// 构建 CPCL 打印任务命令。
/// </summary>
// ReSharper disable once InconsistentNaming
public partial class CPCLPrintCommand : IPrintCommand<CPCLPrintCommand>, IPrintComponent<CPCLPrintCommand>
{
    /// <summary>
    /// 获取当前构建过程的字节写入器。
    /// </summary>
    internal IBufferWriter Writer { get; private set; }

    /// <summary>
    /// 保存构建 CPCL 命令时使用的文本编码。
    /// </summary>
    private readonly Encoding _encoding;

    /// <summary>
    /// 获取待构建的命令结构。
    /// </summary>
    internal PrintCommandStruct Content { get; }

    /// <summary>
    /// 获取当前打印任务的页面设置。
    /// </summary>
    public CPCLCommandInfo CommandInfo { get; internal set; }

    /// <summary>
    /// 初始化一个 <see cref="CPCLPrintCommand"/> 类型的实例。
    /// </summary>
    public CPCLPrintCommand() : this(System.Text.Encoding.GetEncoding("gbk")) { }

    /// <summary>
    /// 初始化一个 <see cref="CPCLPrintCommand"/> 类型的实例。
    /// </summary>
    /// <param name="encoding">命令文本所用编码。</param>
    public CPCLPrintCommand(Encoding encoding)
    {
        _encoding = encoding ?? throw new ArgumentNullException(nameof(encoding));
        Writer = new BufferWriter(_encoding);
        CommandInfo = new CPCLCommandInfo();
        Content = new PrintCommandStruct();
    }

    /// <summary>
    /// 初始化
    /// </summary>
    /// <param name="width">宽度</param>
    /// <param name="height">高度</param>
    /// <returns>当前打印命令实例。</returns>
    internal CPCLPrintCommand Init(int width, int height)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        CommandInfo = new CPCLCommandInfo {Width = width, Height = height};
        Writer = new BufferWriter(_encoding);
        Content.Clear();
        return this;
    }

    /// <inheritdoc />
    public CPCLPrintCommand WriteRaw(string raw)
    {
        Content.AddRaw(raw);
        return this;
    }

    /// <inheritdoc />
    public CPCLPrintCommand WriteRawLine(string raw)
    {
        Content.AddRawLine(raw);
        return this;
    }

    /// <inheritdoc />
    public IBufferWriter Build()
    {
        var previousWriter = Writer;
        var candidate = new BufferWriter(_encoding);
        Writer = candidate;
        try
        {
            Content.SafeInit(CommandInfo);
            Content.Build(this);
            return candidate;
        }
        catch
        {
            Writer = previousWriter;
            throw;
        }
    }
}
