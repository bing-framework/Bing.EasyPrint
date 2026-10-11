namespace Bing.EasyPrint.Protocols;

/// <summary>Core、模板和设计器 Host 共用的固定字体入口边界。</summary>
public static class RasterFontLimits
{
    /// <summary>一个字体目录允许的最大资源数，与设计器现有入口限制一致。</summary>
    public const int MaxResourceCount = 64;

    /// <summary>单资源及整个目录允许的常规、粗体原始字体数据总字节数。</summary>
    public const long MaxTotalDataBytes = 32L * 1024 * 1024;
}
