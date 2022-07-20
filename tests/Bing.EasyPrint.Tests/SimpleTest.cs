using System.Text.RegularExpressions;
using Xunit;

namespace Bing.EasyPrint.Tests
{
    /// <summary>
    /// 简单测试
    /// </summary>
    public class SimpleTest
    {
        /// <summary>
        /// 测试 - 初始化命令
        /// </summary>
        [Theory]
        [InlineData("! 0 200 200 210 1\r\n", true)]
        [InlineData("! 0 200 200 210 1", true)]
        [InlineData("! 0 200 200 210 1 AAA", false)]
        [InlineData("! 0 200 200 210 1\r\nAAA", false)]
        public void Test_InitCommand(string input, bool target)
        {
            var pattern = "^!\\s[0-9]\\s[0-9]+\\s[0-9]+\\s[0-9]+\\s[0-9]+\\s{0,1}$";
            var result = Regex.IsMatch(input, pattern);
            Assert.Equal(target, result);
        }

        /// <summary>
        /// 测试 - 打印命令
        /// </summary>
        [Theory]
        [InlineData("PRINT\r\n", true)]
        [InlineData("PRINT", true)]
        [InlineData("PRINT AAA", false)]
        [InlineData("PRINT\r\nAAA", false)]
        public void Test_PrintCommand(string input, bool target)
        {
            var pattern = "^PRINT\\s{0,1}$";
            var result = Regex.IsMatch(input, pattern);
            Assert.Equal(target, result);
        }

        /// <summary>
        /// 测试 - 页宽命令
        /// </summary>
        [Theory]
        [InlineData("PAGE-WIDTH 500\r\n", true)]
        [InlineData("PAGE-WIDTH 500", true)]
        [InlineData("PAGE-WIDTH 500 AAA", false)]
        [InlineData("PAGE-WIDTH 500\r\nAAA", false)]
        [InlineData("PW 500\r\n", true)]
        [InlineData("PW 500", true)]
        [InlineData("PW 500 AAA", false)]
        [InlineData("PW 500\r\nAAA", false)]
        public void Test_PageWidthCommand(string input, bool target)
        {
            var pattern = "^(PAGE-WIDTH|PW)\\s[0-9]+\\s{0,1}$";
            var result = Regex.IsMatch(input, pattern);
            Assert.Equal(target, result);
        }
    }
}
