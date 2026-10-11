namespace Bing.EasyPrint;

/// <summary>调用方明确请求的裁切形状；目标机型必须支持该模式。</summary>
public enum PrintCutMode
{
    /// <summary>完全切断纸张。</summary>
    Full = 0,
    /// <summary>部分切断，保留设备规定的连接点。</summary>
    Partial = 1
}
