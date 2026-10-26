using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Templates;
using Xunit;

namespace Bing.EasyPrint.Tests
{
    public sealed class TemplateCustomProtocolTests
    {
        [Fact]
        public void Custom_character_width_and_line_height_drive_wrapping_and_layout()
        {
            var template = Template("abcdefghijklmnopqrst");
            var narrow = Render(template, new CustomAdapter(new CustomConfig { CharacterWidth = 20, LineHeight = 24 }));
            var wide = Render(template, new CustomAdapter(new CustomConfig { CharacterWidth = 5, LineHeight = 12 }));

            var narrowMatches = narrow.Pages[0].Elements.Where(item =>
                item.SourcePath == "Rows[0].Cells[0].Elements[0]").ToArray();
            var wideMatches = wide.Pages[0].Elements.Where(item =>
                item.SourcePath == "Rows[0].Cells[0].Elements[0]").ToArray();
            Assert.True(narrowMatches.Length > 0);
            Assert.True(wideMatches.Length > 0);
            Assert.Equal(24, narrowMatches[0].Height);
            Assert.Equal(12, wideMatches[0].Height);
            Assert.NotEqual(narrowMatches.Length, wideMatches.Length);
            Assert.All(new[] { narrow, wide }, result => Assert.Equal("demo.test", result.ProtocolId));
        }

        [Fact]
        public void Custom_layout_reuses_repeat_binding_and_page_ownership()
        {
            var template = new BpTemplate
            {
                Version = 2,
                Width = 100,
                Height = 50,
                Rows = new List<BpRow>
                {
                    new BpRow
                    {
                        Height = 20,
                        RepeatSource = "Items",
                        Cells = new List<BpCell>
                        {
                            new BpCell
                            {
                                Width = 100,
                                Elements = new List<BpElement>
                                {
                                    new BpFontElement { FontSize = 12, Field = new BgField { Name = "Name" } },
                                },
                            },
                        },
                    },
                },
            };
            var data = new
            {
                Items = new[] { new { Name = "one" }, new { Name = "two" }, new { Name = "three" } },
            };
            var adapter = new CustomAdapter(new CustomConfig { CharacterWidth = 5, LineHeight = 10 });
            var result = new TemplateRenderer().RenderCustomProtocolWithLayout(template, data,
                options: adapter.Options());

            Assert.Equal(2, result.PageCount);
            var items = result.Pages.SelectMany(page => page.Elements)
                .Where(item => item.Type == 1)
                .Select(item => item.Text)
                .ToArray();
            Assert.Equal(new[] { "one", "two", "three" }, items);
            Assert.Equal(new[] { 0, 1, 2 }, result.Pages.SelectMany(page => page.Elements)
                .Where(item => item.Type == 1).Select(item => item.InstanceIndices.Single()));
            Assert.Equal(new[] { "one", "two", "three" }, adapter.LastBackend.Texts);
        }

        [Fact]
        public void Custom_protocol_rejects_unsupported_element_and_rotation_before_backend_output()
        {
            var barcode = new BpTemplate
            {
                Version = 2,
                Width = 100,
                Height = 100,
                Rows = new List<BpRow>
                {
                    new BpRow
                    {
                        Height = 50,
                        Cells = new List<BpCell>
                        {
                            new BpCell
                            {
                                Width = 100,
                                Elements = new List<BpElement>
                                {
                                    new BpBarcodeElement { BarcodeType = BarcodeType.Code128, Field = new BgField { Value = "A" } },
                                },
                            },
                        },
                    },
                },
            };
            var adapter = new CustomAdapter(new CustomConfig { CharacterWidth = 5, LineHeight = 10 });

            Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().RenderCustomProtocol(barcode, null,
                options: adapter.Options()));
            Assert.Null(adapter.LastBackend);
        }

