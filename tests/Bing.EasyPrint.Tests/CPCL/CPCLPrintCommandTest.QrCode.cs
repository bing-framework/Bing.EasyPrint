using Xunit;

namespace Bing.EasyPrint.Tests.CPCL;

// ReSharper disable once InconsistentNaming
partial class CPCLPrintCommandTest
{
    [Fact]
    public void Test_QrCode_1()
    {
        Command.WriteRawLine("! 0 200 200 500 1")
            .QRCode(10, 100, 2, 10, "M", null, "QR code ABC123")
            .Text(4, 0, 10, 400, "QR code ABC123")
            .Form()
            .Print();
        Build();
    }

    [Fact]
    public void Test_Aztec()
    {
        Command.WriteRawLine("! 0 200 200 600 1")
            .Text(7, 0, 50, 0, "Aztec Code - Label Spec 5-1 EC=47")
            .Aztec(50, 100, 7, 47, "123456789012")
            .Print();
        Build();
    }

    [Fact]
    public void Test_BarcodeRss()
    {
        Command.WriteRawLine("! 0 200 200 300 1")
            .Text("5", 0, 10, 40, "RSS14 Composite")
            .BarcodeRss(10, 110, 2, 25, 3, 22, 5, "1234567890123", "1234567890")
            .Print();
        Build();
    }

    [Fact]
    public void Test_Pdf417()
    {
        Command.WriteRawLine("! 0 200 200 300 1")
            .Text("5", 0, 10, 40, "RSS14 Composite")
            .Pdf417(10, 20, 3, 12, 3, 2, "PDF Data", "ABCDE12345")
            .Print();
        Build();
    }

    [Fact]
    public void Test_Maxicode()
    {
        Command.WriteRawLine("! 0 200 200 600 1")
            .WriteRawLine("JOURNAL")
            .Maxicode(20, 20, ("CC", "12345"), ("MSG", "This is a MAXICODE low priority message."), ("SC", "12345"), ("POST", "02886"))
            .Print();
        Build();
    }
}