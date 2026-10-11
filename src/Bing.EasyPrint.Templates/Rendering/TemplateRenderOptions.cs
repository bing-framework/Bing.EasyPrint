using System;
using System.Collections.Generic;
using System.Text;
using Bing.EasyPrint.Protocols;
using Bing.EasyPrint.Printing;

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
    /// 获取或设置目标打印协议。默认保持现有 CPCL 输出。
    /// </summary>
    public PrintProtocol Protocol { get; set; } = PrintProtocol.Cpcl;
    /// <summary>
    /// 获取或设置 ESC/POS 的布局模式。默认 Native；Raster 需显式选择。
    /// </summary>
    public EscPosLayoutMode EscPosLayout { get; set; } = EscPosLayoutMode.Native;
    /// <summary>获取或设置每页打印份数，默认为一份。</summary>
    public int Copies { get; set; } = 1;
    /// <summary>获取或设置打印后的额外进纸行数。未指定时 ESC/POS 为一行，其他协议为零。</summary>
    public int? FeedLines { get; set; }
    /// <summary>获取或设置打印后是否切纸。未指定时 ESC/POS 切纸，其他协议不切纸。</summary>
    public bool? CutAfterPrint { get; set; }
    /// <summary>可选页面裁切形状和补偿；与任务裁切参数互斥，省略保留原输出。</summary>
    public PrintCutOptions CutOptions { get; set; }
    /// <summary>显式任务级份数、顺序和收尾策略；为空时保留旧页面行为。</summary>
    public PrintJobOptions Job { get; set; }
    /// <summary>获取或设置 ESC/POS 设备专用设置。</summary>
    public EscPosDeviceOptions EscPosDevice { get; set; }
    /// <summary>获取或设置 TSPL 介质和设备设置。</summary>
    public TsplMediaOptions TsplMedia { get; set; }
    /// <summary>获取或设置 ZPL 介质和设备设置。</summary>
    public ZplMediaOptions ZplMedia { get; set; }
    /// <summary>
    /// 获取或设置 TSPL/ZPL 设备内置字体在 24 点字号下的半角字宽。
    /// 未指定时按 18 点估算；全角按两个半角单元计算。
    /// </summary>
    public double? DeviceHalfCellWidthAt24 { get; set; }
    /// <summary>
    /// 获取或设置 TSPL/ZPL 设备内置字体在 24 点字号下的全角字宽。
    /// 未指定时使用半角字宽的两倍；用于校准中英混排的换行和分页。
    /// </summary>
    public double? DeviceFullCellWidthAt24 { get; set; }
    /// <summary>获取或设置 TSPL/ZPL 设备字体在 24 点字号下实测的行高，用于分页和文字预留高度。</summary>
    public double? DeviceLineHeightAt24 { get; set; }
    /// <summary>
    /// 获取或设置按设备字体标识索引的实测占位；匹配时优先于全局 24 点校准值。
    /// 未显式指定设备字体的文字使用键 "0"。
    /// </summary>
    public Dictionary<string, DeviceFontCalibration> DeviceFontCalibrations { get; set; }
    /// <summary>要求 TSPL/ZPL 模板明确声明设备文字度量和原生条码宽度；默认关闭以兼容旧模板。</summary>
    public bool RequireDeclaredNativeBounds { get; set; }
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
    /// 获取或设置打印输出的最大字节数，默认 32 MiB。
    /// </summary>
    public int MaxOutputBytes { get; set; } = 32 * 1024 * 1024;
    /// <summary>
    /// 可选的受管缓冲区和图像像素预留上限（字节）；为空时保留原资源限制。
    /// 同时计算活动临时资源与保留结果，不能作为进程实际峰值内存数值。
    /// </summary>
    public long? MaxWorkingBytes { get; set; }
    /// <summary>
    /// 获取或设置单张图片编码数据的最大字节数，默认 10 MiB。
    /// </summary>
    public int MaxImageBytes { get; set; } = 10 * 1024 * 1024;
    /// <summary>获取或设置非 CPCL 图片的黑白阈值；合成到白纸后的亮度低于此值时输出黑点。默认 128。</summary>
    public int ImageThreshold { get; set; } = 128;
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
        if (MaxWorkingBytes < 0) throw new ArgumentOutOfRangeException(nameof(MaxWorkingBytes));
        if (CutOptions != null)
        {
            CutOptions.Validate();
            if (Job != null || CutAfterPrint == false)
                throw new ArgumentException("页面裁切参数不能与任务策略或关闭页面切纸同时使用。", nameof(CutOptions));
        }
        if (Job != null)
        {
            Job.Validate();
            if (Copies != 1 || FeedLines > 0 || CutAfterPrint == true)
                throw new ArgumentException("任务策略不能与旧页面份数、正数进纸或切纸设置同时使用。", nameof(Job));
            if (Job.Copies > MaxPages)
                throw new ArgumentException("任务份数已经超过实际输出页数上限。", nameof(Job));
        }
        if (!Enum.IsDefined(typeof(EscPosLayoutMode), EscPosLayout))
            throw new ArgumentOutOfRangeException(nameof(EscPosLayout));
        if (Protocol != PrintProtocol.EscPos && EscPosDevice != null)
            throw new ArgumentException("ESC/POS 设备选项只能用于 ESC/POS 协议。", nameof(EscPosDevice));
        if (EscPosDevice != null)
            _ = new EscPosPrintCommand(Encoding ?? System.Text.Encoding.GetEncoding("gbk"), EscPosLayout, EscPosDevice);
        if (EscPosDevice?.NativeTabWidthDots is int tabWidth &&
            (EscPosLayout != EscPosLayoutMode.Native || tabWidth < 8 || tabWidth % 8 != 0))
            throw new ArgumentException("Native 制表位点距必须是至少 8 点的 8 的倍数，且只能用于 Native 模式。",
                nameof(EscPosDevice));
        if (Protocol != PrintProtocol.Tspl && TsplMedia != null)
            throw new ArgumentException("TSPL 介质选项只能用于 TSPL 协议。", nameof(TsplMedia));
        if (Protocol != PrintProtocol.Zpl && ZplMedia != null)
            throw new ArgumentException("ZPL 介质选项只能用于 ZPL 协议。", nameof(ZplMedia));
        if (DeviceHalfCellWidthAt24.HasValue &&
            (Protocol != PrintProtocol.Tspl && Protocol != PrintProtocol.Zpl ||
             double.IsNaN(DeviceHalfCellWidthAt24.Value) || double.IsInfinity(DeviceHalfCellWidthAt24.Value) ||
             DeviceHalfCellWidthAt24.Value <= 0))
            throw new ArgumentException("设备字体字宽只适用于 TSPL/ZPL，且必须为有限正数。", nameof(DeviceHalfCellWidthAt24));
        if (DeviceFullCellWidthAt24.HasValue &&
            (Protocol != PrintProtocol.Tspl && Protocol != PrintProtocol.Zpl ||
             double.IsNaN(DeviceFullCellWidthAt24.Value) || double.IsInfinity(DeviceFullCellWidthAt24.Value) ||
             DeviceFullCellWidthAt24.Value <= 0))
            throw new ArgumentException("设备全角字宽只适用于 TSPL/ZPL，且必须为有限正数。", nameof(DeviceFullCellWidthAt24));
        if (DeviceLineHeightAt24.HasValue &&
            (Protocol != PrintProtocol.Tspl && Protocol != PrintProtocol.Zpl ||
             double.IsNaN(DeviceLineHeightAt24.Value) || double.IsInfinity(DeviceLineHeightAt24.Value) ||
             DeviceLineHeightAt24.Value <= 0))
            throw new ArgumentException("设备字体行高只适用于 TSPL/ZPL，且必须为有限正数。", nameof(DeviceLineHeightAt24));
        if (DeviceFontCalibrations != null)
        {
            if (Protocol != PrintProtocol.Tspl && Protocol != PrintProtocol.Zpl)
                throw new ArgumentException("逐字体校准只适用于 TSPL/ZPL。", nameof(DeviceFontCalibrations));
            foreach (var entry in DeviceFontCalibrations)
            {
                var name = entry.Key;
                var valid = Protocol == PrintProtocol.Tspl
                    ? name != null && name.Length == 1 && name[0] >= '0' && name[0] <= '8'
                    : name != null && name.Length == 1 &&
                      (name[0] >= '0' && name[0] <= '9' || name[0] >= 'A' && name[0] <= 'Z');
                if (!valid || entry.Value == null)
                    throw new ArgumentException($"协议 {Protocol} 的设备字体校准键或值无效：{name ?? "<null>"}。",
                        nameof(DeviceFontCalibrations));
                entry.Value.Validate(name);
            }
        }
        if (RequireDeclaredNativeBounds && Protocol != PrintProtocol.Tspl && Protocol != PrintProtocol.Zpl)
            throw new ArgumentException("设备原生占位严格校验只适用于 TSPL/ZPL。", nameof(RequireDeclaredNativeBounds));
        if (ImageThreshold < 0 || ImageThreshold > 255)
            throw new ArgumentOutOfRangeException(nameof(ImageThreshold));
        if (Copies < 1 || FeedLines < 0 || MaxJsonBytes < 1 || MaxDepth < 1 || MaxDepth > 128 || MaxDetailRows < 1 ||
            MaxPages < 1 || MaxOutputBytes < 1 || MaxImageBytes < 1 || MaxImagePixels < 1 || MaxPageHeight < 1)
            throw new ArgumentOutOfRangeException(nameof(TemplateRenderOptions), "资源限制必须为正数，MaxDepth 不得超过 128。");
    }
}
