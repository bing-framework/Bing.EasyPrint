namespace Bing.EasyPrint;

/// <summary>打印机指令协议。</summary>
public enum PrintProtocol
{
    /// <summary>CPCL 标签协议。</summary>
    Cpcl = 0,
    /// <summary>ESC/POS 收据协议。</summary>
    EscPos = 1,
    /// <summary>TSPL 标签协议。</summary>
    Tspl = 2,
    /// <summary>ZPL 标签协议。</summary>
    Zpl = 3,
    /// <summary>CPCL（兼容命名）。</summary>
    CPCL = Cpcl,
    /// <summary>ESC/POS（兼容命名）。</summary>
    ESC_POS = EscPos,
    /// <summary>TSPL（兼容命名）。</summary>
    TSPL = Tspl,
    /// <summary>ZPL（兼容命名）。</summary>
    ZPL = Zpl
}
