using Bing.EasyPrint.CPCL;
using Xunit.Abstractions;

namespace Bing.EasyPrint.Tests.CPCL;

/// <summary>
/// CPCL 打印命令测试
/// </summary>
// ReSharper disable once InconsistentNaming
public partial class CPCLPrintCommandTest : TestBase
{
    /// <summary>
    /// CPCL 打印命令
    /// </summary>
    protected CPCLPrintCommand Command { get; set; }

    /// <summary>
    /// 初始化一个<see cref="TestBase"/>类型的实例
    /// </summary>
    /// <param name="output">输出</param>
    public CPCLPrintCommandTest(ITestOutputHelper output) : base(output)
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
}