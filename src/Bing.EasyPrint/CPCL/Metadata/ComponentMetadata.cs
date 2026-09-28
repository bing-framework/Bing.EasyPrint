// ReSharper disable once CheckNamespace
namespace Bing.EasyPrint.CPCL;

/// <summary>
/// 表示可作为打印组件写入的元数据。
/// </summary>
internal abstract class ComponentMetadata : MetadataBase
{
    /// <inheritdoc />
    public override MetadataType MetadataType => MetadataType.Component;
}
