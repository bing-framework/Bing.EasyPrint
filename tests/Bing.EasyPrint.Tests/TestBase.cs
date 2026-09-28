using System.Text;
using Xunit.Abstractions;
using System;

namespace Bing.EasyPrint.Tests;

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
    protected bool Print(byte[] bytes) => !HardwarePrintEnabled || RawPrinterHelper.SendBytesToPrinter(PrinterName, bytes);

    /// <summary>
    /// 打印
    /// </summary>
    /// <param name="str">字符串</param>
    protected bool Print(string str) => !HardwarePrintEnabled || RawPrinterHelper.SendStringToPrinter(PrinterName, str);

    private static bool HardwarePrintEnabled => Environment.GetEnvironmentVariable("EASYPRINT_ENABLE_HARDWARE_PRINT") == "1";
    private static string PrinterName => Environment.GetEnvironmentVariable("EASYPRINT_PRINTER_NAME")
        ?? throw new InvalidOperationException("启用真实打印时必须设置 EASYPRINT_PRINTER_NAME。");
}
