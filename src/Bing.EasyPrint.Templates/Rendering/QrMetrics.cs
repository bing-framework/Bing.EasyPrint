using System;
using System.Text;

namespace Bing.EasyPrint.Templates;

/// <summary>
/// 估算 QR Code Model 2 的打印尺寸。
/// </summary>
internal static class QrMetrics
{
    /// <summary>
    /// 获取各版本在 L、M、Q、H 纠错等级下的可用数据位数。
    /// </summary>
    // 每版本可用数据位，列为 L/M/Q/H。来源：DENSO WAVE 官方版本容量表。
    // https://www.qrcode.com/en/about/version.html （versionPage1_10 至 versionPage31_40）
    private static readonly int[,] DataBits = {
        {152,128,104,72}, {272,224,176,128}, {440,352,272,208}, {640,512,384,288},
        {864,688,496,368}, {1088,864,608,480}, {1248,992,704,528}, {1552,1232,880,688},
        {1856,1456,1056,800}, {2192,1728,1232,976}, {2592,2032,1440,1120}, {2960,2320,1648,1264},
        {3424,2672,1952,1440}, {3688,2920,2088,1576}, {4184,3320,2360,1784}, {4712,3624,2600,2024},
        {5176,4056,2936,2264}, {5768,4504,3176,2504}, {6360,5016,3560,2728}, {6888,5352,3880,3080},
        {7456,5712,4096,3248}, {8048,6256,4544,3536}, {8752,6880,4912,3712}, {9392,7312,5312,4112},
        {10208,8000,5744,4304}, {10960,8496,6032,4768}, {11744,9024,6464,5024}, {12248,9544,6968,5288},
        {13048,10136,7288,5608}, {13880,10984,7880,5960}, {14744,11640,8264,6344}, {15640,12328,8920,6760},
        {16568,13048,9368,7208}, {17528,13800,9848,7688}, {18448,14496,10288,7888}, {19472,15312,10832,8432},
        {20528,15936,11408,8768}, {21616,16816,12016,9136}, {22496,17728,12656,9776}, {23648,18672,13328,10208}
    };

    /// <summary>
    /// 根据内容容量计算包含静区的二维码边长。
    /// </summary>
    /// <param name="text">待编码内容。</param>
    /// <param name="encoding">字节模式所用编码。</param>
    /// <param name="level">L、M、Q 或 H 纠错等级。</param>
    /// <param name="unit">每模块的打印点数。</param>
    /// <returns>包含四周静区的打印点边长。</returns>
    internal static int Extent(string text, Encoding encoding, string level, int unit)
    {
        if (text.Length == 0) throw new ArgumentException("二维码内容不能为空。");
        var numeric = true;
        var alphanumeric = true;
        foreach (var c in text)
        {
            numeric &= c >= '0' && c <= '9';
            alphanumeric &= "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ $%*+-./:".IndexOf(c) >= 0;
        }
        var levelIndex = "LMQH".IndexOf(level, StringComparison.Ordinal);
        if (level.Length != 1 || levelIndex < 0 || unit < 1 || unit > 32) throw new ArgumentException("二维码参数无效。");
        var count = numeric || alphanumeric ? text.Length : encoding.GetByteCount(text);
        var bits = numeric ? count / 3L * 10 + (count % 3 == 0 ? 0 : count % 3 == 1 ? 4 : 7) :
            alphanumeric ? count / 2L * 11 + count % 2 * 6 : count * 8L;
        for (var version = 1; version <= 40; version++)
        {
            var group = version < 10 ? 0 : version < 27 ? 1 : 2;
            var countBits = numeric ? 10 + group * 2 : alphanumeric ? 9 + group * 2 : group == 0 ? 8 : 16;
            if (count < (1 << countBits) && bits + 4 + countBits <= DataBits[version - 1, levelIndex])
                return checked((17 + 4 * version + 8) * unit); // 四边各四模块静区。
        }
        throw new ArgumentException("二维码数据超出 Model 2 容量。");
    }
}
