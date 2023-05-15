using System.Linq;
using System.Text;
using Bing.EasyPrint.Core;
using Xunit;
using Xunit.Abstractions;

namespace Bing.EasyPrint.Tests;

/// <summary>
/// 缓冲区写入器 测试
/// </summary>
public class BufferWriterTest : TestBase
{
    /// <summary>
    /// 字符编码
    /// </summary>
    private readonly Encoding _encoding;

    /// <summary>
    /// 缓冲区写入器
    /// </summary>
    private readonly IBufferWriter _writer;

    /// <summary>
    /// 初始化一个<see cref="BufferWriterTest"/>类型的实例
    /// </summary>
    /// <param name="output">输出</param>
    public BufferWriterTest(ITestOutputHelper output) : base(output)
    {
        _encoding = Encoding.GetEncoding("gbk");
        _writer = new BufferWriter(_encoding);
    }

    /// <summary>
    /// 测试 - 字符串是否匹配字节数组
    /// </summary>
    [Fact]
    public void Test_StringEqualBytes()
    {
        var input = "! 0 200 200 210 1";
        _writer.Write(input);
        var original = _writer.GetBytes();
        var target = GetBytes(input);
        Assert.True(original.SequenceEqual(target));
    }

    /// <summary>
    /// 获取字节数组
    /// </summary>
    /// <param name="value">值</param>
    private byte[] GetBytes(string value) => _encoding.GetBytes(value);
}