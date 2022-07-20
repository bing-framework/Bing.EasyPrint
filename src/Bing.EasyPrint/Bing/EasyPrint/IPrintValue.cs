using System.Text;

namespace Bing.EasyPrint
{
    /// <summary>
    /// 打印值
    /// </summary>
    public interface IPrintValue
    {
        /// <summary>
        /// 数据
        /// </summary>
        byte[] Data { get; }

        /// <summary>
        /// 获取字符串
        /// </summary>
        string GetString();

        /// <summary>
        /// 获取字符串
        /// </summary>
        /// <param name="encoding">字符编码</param>
        string GetString(Encoding encoding);

        /// <summary>
        /// 获取16进制字符串
        /// </summary>
        string GetHexString();

        /// <summary>
        /// 获取16进制字符串
        /// </summary>
        /// <param name="uppercase">是否大写字母</param>
        string GetHexString(bool uppercase);

        /// <summary>
        /// 获取Base64字符串
        /// </summary>
        string GetBase64String();
    }
}
