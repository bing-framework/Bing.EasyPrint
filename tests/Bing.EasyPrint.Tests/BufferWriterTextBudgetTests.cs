using System;
using System.Linq;
using System.Text;
using Bing.EasyPrint.Core;
using Bing.EasyPrint.Printing;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>验证 BufferWriter 原始文本预算的保守计费和失败回收。</summary>
public sealed class BufferWriterTextBudgetTests
{
    [Fact]
    public void Text_budget_rejection_preserves_existing_bytes_and_sources()
    {
        using (var scope = PrintResourceScope.Begin(260))
        {
            var writer = new BufferWriter(Encoding.ASCII);
            writer.Write("A");
            var before = scope.CurrentBytes;

            var error = Assert.Throws<PrintResourceLimitException>(() => writer.Write("BBBB"));

            Assert.Equal("Command.SourceText", error.ResourceName);
            Assert.Equal(before, scope.CurrentBytes);
            Assert.Equal("A", writer.ToString());
            Assert.Equal(new byte[] { (byte)'A' }, writer.GetBytes());
        }
    }

    [Fact]
    public void Clear_releases_text_leases_and_allows_a_retry()
    {
        using (var scope = PrintResourceScope.Begin(300))
        {
            var writer = new BufferWriter(Encoding.ASCII);
            writer.Write(new string('A', 20));
            Assert.Equal(296, scope.CurrentBytes);

            writer.Clear();

            Assert.Equal(256, scope.CurrentBytes);
            writer.Write("retry");
            Assert.Equal("retry", writer.ToString());
            Assert.Equal(266, scope.CurrentBytes);
        }
    }

    [Fact]
    public void Encoding_failure_releases_source_text_budget_in_the_same_scope()
    {
        var strictAscii = (Encoding)Encoding.ASCII.Clone();
        strictAscii.EncoderFallback = EncoderFallback.ExceptionFallback;
        using (var scope = PrintResourceScope.Begin(300))
        {
            var writer = new BufferWriter(strictAscii);

            Assert.Throws<EncoderFallbackException>(() => writer.Write("é"));
            Assert.Equal(256, scope.CurrentBytes);

            writer.Write("OK");
            Assert.Equal("OK", writer.ToString());
            Assert.Equal(260, scope.CurrentBytes);
        }
    }

    [Fact]
    public void Unicode_text_is_charged_by_utf16_length_and_none_scope_bytes_remain_unchanged()
    {
        var value = "😀";
        using (var scope = PrintResourceScope.Begin(260))
        {
            var writer = new BufferWriter(Encoding.UTF8);
            writer.Write(value);

            // The emoji is two UTF-16 code units, so its conservative source
            // reservation is four bytes in addition to the initial buffer.
            Assert.Equal(260, scope.CurrentBytes);
            Assert.Equal(value, writer.ToString());
        }

        var expected = Encoding.UTF8.GetBytes(value);
        var actual = new BufferWriter(Encoding.UTF8).Write(value).GetBytes();
        Assert.True(expected.SequenceEqual(actual));
    }
}
