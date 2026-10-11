namespace Bing.EasyPrint.Protocols;

/// <summary>调用方按设备资料声明的 ZPL 机型限制；不探测设备或下载字体。</summary>
public sealed class ZplPrinterProfile
{
    /// <summary>机型配置名称。</summary>
    public string Name { get; set; } = "Custom";
    /// <summary>可选固件版本记录，不触发自动兼容推断。</summary>
    public string FirmwareVersion { get; set; }
    /// <summary>相对打印原点的最大可打印宽度，单位为当前 DPI 的点。</summary>
    public int? PrintableWidthDots { get; set; }
    /// <summary>最大标签高度，单位为当前 DPI 的点。</summary>
    public int? MaxLabelHeightDots { get; set; }
    /// <summary>单条图片的单色数据字节上限，不包含十六进制膨胀或命令头。</summary>
    public int? MaxImageDataBytes { get; set; }
    /// <summary>设备手册声明的 GS1-128 payload 字符上限；统计规范化数据及 ASCII 29 分隔符，不含符号 framing。</summary>
    public int? MaxGs1LinearPayloadCharacters { get; set; }
    /// <summary>设备手册声明的 GS1 DataBar Expanded payload 字符上限；为空时只检查协议字段范围。</summary>
    public int? MaxGs1DataBarExpandedPayloadCharacters { get; set; }
    /// <summary>是否支持 ^GFA。</summary>
    public bool SupportsImages { get; set; } = true;
    /// <summary>是否支持线框绘图；位图斜线还须允许图片。</summary>
    public bool SupportsGraphics { get; set; } = true;
    /// <summary>是否支持原生一维条码。</summary>
    public bool SupportsNativeBarcode { get; set; } = true;
    /// <summary>是否支持原生一维条码的设备可读文字（HRI）。</summary>
    public bool SupportsNativeBarcodeHumanReadable { get; set; } = true;
    /// <summary>已确认支持原生 HRI 的条码制式；null 表示不按制式收窄，空数组表示全部禁用。</summary>
    public BarcodeType[] SupportedNativeBarcodeHumanReadableTypes { get; set; }
    /// <summary>direct 条码页边界预检的 HRI 保留高度，单位为当前 DPI 的打印点；null 沿用兼容性 24 点主机估值。</summary>
    /// <remarks>应按目标机型手册或实测填写保守值；此值只影响主机边界估算，不设置打印机字体或字号。</remarks>
    public int? NativeBarcodeHumanReadableReserveHeightDots { get; set; }
    /// <summary>原生 HRI 页边界预检的最小横向占位宽度，单位为当前 DPI 的打印点；仅在 HRI 启用时使用。</summary>
    /// <remarks>该宽度从条码字段原点向右计量，只影响主机边界估算；居中 HRI 的左侧外伸仍须由调用方留边。</remarks>
    public int? NativeBarcodeHumanReadableReserveWidthDots { get; set; }
    /// <summary>HRI 启用时相对条码字段坐标向页面 X 轴左侧外伸的距离，单位为打印点；默认不增加左侧占位。</summary>
    /// <remarks>该值用于主机边界预检，不移动输出字段或改变设备对齐；应按目标机型的实际可读文字边界校准。</remarks>
    public int? NativeBarcodeHumanReadableLeftOverhangDots { get; set; }
    /// <summary>HRI 启用时超出条码估算矩形上边的页面坐标边距，单位为打印点；只扩充主机边界预检。</summary>
    public int? NativeBarcodeHumanReadableTopOverhangDots { get; set; }
    /// <summary>HRI 启用时超出条码估算矩形右边的页面坐标边距，单位为打印点；与最小总宽预留相加。</summary>
    public int? NativeBarcodeHumanReadableRightOverhangDots { get; set; }
    /// <summary>HRI 启用时超出条码估算矩形下边的页面坐标边距，单位为打印点；只扩充主机边界预检。</summary>
    public int? NativeBarcodeHumanReadableBottomOverhangDots { get; set; }
    /// <summary>原生条码旋转 90 度时的 HRI 页面边距覆盖；每个 null 边继承通用边距。</summary>
    public NativeBarcodeHumanReadableOverhang? NativeBarcodeHumanReadableRotation90Overhang { get; set; }
    /// <summary>原生条码旋转 180 度时的 HRI 页面边距覆盖；每个 null 边继承通用边距。</summary>
    public NativeBarcodeHumanReadableOverhang? NativeBarcodeHumanReadableRotation180Overhang { get; set; }
    /// <summary>原生条码旋转 270 度时的 HRI 页面边距覆盖；每个 null 边继承通用边距。</summary>
    public NativeBarcodeHumanReadableOverhang? NativeBarcodeHumanReadableRotation270Overhang { get; set; }
    /// <summary>是否支持原生二维码。</summary>
    public bool SupportsNativeQrCode { get; set; } = true;
    /// <summary>已确认支持的原生二维码非零旋转角度；null 表示不按角度收窄，空数组表示禁用非零旋转。</summary>
    public RotationAngle[] SupportedNativeQrCodeRotations { get; set; }
    /// <summary>是否支持原生文字及条码旋转。</summary>
    public bool SupportsRotation { get; set; } = true;
    /// <summary>是否支持现有 ^CN 切纸；切刀模式和补偿另由裁切契约描述。</summary>
    public bool SupportsCut { get; set; } = true;
    /// <summary>可选受控切刀声明；为空时仅保留旧裸 ^CN 输出。</summary>
    public ZplCutterProfile Cutter { get; set; }
    /// <summary>已确认的内置字体标识 0–9/A–Z；null 不额外限制，空数组禁用文字。</summary>
    public string[] DeviceFontNames { get; set; }
    /// <summary>已确认的主机代码页，只能收窄当前 ASCII、UTF-8 映射；null 不额外限制。</summary>
    public int[] EncodingCodePages { get; set; }
    /// <summary>所声明字体共同支持的 Unicode 区间；null 不额外限制，不证明设备字库覆盖。</summary>
    public PrintCharacterRange[] CharacterRanges { get; set; }

