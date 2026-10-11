namespace Bing.EasyPrint;

/// <summary>ESC/POS 内容布局模式。</summary>
public enum EscPosLayoutMode
{
    /// <summary>使用字符、条码等原生 ESC/POS 命令。</summary>
    Native = 0,
    /// <summary>由调用方提供光栅数据。</summary>
    Raster = 1
}
