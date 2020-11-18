using System.Text;
using Xunit.Abstractions;

namespace Bing.EasyPrint.Tests
{
    /// <summary>
    /// 测试基类
    /// </summary>
    public abstract class TestBase
    {
        /// <summary>
        /// 输出
        /// </summary>
        protected ITestOutputHelper Output { get; }

        /// <summary>
        /// 初始化一个<see cref="TestBase"/>类型的实例
        /// </summary>
        /// <param name="output">输出</param>
        protected TestBase(ITestOutputHelper output)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            Output = output;
        }

        /// <summary>
        /// 打印
        /// </summary>
        /// <param name="bytes">字节数组</param>
        protected bool Print(byte[] bytes) => RawPrinterHelper.SendBytesToPrinter("Zicox CS4", bytes);

        /// <summary>
        /// 打印
        /// </summary>
        /// <param name="str">字符串</param>
        protected bool Print(string str) => RawPrinterHelper.SendStringToPrinter("Zicox CS4", str);
    }
}
