using System.Text;

namespace Bing.EasyPrint;

/// <summary>
/// 将打印数据转换为文本表示的描述符。
/// </summary>
public interface IPrintValueDescriptor
{
    /// <summary>
    /// 使用默认编码获取打印数据的字符串表示。
    /// </summary>
    /// <returns>解码后的字符串。</returns>
    string GetString();

    /// <summary>
    /// 使用指定编码获取打印数据的字符串表示。
    /// </summary>
    /// <param name="encoding">用于解码打印数据的字符编码。</param>
    /// <returns>解码后的字符串。</returns>
    string GetString(Encoding encoding);

    /// <summary>
    /// 获取打印数据的十六进制字符串。
    /// </summary>
    /// <returns>十六进制字符串。</returns>
    string GetHexString();

    /// <summary>
    /// 获取打印数据的十六进制字符串。
    /// </summary>
    /// <param name="uppercase">为 true 时使用大写字母，为 false 时使用小写字母。</param>
    /// <returns>按指定大小写表示的十六进制字符串。</returns>
    string GetHexString(bool uppercase);
}
