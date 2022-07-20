using System.Text;

namespace Bing.EasyPrint
{
    /// <summary>
    /// 打印值描述符
    /// </summary>
    public interface IPrintValueDescriptor
    {
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
    }
}
