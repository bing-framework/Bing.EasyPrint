using Xunit;

namespace Bing.EasyPrint.Tests.CPCL;

// ReSharper disable once InconsistentNaming
partial class CPCLPrintCommandTest
{
    [Fact]
    public void Test_Box()
    {
        Command.WriteRawLine("! 0 200 200 210 1")
            .PageWidth(500)
            .Box(0, 0, 200, 200, 1)
            .Form()
            .Print();
        Build();
    }

    [Fact]
    public void Test_Line()
    {
        Command.WriteRawLine("! 0 200 200 210 1")
            .Line(0, 0, 200, 0, 1)
            .Line(0, 0, 200, 200, 2)
            .Line(0, 0, 0, 200, 3)
            .Form()
            .Print();
        Build();
    }

    [Fact]
    public void Test_InverseLine_1()
    {
        Command.WriteRawLine("! 0 200 200 210 1")
            .Center()
            .Text(4, 0, 0, 45, "SAVE")
            .Text(4, 0, 0, 95, "MORE")
            .InverseLine(0, 45, 145, 45, 45)
            .InverseLine(0, 95, 145, 95, 45)
            .Form()
            .Print();
        Build();
    }

    [Fact]
    public void Test_InverseLine_2()
    {
        Command.WriteRawLine("! 0 200 200 210 1")
            .Center()
            .Text(4, 2, 30, 20, "$123.45")
            .Text(4, 2, 30, 70, "$678.90")
            .InverseLine(25, 40, 350, 40, 90)
            .Text(4, 2, 30, 120, "$432.10")
            .Form()
            .Print();
        Build();
    }

    [Fact]
    public void Test_Pattern()
    {
        Command.WriteRawLine("! 0 200 200 700 1")
            .Pattern(101)
            .Line(10, 10, 160, 10, 42)
            .Pattern(102)
            .Line(170, 10, 350, 10, 42)
            .Pattern(103)
            .Line(10, 65, 160, 65, 40)
            .Pattern(104)
            .Line(170, 65, 350, 65, 40)
            .Pattern(105)
            .Line(10, 115, 160, 115, 40)
            .Pattern(106)
            .Line(170, 115, 350, 115, 40)
            .Form()
            .Print();
        Build();
    }

    [Fact]
    public void Test_ExpandedGraphics()
    {
        Command.WriteRawLine("! 0 200 200 210 1")
            .ExpandedGraphics(2, 16, 90, 45, "F0F0F0F0F0F0F0F00F0F0F0F0F0F0F0FF0F0F0F0F0F0F0F00F0F0F0F0F0F0F0F")
            .Form()
            .Print();
        Build();
    }
}