using System;
using System.Text;

namespace Bing.EasyPrint.Templates;

/// <summary>
/// 配置单次模板渲染的资源限制。
/// </summary>
/// <remarks>
/// 调用方不应在渲染期间修改此实例。
/// </remarks>
public sealed class TemplateRenderOptions
{
    /// <summary>
    /// 获取或设置模板 JSON 的最大 UTF-8 字节数，默认 1 MiB。
    /// </summary>
    public int MaxJsonBytes { get; set; } = 1024 * 1024;
    /// <summary>
    /// 获取或设置模板结构的最大嵌套层数，默认 16。
    /// </summary>
    public int MaxDepth { get; set; } = 16;
    /// <summary>
    /// 获取或设置本次渲染的最大明细行数，默认 10000。
    /// </summary>
    public int MaxDetailRows { get; set; } = 10000;
    /// <summary>
    /// 获取或设置本次渲染的最大页数，默认 100。
    /// </summary>
    public int MaxPages { get; set; } = 100;
    /// <summary>
    /// 获取或设置 CPCL 输出的最大字节数，默认 32 MiB。
    /// </summary>
    public int MaxOutputBytes { get; set; } = 32 * 1024 * 1024;
    /// <summary>
    /// 获取或设置单张图片编码数据的最大字节数，默认 10 MiB。
    /// </summary>
    public int MaxImageBytes { get; set; } = 10 * 1024 * 1024;
    /// <summary>
    /// 获取或设置单张图片解码后的最大像素数，默认 1600 万。
    /// </summary>
    public long MaxImagePixels { get; set; } = 16000000;
    /// <summary>
    /// 获取或设置单页高度上限，单位为打印点。
    /// </summary>
    public int MaxPageHeight { get; set; } = 4096;
    /// <summary>
    /// 获取或设置打印输出编码。
    /// </summary>
    /// <remarks>
    /// 未指定时使用 GBK，调用方须注册 CodePagesEncodingProvider。
    /// </remarks>
    public Encoding Encoding { get; set; }

    /// <summary>
    /// 验证本次渲染的资源限制配置。
    /// </summary>
    internal void Validate()
    {
        if (MaxJsonBytes < 1 || MaxDepth < 1 || MaxDepth > 128 || MaxDetailRows < 1 ||
            MaxPages < 1 || MaxOutputBytes < 1 || MaxImageBytes < 1 || MaxImagePixels < 1 || MaxPageHeight < 1)
            throw new ArgumentOutOfRangeException(nameof(TemplateRenderOptions), "资源限制必须为正数，MaxDepth 不得超过 128。");
    }
}
