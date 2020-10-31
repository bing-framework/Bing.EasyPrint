using System.ComponentModel;

namespace Bing.EasyPrint.Tests.Biz
{
    /// <summary>
    /// 打印纸类型
    /// </summary>
    public enum PrintPageType
    {
        /// <summary>
        /// 15mm 打印纸
        /// </summary>
        [Description("15mm")]
        // ReSharper disable once InconsistentNaming
        Page15mm = 15,
        /// <summary>
        /// 70mm 打印纸
        /// </summary>
        [Description("70mm")]
        // ReSharper disable once InconsistentNaming
        Page70mm = 70,
        /// <summary>
        /// 90mm 打印纸
        /// </summary>
        [Description("90mm")]
        // ReSharper disable once InconsistentNaming
        Page90mm = 90,
    }
}