        [Fact]
        public void Custom_capability_switches_reject_coordinates_and_bold_before_backend_output()
        {
            var template = new BpTemplate
            {
                Version = 2,
                Width = 100,
                Height = 100,
                Rows = new List<BpRow>
                {
                    new BpRow
                    {
                        Height = 50,
                        Cells = new List<BpCell>
                        {
                            new BpCell
                            {
                                Width = 100,
                                Elements = new List<BpElement>
                                {
                                    new BpFontElement
                                    {
                                        FontSize = 12,
                                        Bold = true,
                                        Field = new BgField { Value = "A" },
                                        StartCoordinate = new BpCoordinate { X = 1, Y = 2 },
                                    },
                                },
                            },
                        },
                    },
                },
            };
            var adapter = new CustomAdapter(new CustomConfig
            {
                CharacterWidth = 5,
                LineHeight = 10,
                SupportsCoordinates = false,
            });
            Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().RenderCustomProtocol(
                template, null, options: adapter.Options()));
            Assert.Null(adapter.LastBackend);
        }
        [Fact]
        public void Custom_protocol_validates_inline_group_children_before_backend_output()
        {
            var template = new BpTemplate
            {
                Version = 2,
                Width = 100,
                Height = 100,
                Rows = new List<BpRow>
                {
                    new BpRow
                    {
                        Height = 50,
                        Cells = new List<BpCell>
                        {
                            new BpCell
                            {
                                Width = 100,
                                Elements = new List<BpElement>
                                {
                                    new BpGroupElement
                                    {
                                        Elements = new List<BpElement>
                                        {
                                            new BpBarcodeElement { BarcodeType = BarcodeType.Code128, Field = new BgField { Value = "A" } },
                                        },
                                    },
                                },
                            },
                        },
                    },
                },
            };
            var adapter = new CustomAdapter(new CustomConfig { CharacterWidth = 5, LineHeight = 10 });

            Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().RenderCustomProtocol(template, null,
                options: adapter.Options()));
            Assert.Null(adapter.LastBackend);
        }

        [Fact]
        public void Custom_backend_is_created_per_render_and_disposed()
        {
            var adapter = new CustomAdapter(new CustomConfig { CharacterWidth = 5, LineHeight = 10 });
            var renderer = new TemplateRenderer();
            var template = Template("A");

            renderer.RenderCustomProtocol(template, null, options: adapter.Options());
            var first = adapter.LastBackend;
            renderer.RenderCustomProtocol(template, null, options: adapter.Options());
            var second = adapter.LastBackend;

            Assert.NotSame(first, second);
            Assert.True(first.Disposed);
            Assert.True(second.Disposed);
        }

        [Fact]
        public void Custom_physical_copy_expansion_respects_max_pages_before_backend_creation()
        {
            var adapter = new CustomAdapter(new CustomConfig { Copies = 100, MaxPages = 100 });
            var result = Render(Template("A"), adapter);

            Assert.Equal(100, result.PageCount);
            Assert.Equal(0, result.Pages[0].SourcePageIndex);
            Assert.Equal(99, result.Pages[99].CopyIndex);
            Assert.Equal(100, adapter.LastBackend.LastDocumentPages);
            Assert.Equal(1, adapter.LastBackend.RenderCalls);

            var rejected = new CustomAdapter(new CustomConfig { Copies = 101, MaxPages = 100 });
            Assert.Throws<TemplateRenderException>(() => Render(Template("A"), rejected));
            Assert.Null(rejected.LastBackend);

            var customLimit = new CustomAdapter(new CustomConfig { Copies = 3, MaxPages = 2 });
            Assert.Throws<TemplateRenderException>(() => Render(Template("A"), customLimit));
            Assert.Null(customLimit.LastBackend);
        }

        [Fact]
        public void Custom_declared_device_font_and_max_text_size_flow_through_registry_layout_and_backend()
        {
            var config = new CustomConfig
            {
                MaxTextSize = 64,
                FontKinds = new[] { 1 },
                SupportsLegacyDeviceFont = true,
            };
            var adapter = new CustomAdapter(config);
            var result = Render(TemplateWithFont("DEV", 64, new PrintFont(PrintFontKind.Device, "FONT1")), adapter);

            Assert.Single(result.Pages);
            Assert.Equal(new[] { "DEV" }, adapter.LastBackend.Texts);
            Assert.Equal(new[] { "FONT1" }, adapter.LastBackend.DeviceFonts);
            Assert.Equal(new[] { 64 }, adapter.LastBackend.FontSizes);
            Assert.Equal(64, adapter.LastMeasurementRequest.FontSize);
            Assert.Equal(PrintFontKind.Device, adapter.LastMeasurementRequest.Font.Kind);

            var tooLarge = new CustomAdapter(config);
            Assert.Throws<TemplateRenderException>(() => Render(
                TemplateWithFont("DEV", 65, new PrintFont(PrintFontKind.Device, "FONT1")), tooLarge));
            Assert.Null(tooLarge.LastBackend);

            var undeclared = new CustomAdapter(new CustomConfig { MaxTextSize = 64, FontKinds = new[] { 0 } });
            Assert.Throws<TemplateRenderException>(() => Render(
                TemplateWithFont("DEV", 24, new PrintFont(PrintFontKind.Device, "FONT1")), undeclared));
            Assert.Null(undeclared.LastBackend);
        }

        private static BpTemplate TemplateWithFont(string value, int fontSize, PrintFont font)
            => new BpTemplate
            {
                Version = 2,
                Width = 100,
                Height = 100,
                Rows = new List<BpRow>
                {
                    new BpRow
                    {
                        Height = 100,
                        Cells = new List<BpCell>
                        {
                            new BpCell
                            {
                                Width = 100,
                                Elements = new List<BpElement>
                                {
                                    new BpFontElement { FontSize = fontSize, Font = font, Field = new BgField { Value = value } },
                                },
                            },
                        },
                    },
                },
            };

        private static BpTemplate Template(string value)
            => new BpTemplate
            {
                Version = 2,
                Width = 100,
                Height = 100,
                Rows = new List<BpRow>
                {
                    new BpRow
                    {
                        Height = 100,
                        Cells = new List<BpCell>
                        {
                            new BpCell
                            {
                                Width = 100,
                                Elements = new List<BpElement>
                                {
                                    new BpFontElement { FontSize = 12, Field = new BgField { Value = value } },
                                },
                            },
                        },
                    },
                },
            };

        private static TemplateCustomRenderResult Render(BpTemplate template, CustomAdapter adapter)
            => new TemplateRenderer().RenderCustomProtocolWithLayout(template, null, options: adapter.Options());

        private sealed class CustomConfig
        {
            public int CharacterWidth { get; set; } = 5;
            public int LineHeight { get; set; } = 10;
            public bool SupportsCoordinates { get; set; } = true;
            public bool SupportsBoldText { get; set; }
            public int MaxTextSize { get; set; } = 512;
            public int[] FontKinds { get; set; } = { 0 };
            public bool SupportsLegacyDeviceFont { get; set; }
            public bool SupportsHostFontFamily { get; set; }
            public int Copies { get; set; } = 1;
            public int MaxPages { get; set; } = 100;
        }

        private sealed class CustomAdapter : ITemplateProtocolAdapter
        {
            private readonly CustomConfig _config;
            private readonly PrintProtocolCapabilities _coreCapabilities;

            public CustomAdapter(CustomConfig config)
            {
                _config = config;
                _coreCapabilities = new PrintProtocolCapabilities(203, new[] { 1, 7 },
                    textSizes: Array.Empty<int>(), fontKinds: config.FontKinds,
                    textRotations: new[] { RotationAngle.None }, maxPageHeightDots: 4096,
                    supportsSoftwareBitmap: false, softwareBitmapReason: "测试协议不提供软件位图。",
                    supportsCoordinates: config.SupportsCoordinates, supportsBoldText: config.SupportsBoldText,
                    supportsHostFontFamily: config.SupportsHostFontFamily,
                    supportsLegacyDeviceFont: config.SupportsLegacyDeviceFont)
                    .WithMaxTextSize(config.MaxTextSize);
                Capabilities = TemplateCustomProtocolCapabilities.FromCore(_coreCapabilities);
            }

            public string ProtocolId => "demo.test";
            public TemplateCustomProtocolCapabilities Capabilities { get; }
            public FakeBackend LastBackend { get; private set; }
            public TemplateTextMeasurementRequest LastMeasurementRequest { get; private set; }

            public ITemplateTextMetrics CreateTextMetrics(TemplateTextMeasurementRequest request)
            {
                if (request.Rotation != RotationAngle.None)
                    throw new NotSupportedException("只支持横排文字。");
                LastMeasurementRequest = request;
                return new FakeMetrics(_config, request.Text);
            }

            public TemplateBarcodeMeasurement MeasureBarcode(TemplateBarcodeMeasurementRequest request)
                => throw new NotSupportedException("未声明一维码。");
            public PrintElement ConvertBarcode(TemplateBarcodeConversionRequest request)
                => throw new NotSupportedException("未声明一维码。");
            public TemplateQrCodeMeasurement MeasureQrCode(TemplateQrCodeMeasurementRequest request)
                => throw new NotSupportedException("未声明二维码。");
            public PrintElement ConvertQrCode(TemplateQrCodeConversionRequest request)
                => throw new NotSupportedException("未声明二维码。");
            public TemplateRenderOptions Options()
            {
                var registry = new PrintProtocolRegistry();
                registry.Register(new PrintProtocolRegistration(ProtocolId, "Test",
                    _ => LastBackend = new FakeBackend(ProtocolId), capabilities: _coreCapabilities));
                registry.Freeze();
                return new TemplateRenderOptions
                {
                    ProtocolId = ProtocolId,
                    CustomProtocol = this,
                    CustomProtocolConfigurationJson = "{}",
                    ProtocolRegistry = registry,
                    Encoding = Encoding.UTF8,
                    Copies = _config.Copies,
                    MaxPages = _config.MaxPages,
                };
            }
        }

        private sealed class FakeMetrics : ITemplateTextMetrics
        {
            private readonly int _characterWidth;

            public FakeMetrics(CustomConfig config, string text)
            {
                _characterWidth = config.CharacterWidth;
                LineHeight = config.LineHeight;
            }

            public int LineHeight { get; }
            public string FontFamily => null;
            public bool HasDeviceTextElementCalibrations => false;
            public bool NeedsFullMeasure => false;
            public int Advance(string textElement) => (textElement ?? string.Empty).Length * _characterWidth;
            public long Measure(string text) => (long)(text ?? string.Empty).Length * _characterWidth;
            public TemplateTextBounds MeasureBounds(string text)
                => new TemplateTextBounds(0, 0, Advance(text), LineHeight);
        }

        public sealed class FakeBackend : IPrintProtocolBackend
        {
            public FakeBackend(string protocolId) => ProtocolId = protocolId;

            public string ProtocolId { get; }
            public bool Disposed { get; private set; }
            public int ValidateCalls { get; private set; }
            public int RenderCalls { get; private set; }
            public int LastDocumentPages { get; private set; }
            public List<string> Texts { get; } = new List<string>();
            public List<string> DeviceFonts { get; } = new List<string>();
            public List<int> FontSizes { get; } = new List<int>();

            public void Validate(PrintDocument document, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ValidateCalls++;
                LastDocumentPages = document.Pages.Count;
            }

            public IReadOnlyList<PrintPageOutput> Render(PrintDocument document,
                CancellationToken cancellationToken)
            {
                RenderCalls++;
                LastDocumentPages = document.Pages.Count;
                Texts.Clear();
                DeviceFonts.Clear();
                FontSizes.Clear();
                foreach (var page in document.Pages)
                foreach (var element in page.Elements)
                    if (element is PrintTextElement text)
                    {
                        Texts.Add(text.Text);
                        DeviceFonts.Add(text.DeviceFontName);
                        FontSizes.Add(text.FontSize);
                    }
                var result = new List<PrintPageOutput>();
                foreach (var page in document.Pages)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    result.Add(new PrintPageOutput(new[]
                    {
                        new PrintOutputSegment(Encoding.UTF8.GetBytes("page")),
                    }));
                }
                return result;
            }

            public void Dispose() => Disposed = true;
        }
    }
}
