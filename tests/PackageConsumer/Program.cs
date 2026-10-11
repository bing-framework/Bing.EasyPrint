using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Bing.EasyPrint;
using Bing.EasyPrint.CPCL;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;
using Bing.EasyPrint.Templates;

internal static class Program
{
    private const string TemplateText = "ASCII-PACK";
    private const string ModelText = "MODEL-OUTPUT";

    private static int Main(string[] args)
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var runtimeHost = ReadOption(args, "--runtime-host") ?? "<unspecified>";
            Console.WriteLine($"RuntimeHost={runtimeHost}");
            Console.WriteLine($"FrameworkDescription={RuntimeInformation.FrameworkDescription}");
            Console.WriteLine($"CoreLibLocation={typeof(object).Assembly.Location}");

            var fontPath = ReadOption(args, "--font-path") ??
                Path.Combine(AppContext.BaseDirectory, "DejaVuSans.ttf");

            Require(File.Exists(fontPath), $"固定 DejaVu 字体资源不存在: {fontPath}");
            var fonts = CreateFixedFonts(fontPath);

            VerifyLegacyCpclAndDefaultTemplate();
            VerifyProtocolFactories();
            VerifyTemplateAcrossProtocols(fonts);
            VerifyModelProviderOutputs(fonts);
            VerifyHumanReadableOutputs(fonts);
            VerifyFilledRectangleOutputs(fonts);
            VerifyZplDelayedOutput();
            VerifyImageRotationOutputs();

