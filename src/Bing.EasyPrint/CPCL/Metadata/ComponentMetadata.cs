// ReSharper disable once CheckNamespace
namespace Bing.EasyPrint.CPCL;

/// <summary>
/// 组件 - 元数据
/// </summary>
internal abstract class ComponentMetadata : MetadataBase
{
    /// <summary>
    /// 元数据类型
    /// </summary>
    public override MetadataType MetadataType => MetadataType.Component;
}