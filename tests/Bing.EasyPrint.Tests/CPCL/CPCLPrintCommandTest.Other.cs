using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Bing.EasyPrint.Tests.CPCL;

// ReSharper disable once InconsistentNaming
partial class CPCLPrintCommandTest
{
    [Fact]
    public void Test_GetLineMaxLength()
    {
        var cLength = GetLineMaxLength(77 * 8, 12);
        var sb = new StringBuilder();
        for (var i = 0; i < cLength; i++) 
            sb.Append("-");

        Command
            .InitCommand(0, 78*8, 200, 1)
            //.WriteRawLine("SETMAG 1 1")
            .WriteRawLine($"T 03 0 0 20 {sb.ToString()}")
            //.WriteRawLine("SETMAG 0 0")
            .Print();
        Build();
    }

    private static int GetLineMaxLength(int width, int fontSize) => width / fontSize;
}