            Console.WriteLine("Package consumer verification PASS");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("Package consumer verification FAILED");
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    private static void VerifyImageRotationOutputs()
    {
        foreach (var protocol in new[] { PrintProtocol.Cpcl, PrintProtocol.Tspl, PrintProtocol.Zpl, PrintProtocol.EscPos })
        {
            var document = new PrintDocument();
            document.AddPage(24, 12).Add(new PrintImageElement(2, 1, 3, 2,
                new byte[] { 0xA0, 0xC0 }, RotationAngle.Rotate90), "Logo");
            var golden = new PrintDocument();
            golden.AddPage(24, 12).Add(new PrintImageElement(2, 1, 2, 3,
                new byte[] { 0xC0, 0x80, 0x40 }));
            var provider = PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII, EscPosLayoutMode.Raster);
            Require(provider.Capabilities.SupportsImageRotation, $"图片旋转能力: {protocol}");
            RequireBytesEqual(Single(provider.Render(golden), "预旋转图片"),
                Single(provider.Render(document), "图片旋转"), $"图片旋转字节: {protocol}");
        }
    }

    private static void VerifyLegacyCpclAndDefaultTemplate()
    {
        IEasyPrint easyPrint = new DefaultEasyPrint();
        var command = easyPrint.CreateCommand<CPCLPrintCommand>();
        command.InitCommand(0, 240, 64, 1);
        command.WriteRawLine("LEGACY-CPCL");

        var first = command.Build().GetBytes();
        var second = command.Build().GetBytes();
        var expected = Encoding.GetEncoding("gbk").GetBytes(
            "! 0 200 200 64 1\r\nPAGE-WIDTH 240\r\nLEGACY-CPCL\r\nPRINT\r\n");
        RequireBytesEqual(expected, first, "旧 IEasyPrint CPCL 字节快照");
        RequireBytesEqual(first, second, "CPCL Build 幂等性");

        var loader = new TemplateLoader();
        var template = loader.LoadJson(SimpleTemplateJson(), new TemplateRenderOptions { Encoding = Encoding.ASCII });
        var defaultResult = new TemplateRenderer().Render(template, null);
        var explicitResult = new TemplateRenderer().Render(template, null, options: new TemplateRenderOptions
        {
            Protocol = PrintProtocol.Cpcl,
            Encoding = Encoding.ASCII,
            FeedLines = 0,
            CutAfterPrint = false
        });

        Require(defaultResult.Protocol == PrintProtocol.Cpcl, "默认模板协议应保持 CPCL");
        Require(defaultResult.PageCount == 1 && explicitResult.PageCount == 1,
            "默认模板应生成一个 CPCL 页面");
        RequireBytesEqual(defaultResult.Pages[0].Data, explicitResult.Pages[0].Data,
            "默认模板与显式 CPCL 模板输出兼容性");
        RequireAscii(defaultResult.Pages[0].Data, TemplateText, "默认模板 CPCL 文本片段");
    }

    private static void VerifyProtocolFactories()
    {
        var esc = (EscPosPrintCommand)ProtocolCommandFactory.Create(
            PrintProtocol.EscPos, Encoding.ASCII, EscPosLayoutMode.Native);
        var escBytes = esc.Initialize().Text(TemplateText).Build().GetBytes();
        var expectedEsc = new byte[] { 0x1B, 0x40 }
            .Concat(Encoding.ASCII.GetBytes(TemplateText + "\n"))
            .ToArray();
        Require(esc.Protocol == PrintProtocol.EscPos, "ESC/POS 工厂协议标识");
        RequireBytesEqual(expectedEsc, escBytes, "ESC/POS 工厂真实字节");

        var tspl = (TsplPrintCommand)ProtocolCommandFactory.Create(PrintProtocol.Tspl, Encoding.ASCII);
        var tsplBytes = tspl.Size(60, 40, "dot").Text(1, 2, TemplateText).Print().Build().GetBytes();
        Require(tspl.Protocol == PrintProtocol.Tspl, "TSPL 工厂协议标识");
        RequireAscii(tsplBytes, "SIZE 60 dot,40 dot\r\n", "TSPL SIZE 字节片段");
        RequireAscii(tsplBytes, "TEXT 1,2,\"0\",0,1,1,\"ASCII-PACK\"\r\n", "TSPL TEXT 字节片段");
        RequireAscii(tsplBytes, "PRINT 1,1\r\n", "TSPL PRINT 字节片段");

        var zpl = (ZplPrintCommand)ProtocolCommandFactory.Create(PrintProtocol.Zpl, Encoding.ASCII);
        var zplBytes = zpl.Start().Label(60, 40).Text(1, 2, TemplateText).Print().End().Build().GetBytes();
        var expectedZpl = "^XA\r\n^PW60\r\n^LL40\r\n" +
            "^FO1,2^A0N,30,30^FDASCII-PACK^FS\r\n^PQ1\r\n^XZ\r\n";
        Require(zpl.Protocol == PrintProtocol.Zpl, "ZPL 工厂协议标识");
        RequireAsciiEqual(expectedZpl, zplBytes, "ZPL 工厂真实字节");
    }

    private static void VerifyTemplateAcrossProtocols(RasterFontCatalog fonts)
    {
        var template = new TemplateLoader().LoadJson(SimpleTemplateJson(),
            new TemplateRenderOptions { Encoding = Encoding.ASCII });
        var renderer = new TemplateRenderer();

        foreach (var protocol in new[]
        {
            PrintProtocol.Cpcl,
            PrintProtocol.EscPos,
            PrintProtocol.Tspl,
            PrintProtocol.Zpl
        })
        {
            var result = renderer.RenderWithLayout(template, null, options: OptionsFor(protocol, fonts));
            Require(result.Protocol == protocol, $"模板协议标识: {protocol}");
            Require(result.PageCount == 1, $"模板页数: {protocol}");

            var page = result.Pages[0];
            Require(page.Width == 240 && page.Height == 64, $"模板页模型尺寸: {protocol}");
            Require(page.Elements.Any(element => element.Text == TemplateText),
                $"模板布局文字: {protocol}");
            Require(page.ByteLength > 0, $"模板输出非空: {protocol}");

            switch (protocol)
            {
                case PrintProtocol.Cpcl:
                    RequireAscii(page.Data, "! 0 200 200 64", "CPCL 模板页面字节");
                    RequireAscii(page.Data, TemplateText, "CPCL 模板正文");
                    break;
                case PrintProtocol.EscPos:
                    RequireSequence(page.Data, new byte[] { 0x1D, 0x76, 0x30 },
                        "ESC/POS Raster 模板光栅字节");
                    break;
                case PrintProtocol.Tspl:
                    RequireAscii(page.Data, "SIZE 240 dot,64 dot", "TSPL 模板页面字节");
                    RequireAscii(page.Data, TemplateText, "TSPL 模板正文");
                    break;
                case PrintProtocol.Zpl:
                    RequireAscii(page.Data, "^XA", "ZPL 模板页面开始字节");
                    RequireAscii(page.Data, "^FDASCII-PACK^FS", "ZPL 模板正文");
                    break;
            }
        }
    }

    private static void VerifyModelProviderOutputs(RasterFontCatalog fonts)
    {
        foreach (var protocol in new[]
        {
            PrintProtocol.Cpcl,
            PrintProtocol.EscPos,
            PrintProtocol.Tspl,
            PrintProtocol.Zpl
        })
        {
            var document = new PrintDocument();
            var page = document.AddPage(240, 64);
            page.FeedLines = 0;
            page.CutAfterPrint = false;
            var text = protocol == PrintProtocol.EscPos
                ? new PrintTextElement(0, 0, ModelText, 24,
                    new PrintFont(PrintFontKind.Host, "DejaVu"))
                : new PrintTextElement(0, 0, ModelText, 24);
            page.Add(text, "Model.Text");

            var provider = protocol == PrintProtocol.EscPos
                ? PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII, EscPosLayoutMode.Raster,
                    new EscPosDeviceOptions { RasterFonts = fonts })
                : PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII);
            var bytes = Single(provider.Render(document), $"模型页面: {protocol}");
            var outputs = Single(provider.RenderOutput(document), $"模型输出: {protocol}");

            RequireBytesEqual(bytes, outputs.Data, $"模型 Render 与 RenderOutput: {protocol}");
            Require(outputs.Segments.Count == 1, $"模型输出段数: {protocol}");
            Require(outputs.Segments[0].Kind == PrintOutputSegmentKind.Commands,
                $"模型输出段类型: {protocol}");

            switch (protocol)
            {
                case PrintProtocol.Cpcl:
                    RequireAscii(bytes, ModelText, "CPCL 模型正文");
                    break;
                case PrintProtocol.EscPos:
                    RequireSequence(bytes, new byte[] { 0x1D, 0x76, 0x30 },
                        "ESC/POS Raster 模型光栅字节");
                    break;
                case PrintProtocol.Tspl:
                    RequireAscii(bytes, "TEXT 0,0,\"0\"", "TSPL 模型文字指令");
                    RequireAscii(bytes, ModelText, "TSPL 模型正文");
                    break;
                case PrintProtocol.Zpl:
                    RequireAscii(bytes, "^FDMODEL-OUTPUT^FS", "ZPL 模型正文");
                    break;
            }
        }
    }

    private static void VerifyHumanReadableOutputs(RasterFontCatalog fonts)
    {
        var cpcl = new PrintDocument();
        cpcl.AddPage(400, 100).Add(new PrintBarcodeElement(0, 0, BarcodeType.Code128,
            "12345", 40, humanReadable: true, humanReadableReserveHeight: 20), "Barcode");
        var cpclProvider = PrintProtocolProviderFactory.Create(PrintProtocol.Cpcl, Encoding.ASCII);
        var cpclBytes = Single(cpclProvider.Render(cpcl), "CPCL HRI");
        Require(cpclProvider.Capabilities.SupportsBarcodeHumanReadable, "CPCL HRI 能力");
        RequireAscii(cpclBytes, "BT 0 24 0", "CPCL HRI 开启字节");
        RequireAscii(cpclBytes, "BT OFF", "CPCL HRI 恢复字节");

        var raster = new PrintDocument();
        raster.AddPage(400, 100).Add(new PrintBarcodeElement(0, 0, BarcodeType.Code128,
            "12345", 40, humanReadable: true, humanReadableReserveHeight: 20), "Barcode");
        var rasterProvider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster, new EscPosDeviceOptions { RasterFonts = fonts });
        var rasterBytes = Single(rasterProvider.Render(raster), "ESC/POS Raster HRI");
        Require(rasterProvider.Capabilities.SupportsBarcodeHumanReadable, "ESC/POS Raster HRI 能力");
        Require(rasterProvider.Capabilities.RasterBarcodeHumanReadableFontSize == 12,
            "ESC/POS Raster HRI 默认字号");
        RequireSequence(rasterBytes, new byte[] { 0x1D, 0x76, 0x30 }, "ESC/POS Raster HRI 光栅字节");

        var configured = new PrintDocument();
        configured.AddPage(400, 120).Add(new PrintBarcodeElement(0, 0, BarcodeType.Code128,
            "12345", 40, humanReadable: true, humanReadableReserveHeight: 30), "Barcode");
        var configuredProvider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster, new EscPosDeviceOptions
            {
                RasterFonts = fonts,
                RasterBarcodeHumanReadableFontSize = 16
            });
        var configuredBytes = Single(configuredProvider.Render(configured), "ESC/POS Raster HRI 配置字号");
        Require(configuredProvider.Capabilities.RasterBarcodeHumanReadableFontSize == 16,
            "ESC/POS Raster HRI 配置字号能力");
        Require(!rasterBytes.SequenceEqual(configuredBytes), "ESC/POS Raster HRI 配置字号输出差异");
    }

    private static void VerifyFilledRectangleOutputs(RasterFontCatalog fonts)
    {
        var tspl = new TsplPrintCommand(Encoding.ASCII);
        var tsplBytes = tspl.FillRectangle(2, 4, 8, 3).Build().GetBytes();
        RequireAsciiEqual("BAR 2,4,8,3\r\n", tsplBytes,
            "TSPL 填充矩形公共命令字节");
        RequireBytesEqual(tsplBytes, tspl.Build().GetBytes(),
            "TSPL 填充矩形 Build 幂等性");

        var zpl = new ZplPrintCommand(Encoding.ASCII);
        var zplBytes = zpl.FillRectangle(2, 4, 8, 3).Build().GetBytes();
        RequireAsciiEqual("^FO2,4^GB8,3,3,B,0^FS\r\n", zplBytes,
            "ZPL 填充矩形公共命令字节");
        RequireBytesEqual(zplBytes, zpl.Build().GetBytes(),
            "ZPL 填充矩形 Build 幂等性");

        var document = new PrintDocument();
        var page = document.AddPage(10, 7);
        page.FeedLines = 0;
        page.CutAfterPrint = false;
        page.Add(PrintRectangleElement.CreateFilled(2, 4, 8, 3), "Filled");

        foreach (var protocol in new[]
        {
            PrintProtocol.Cpcl,
            PrintProtocol.Tspl,
            PrintProtocol.Zpl,
            PrintProtocol.EscPos
        })
        {
            var provider = CreateFilledProvider(protocol);
            Require(provider.Capabilities.SupportsFilledRectangles,
                $"填充矩形能力: {protocol}");
            if (protocol == PrintProtocol.Tspl || protocol == PrintProtocol.Zpl)
            {
                Require(!provider.Capabilities.SupportsImages,
                    $"{protocol} 填充矩形不依赖图片能力");
                Require(provider.Capabilities.SupportsGraphics,
                    $"{protocol} 填充矩形图形能力");
            }

            var first = Single(provider.Render(document), $"填充矩形模型页面: {protocol}");
            var second = Single(provider.Render(document), $"填充矩形模型重复页面: {protocol}");
            RequireBytesEqual(first, second, $"填充矩形 Provider 输出幂等性: {protocol}");
            switch (protocol)
            {
                case PrintProtocol.Cpcl:
                    RequireAscii(first, "L 2 4 9 4 3\r\n", "CPCL 精确实心填充字节");
                    break;
                case PrintProtocol.Tspl:
                    RequireAscii(first, "BAR 2,4,8,3\r\n", "TSPL Provider 填充矩形字节");
                    break;
                case PrintProtocol.Zpl:
                    RequireAscii(first, "^FO2,4^GB8,3,3,B,0^FS\r\n", "ZPL Provider 填充矩形字节");
                    break;
                case PrintProtocol.EscPos:
                    RequireSequence(first, new byte[] { 0x1B, 0x40, 0x1D, 0x76, 0x30 },
                        "ESC/POS Raster 填充矩形光栅字节");
                    RequireSequence(first, new byte[] { 0x3F, 0xC0, 0x3F, 0xC0, 0x3F, 0xC0 },
                        "ESC/POS Raster 填充矩形黑点数据");
                    break;
            }
        }

        var loader = new TemplateLoader();
        var jsonOptions = new TemplateRenderOptions { Encoding = Encoding.ASCII };
        var template = loader.LoadJson(loader.SaveJson(CreateFilledV2Template(), jsonOptions), jsonOptions);
        var renderer = new TemplateRenderer();
        foreach (var protocol in new[]
        {
            PrintProtocol.Cpcl,
            PrintProtocol.Tspl,
            PrintProtocol.Zpl,
            PrintProtocol.EscPos
        })
        {
            var result = renderer.RenderWithLayout(template, null,
                options: FilledTemplateOptionsFor(protocol, fonts));
            Require(result.PageCount >= 2, $"Version 2 填充模板至少两张标签: {protocol}");
            foreach (var outputPage in result.Pages)
            {
                Require(outputPage.Elements.Any(element => element.Type == 7 &&
                    element.Width == 8 && element.Height == 3),
                    $"Version 2 填充模板布局轨迹: {protocol}");
                Require(outputPage.ByteLength > 0, $"Version 2 填充模板输出非空: {protocol}");
                switch (protocol)
                {
                    case PrintProtocol.Cpcl:
                        RequireAscii(outputPage.Data, "L 1 2 8 2 3\r\n", "Version 2 CPCL 填充模板字节");
                        break;
                    case PrintProtocol.Tspl:
                        RequireAscii(outputPage.Data, "BAR 1,2,8,3\r\n", "Version 2 TSPL 填充模板字节");
                        break;
                    case PrintProtocol.Zpl:
                        RequireAscii(outputPage.Data, "^FO1,2^GB8,3,3,B,0^FS\r\n", "Version 2 ZPL 填充模板字节");
                        break;
                    case PrintProtocol.EscPos:
                        RequireSequence(outputPage.Data, new byte[] { 0x1D, 0x76, 0x30 },
                            "Version 2 ESC/POS Raster 填充模板字节");
                        break;
                }
            }
        }
    }

    private static IPrintProtocolProvider CreateFilledProvider(PrintProtocol protocol)
    {
        switch (protocol)
        {
            case PrintProtocol.Cpcl:
                return PrintProtocolProviderFactory.Create(PrintProtocol.Cpcl, Encoding.ASCII);
            case PrintProtocol.EscPos:
                return PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
                    EscPosLayoutMode.Raster);
            case PrintProtocol.Tspl:
                return PrintProtocolProviderFactory.Create(PrintProtocol.Tspl, Encoding.ASCII,
                    tsplOptions: new TsplMediaOptions
                    {
                        Profile = new TsplPrinterProfile
                        {
                            SupportsImages = false,
                            SupportsGraphics = true
                        }
                    });
            case PrintProtocol.Zpl:
                return PrintProtocolProviderFactory.Create(PrintProtocol.Zpl, Encoding.ASCII,
                    zplOptions: new ZplMediaOptions
                    {
                        Profile = new ZplPrinterProfile
                        {
                            SupportsImages = false,
                            SupportsGraphics = true
                        }
                    });
            default:
                throw new ArgumentOutOfRangeException(nameof(protocol), protocol, "未知协议。");
        }
    }

    private static BpTemplate CreateFilledV2Template()
    {
        return new BpTemplate
        {
            Version = 2,
            Width = 10,
            Height = 7,
            Rows = new System.Collections.Generic.List<BpRow>
            {
                FilledLabelRow(),
                FilledLabelRow()
            }
        };
    }

    private static BpRow FilledLabelRow()
    {
        return new BpRow
        {
            Height = 7,
            Cells = new System.Collections.Generic.List<BpCell>
            {
                new BpCell
                {
                    Elements = new System.Collections.Generic.List<BpElement>
                    {
                        new BpRectangleElement
                        {
                            Width = 8,
                            Height = 3,
                            Filled = true,
                            StartCoordinate = new BpCoordinate { X = 1, Y = 2 }
                        }
                    }
                }
            }
        };
    }

    private static TemplateRenderOptions FilledTemplateOptionsFor(PrintProtocol protocol,
        RasterFontCatalog fonts)
    {
        var options = OptionsFor(protocol, fonts);
        if (protocol == PrintProtocol.Tspl)
        {
            options.TsplMedia = new TsplMediaOptions
            {
                Profile = new TsplPrinterProfile
                {
                    SupportsImages = false,
                    SupportsGraphics = true
                }
            };
        }
        else if (protocol == PrintProtocol.Zpl)
        {
            options.ZplMedia = new ZplMediaOptions
            {
                Profile = new ZplPrinterProfile
                {
                    SupportsImages = false,
                    SupportsGraphics = true
                }
            };
        }
        return options;
    }

    private static void VerifyZplDelayedOutput()
    {
        var media = new ZplMediaOptions
        {
            Profile = new ZplPrinterProfile
            {
                Cutter = new ZplCutterProfile
                {
                    Strategy = ZplCutStrategy.Delayed,
                    SupportedCutModes = new[] { PrintCutMode.Full },
                    SupportsTearOff = true
                }
            }
        };
        var command = (ZplPrintCommand)ProtocolCommandFactory.Create(
            PrintProtocol.Zpl, Encoding.ASCII, zplOptions: media);
        command.Start()
            .Label(60, 40)
            .Text(1, 2, "DELAYED")
            .PrintWithCut(new PrintCutOptions { Mode = PrintCutMode.Full })
            .End();

        Require(command.Capabilities.SupportsIndependentOutputSegments,
            "ZPL Delayed 独立输出能力");
        RequireThrows<NotSupportedException>(() => command.Build(),
            "ZPL Delayed 旧 Build 应拒绝合并输出");

        var first = command.BuildOutput();
        var second = command.BuildOutput();
        Require(first.Segments.Count == 2, "ZPL Delayed 输出段数");
        Require(first.Segments[0].Kind == PrintOutputSegmentKind.Commands &&
                first.Segments[1].Kind == PrintOutputSegmentKind.Control,
            "ZPL Delayed 输出段顺序");
        RequireAsciiEqual("^XA\r\n^PW60\r\n^LL40\r\n" +
            "^FO1,2^A0N,30,30^FDDELAYED^FS\r\n" +
            "^MMD\r\n^PQ1\r\n^XZ\r\n", first.Segments[0].Data,
            "ZPL Delayed 命令段");
        RequireAsciiEqual("~JK\r\n", first.Segments[1].Data, "ZPL Delayed 控制段");
        RequireBytesEqual(first.Segments[0].Data, second.Segments[0].Data,
            "ZPL Delayed 命令段幂等性");
        RequireBytesEqual(first.Segments[1].Data, second.Segments[1].Data,
            "ZPL Delayed 控制段幂等性");
        RequireThrows<NotSupportedException>(() => { var ignored = first.Data; },
            "ZPL Delayed 旧 Data 属性应拒绝合并输出");
    }

    private static RasterFontCatalog CreateFixedFonts(string fontPath)
    {
        var resource = new RasterFontResource("DejaVu", "dejavu-2.37", File.ReadAllBytes(fontPath));
        return new RasterFontCatalog(new[] { resource }, "DejaVu");
    }

    private static TemplateRenderOptions OptionsFor(PrintProtocol protocol, RasterFontCatalog fonts)
    {
        return new TemplateRenderOptions
        {
            Protocol = protocol,
            Encoding = Encoding.ASCII,
            EscPosLayout = protocol == PrintProtocol.EscPos ? EscPosLayoutMode.Raster : EscPosLayoutMode.Native,
            EscPosDevice = protocol == PrintProtocol.EscPos
                ? new EscPosDeviceOptions { RasterFonts = fonts }
                : null,
            FeedLines = 0,
            CutAfterPrint = false
        };
    }

    private static string SimpleTemplateJson()
    {
        return "{\"Version\":1,\"Width\":240,\"Height\":64,\"Rows\":[" +
            "{\"Height\":32,\"Cells\":[{\"Elements\":[" +
            "{\"Type\":1,\"FontSize\":24,\"Field\":{\"Value\":\"ASCII-PACK\"}}" +
            "]}]}]}";
    }

    private static T Single<T>(IReadOnlyList<T> values, string description)
    {
        if (values == null || values.Count != 1)
            throw new InvalidOperationException($"验证失败: {description} 应只有一项");
        return values[0];
    }

    private static string? ReadOption(string[] args, string name)
    {
        for (var index = 0; index < args.Length; index++)
        {
            if (!string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase)) continue;
            if (index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]))
                throw new ArgumentException($"缺少参数 {name} 的值。");
            return Path.GetFullPath(args[index + 1]);
        }
        return null;
    }

    private static void Require(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException("验证失败: " + description);
    }

    private static void RequireAscii(byte[] actual, string expectedFragment, string description)
    {
        RequireSequence(actual, Encoding.ASCII.GetBytes(expectedFragment), description);
    }

    private static void RequireAsciiEqual(string expected, byte[] actual, string description)
    {
        RequireBytesEqual(Encoding.ASCII.GetBytes(expected), actual, description);
    }

    private static void RequireBytesEqual(byte[] expected, byte[] actual, string description)
    {
        Require(expected != null && actual != null && expected.SequenceEqual(actual),
            description + $" (expected {FormatBytes(expected)}, actual {FormatBytes(actual)})");
    }

    private static void RequireSequence(byte[] actual, byte[] expected, string description)
    {
        Require(actual != null && expected != null && ContainsSequence(actual, expected),
            description + $" (missing {FormatBytes(expected)})");
    }

    private static bool ContainsSequence(byte[] actual, byte[] expected)
    {
        if (expected.Length == 0) return true;
        for (var start = 0; start <= actual.Length - expected.Length; start++)
        {
            var match = true;
            for (var index = 0; index < expected.Length; index++)
            {
                if (actual[start + index] == expected[index]) continue;
                match = false;
                break;
            }
            if (match) return true;
        }
        return false;
    }

    private static void RequireThrows<TException>(Action action, string description)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }
        catch (Exception error)
        {
            throw new InvalidOperationException($"{description}，但抛出 {error.GetType().Name}。", error);
        }

        throw new InvalidOperationException(description + "，但没有抛出预期异常。");
    }

    private static string FormatBytes(byte[]? bytes)
    {
        if (bytes == null) return "<null>";
        return BitConverter.ToString(bytes);
    }
}
