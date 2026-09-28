// ReSharper disable once CheckNamespace
namespace Bing.EasyPrint;

/// <summary>
/// 指定二维码用于恢复受损码字的纠错等级。
/// </summary>
public enum QrCodeCorrectionLevel
{
    /// <summary>
    /// 可纠正约 7% 的码字损坏。
    /// </summary>
    L = 0,
    /// <summary>
    /// 可纠正约 15% 的码字损坏。
    /// </summary>
    M = 1,
    /// <summary>
    /// 可纠正约 25% 的码字损坏。
    /// </summary>
    Q = 2,
    /// <summary>
    /// 可纠正约 30% 的码字损坏。
    /// </summary>
    H = 3,
}
