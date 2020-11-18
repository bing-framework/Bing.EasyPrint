using Bing.EasyPrint.CPCL;
using Xunit;
using Xunit.Abstractions;

namespace Bing.EasyPrint.Tests.CPCL
{
    /// <summary>
    /// 单位转换测试
    /// </summary>
    public class UnitConvertTest : TestBase
    {
        /// <summary>
        /// CPCL 打印命令
        /// </summary>
        protected CPCLPrintCommand Command { get; set; }

        /// <summary>
        /// 初始化一个<see cref="TestBase"/>类型的实例
        /// </summary>
        /// <param name="output">输出</param>
        public UnitConvertTest(ITestOutputHelper output) : base(output)
        {
            IEasyPrint print = new DefaultEasyPrint();
            Command = print.CreateCPCLCommand();
        }

        /// <summary>
        /// 构建
        /// </summary>
        private void Build()
        {
            var result = Command.Build();
            Output.WriteLine("----------------------------- 调试命令 ---------------------------------------");
            Output.WriteLine(result.ToString());
            Output.WriteLine("----------------------------- 调试命令-十六进制 ---------------------------------------");
            Output.WriteLine(result.ToHex());
            Output.WriteLine("----------------------------- 执行命令 ---------------------------------------");
            Print(result.GetBytes());
        }

        /// <summary>
        /// 测试 - 画文本
        /// </summary>
        [Fact]
        public void Test_DrawText_1()
        {
            // width: 78mm
            // left: 3mm
            // width - left * 8.3 == 实际距离
            Command.SetPage(624, 240)
                .DrawText(600, 0, "隔壁老王的战斗 with lao wang de zhan dou", 24, 0, false, false, false);
            Build();
        }
    }
}
