// ReSharper disable once CheckNamespace
namespace Bing.EasyPrint.CPCL;

/// <summary>
/// 保存未解析的原始 CPCL 命令。
/// </summary>
internal class RawMetadata : MetadataBase
{
    /// <inheritdoc />
    public override MetadataType MetadataType => MetadataType.Raw;

    /// <summary>
    /// 获取原始 CPCL 命令内容。
    /// </summary>
    public string Raw { get; }

    /// <summary>
    /// 获取写入命令后是否追加换行。
    /// </summary>
    public bool NewLine { get; }

    /// <summary>
    /// 初始化一个 <see cref="RawMetadata"/> 类型的实例。
    /// </summary>
    /// <param name="raw">原始命令</param>
    public RawMetadata(string raw) : this(raw, false) { }

    /// <summary>
    /// 初始化一个 <see cref="RawMetadata"/> 类型的实例。
    /// </summary>
    /// <param name="raw">命令</param>
    /// <param name="newLine">是否换行</param>
    public RawMetadata(string raw, bool newLine)
    {
        Raw = raw;
        NewLine = newLine;
    }

    /// <inheritdoc />
    public override void Build(CPCLPrintCommand command)
    {
        if (NewLine)
            command.Writer.WriteLine(Raw);
        else
            command.Writer.Write(Raw);
    }
}