    internal LabelDeviceContract Snapshot()
    {
        Cutter?.Snapshot();
        if (Cutter != null && !SupportsCut) throw new System.ArgumentException("禁用切刀时不能配置 Cutter。", nameof(Cutter));
        return LabelDeviceContract.Create(PrintProtocol.Zpl,
        Name, FirmwareVersion, PrintableWidthDots, MaxLabelHeightDots, MaxImageDataBytes,
        MaxGs1LinearPayloadCharacters, MaxGs1DataBarExpandedPayloadCharacters,
        SupportsImages, SupportsGraphics, SupportsNativeBarcode, SupportsNativeBarcodeHumanReadable,
        NativeBarcodeHumanReadableReserveHeightDots, NativeBarcodeHumanReadableReserveWidthDots,
        NativeBarcodeHumanReadableLeftOverhangDots, NativeBarcodeHumanReadableTopOverhangDots,
        NativeBarcodeHumanReadableRightOverhangDots, NativeBarcodeHumanReadableBottomOverhangDots,
        NativeBarcodeHumanReadableRotation90Overhang, NativeBarcodeHumanReadableRotation180Overhang,
        NativeBarcodeHumanReadableRotation270Overhang,
        SupportedNativeBarcodeHumanReadableTypes,
        SupportsNativeQrCode,
        SupportedNativeQrCodeRotations,
        SupportsRotation, SupportsCut, DeviceFontNames, EncodingCodePages, CharacterRanges);
    }

    internal ZplPrinterProfile Clone()
    {
        var copy = (ZplPrinterProfile)MemberwiseClone();
        copy.Cutter = Cutter?.Snapshot();
        copy.DeviceFontNames = DeviceFontNames == null ? null : (string[])DeviceFontNames.Clone();
        copy.EncodingCodePages = EncodingCodePages == null ? null : (int[])EncodingCodePages.Clone();
        copy.CharacterRanges = CharacterRanges == null ? null : (PrintCharacterRange[])CharacterRanges.Clone();
        copy.SupportedNativeBarcodeHumanReadableTypes = SupportedNativeBarcodeHumanReadableTypes == null
            ? null : (BarcodeType[])SupportedNativeBarcodeHumanReadableTypes.Clone();
        copy.SupportedNativeQrCodeRotations = SupportedNativeQrCodeRotations == null
            ? null : (RotationAngle[])SupportedNativeQrCodeRotations.Clone();
        return copy;
    }
}
