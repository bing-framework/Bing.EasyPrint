using Bing.EasyPrint.CPCL;
using Xunit.Abstractions;
using Xunit;

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
        Assert.NotEmpty(result.GetBytes());
        Assert.StartsWith("! ", result.ToString());
        Assert.Contains("PAGE-WIDTH", result.ToString());
        Assert.EndsWith("PRINT\r\n", result.ToString());
        Assert.Equal(result.GetBytes(), Command.Build().GetBytes());
        Output.WriteLine("----------------------------- 调试命令 ---------------------------------------");
        Output.WriteLine(result.ToString());
        Output.WriteLine("----------------------------- 调试命令-十六进制 ---------------------------------------");
        Output.WriteLine(result.ToHex());
        Output.WriteLine("----------------------------- 执行命令 ---------------------------------------");
        Assert.True(Print(result.GetBytes()));
    }
}
