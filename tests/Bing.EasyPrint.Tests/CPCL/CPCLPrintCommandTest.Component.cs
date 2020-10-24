using Xunit;

namespace Bing.EasyPrint.Tests.CPCL
{
    // ReSharper disable once InconsistentNaming
    partial class CPCLPrintCommandTest
    {
        [Fact]
        public void Test_DrawText_1()
        {
            Command.SetPage(400, 240)
                .DrawText(0, 0, "隔壁老王的战斗 with lao wang de zhan dou", 16, 0, false, false, false);
            Build();
        }
    }
}
