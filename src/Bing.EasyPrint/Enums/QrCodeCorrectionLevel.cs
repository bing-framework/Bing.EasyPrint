// ReSharper disable once CheckNamespace
namespace Bing.EasyPrint
{
    /// <summary>
    /// 二维码纠错级别
    /// </summary>
    public enum QrCodeCorrectionLevel
    {
        /// <summary>
        /// 7%的字码可被修正
        /// </summary>
        L = 0,
        /// <summary>
        /// 15%的字码可被修正
        /// </summary>
        M = 1,
        /// <summary>
        /// 25%的字码可被修正
        /// </summary>
        Q = 2,
        /// <summary>
        /// 30%的字码可被修正
        /// </summary>
        H = 3,
    }
}
