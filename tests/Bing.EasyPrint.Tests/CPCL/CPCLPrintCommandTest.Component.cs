using Xunit;

namespace Bing.EasyPrint.Tests.CPCL
{
    // ReSharper disable once InconsistentNaming
    partial class CPCLPrintCommandTest
    {
        /// <summary>
        /// 测试 - 画文本
        /// </summary>
        [Fact]
        public void Test_DrawText_1()
        {
            Command.SetPage(400, 240)
                .DrawText(0, 0, "隔壁老王的战斗 with lao wang de zhan dou", 16, 0, false, false, false);
            Build();
        }

        /// <summary>
        /// 测试 - 画线条
        /// </summary>
        [Fact]
        public void Test_DrawLine_1()
        {
            Command.SetPage(200, 210)
                .DrawLine(0, 0, 200, 0, 1)
                .DrawLine(0, 0, 200, 200, 2)
                .DrawLine(0, 0, 0, 200, 3);
            Build();
        }

        /// <summary>
        /// 测试 - 画虚线
        /// </summary>
        [Fact]
        public void Test_DrawDashLine_1()
        {
            Command.SetPage(600, 200)
                .DrawDashLine(0, 10, 595, 5);
            Build();
        }

        /// <summary>
        /// 测试 - 画矩形
        /// </summary>
        [Fact]
        public void Test_DrawRect_1()
        {
            Command.SetPage(400, 210)
                .DrawRect(0, 0, 200, 200, 1)
                .DrawRect(200, 0, 400, 200, 1);
            Build();
        }

        /// <summary>
        /// 测试 - 画条码
        /// </summary>
        [Fact]
        public void Test_DrawBarcode_1()
        {
            Command.SetPage(400, 210)
                .DrawBarcode1D("128", 150, 10, "HORIZ.", 1, 50, 0, 1)
                .DrawText(210, 60, "HORIZ.", 16, 0, false, false, false)
                .DrawBarcode1D("128", 10, 200, "VERT.", 1, 50, 90, 1)
                .DrawText(60, 140, "VERT.", 16,  90, false, false, false);
            Build();
        }

        /// <summary>
        /// 测试 - 画条码
        /// </summary>
        [Fact]
        public void Test_DrawBarcode_2()
        {
            Command.SetPage(600, 350)
                .DrawBarcode(0, 0, "10000000256", 1, 100)
                .DrawBarcode(0, 150, "10000000257", 2, 100);
            Build();
        }

        /// <summary>
        /// 测试 - 画二维码
        /// </summary>
        [Fact]
        public void Test_DrawQrCode_1()
        {
            Command.SetPage(400, 500)
                .DrawQrCode(10, 100, "ABC123", 10, "M", 0);
            Build();
        }
    }
}
