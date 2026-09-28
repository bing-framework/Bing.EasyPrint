using System;
using System.Text;
using Bing.EasyPrint.CPCL;
using Xunit;

namespace Bing.EasyPrint.Tests;

public class ComponentBuildRegressionTests
{
    private static CPCLPrintCommand Command() => new CPCLPrintCommand(Encoding.ASCII).SetPage(200, 300);

    [Fact]
    public void FailedSplitLineBuildPreservesLastSuccessAndCanRetry()
    {
        var command = Command().DrawText(0, 0, "existing", 24, 0, false, false, false);
        var successfulWriter = command.Build();
        var successfulBytes = successfulWriter.GetBytes();
        var invalidSplitLine = new SplitLineComponent { X = 201, Y = 20 };
        command.Content.Add(invalidSplitLine);

        Assert.Throws<ArgumentOutOfRangeException>(() => command.Build());
        Assert.Same(successfulWriter, command.Writer);
        Assert.Equal(successfulBytes, command.Writer.GetBytes());

        invalidSplitLine.X = 10;
        var recovered = command.Build().GetBytes();
        Assert.NotEqual(successfulBytes, recovered);
        Assert.Contains("existing", Encoding.ASCII.GetString(recovered));
    }

    [Theory]
    [InlineData(0, 60)]
    [InlineData(60, 0)]
    public void DrawTextArea_RejectsZeroDimensionsWithoutChangingCommand(int width, int height)
    {
        var command = Command().DrawText(0, 0, "existing", 24, 0, false, false, false);
        var expected = command.Build().GetBytes();

        Assert.Throws<ArgumentException>(() => command.DrawTextArea(
            0, 40, width, height, "candidate", FontSize.Size24, RotationAngle.None, TextStyle.None));

        Assert.Equal(expected, command.Build().GetBytes());
    }

    [Fact]
    public void DrawTextArea_WrapsTextAtTheConfiguredWidth()
    {
        var output = Encoding.ASCII.GetString(Command()
            .DrawTextArea(10, 20, 24, 100, "ABCD", FontSize.Size24, RotationAngle.None, TextStyle.None)
            .Build().GetBytes());

        Assert.Contains(" 10 20 AB", output);
        Assert.Contains(" 10 46 CD", output);
    }
}
