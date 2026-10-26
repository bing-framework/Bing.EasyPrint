using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Templates;
using Xunit;

namespace Bing.EasyPrint.Tests
{
    public sealed class FieldFormatTests
    {
        static FieldFormatTests() => System.Text.Encoding.RegisterProvider(
            System.Text.CodePagesEncodingProvider.Instance);

        [Fact]
        public void Format_order_is_convert_format_pad_prefix_suffix()
        {
            var template = Template("Amount", new BgFieldFormat
            {
                Kind = BgFieldFormatKind.Number,
                Pattern = "0.00",
                PadLength = 8,
                PadCharacter = "0",
                Prefix = "[",
                Suffix = "]"
            });
            var result = Render(template, new { Amount = "-12.5" });
            Assert.Equal("[-0012.50]", Text(result));
        }

        [Fact]
        public void Null_text_only_replaces_actual_null_and_then_applies_pad_and_affixes()
        {
            var template = Template("Amount", new BgFieldFormat
            {
                Kind = BgFieldFormatKind.Text,
                NullText = "N/A",
                PadLength = 4,
                PadCharacter = " ",
                Prefix = "<",
                Suffix = ">"
            });
            var result = Render(template, new { Amount = (object)null });
            Assert.Equal("< N/A>", Text(result));
            Assert.Equal("<    >", Text(Render(template, new { Amount = string.Empty })));
        }

        [Fact]
        public void Date_format_preserves_offset_and_is_independent_of_current_culture()
        {
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
                var template = Template("When", new BgFieldFormat
                {
                    Kind = BgFieldFormatKind.Date,
                    Pattern = "yyyy-MM-dd HH:mm zzz",
                    Culture = "en-US"
                });
                var result = Render(template, new { When = "2024-01-02T03:04:05+08:00" });
                Assert.Equal("2024-01-02 03:04 +08:00", Text(result));
            }
            finally { CultureInfo.CurrentCulture = previous; }
        }

        [Fact]
        public void Enum_mapping_and_invalid_values_report_field_path()
        {
            var template = Template("Status", new BgFieldFormat
            {
                Kind = BgFieldFormatKind.Enum,
                Map = new Dictionary<string, string> { ["A"] = "可用" }
            });
            Assert.Equal("可用", Text(Render(template, new { Status = "A" })));

            var error = Assert.Throws<TemplateRenderException>(() => Render(template, new { Status = "B" }));
            Assert.Equal("Rows[0].Cells[0].Elements[0]", error.SourcePath);
            Assert.Equal("Status", error.FieldPath);

            var count = Template("Count", new BgFieldFormat { Kind = BgFieldFormatKind.Number, Pattern = "0" });
            var missing = Assert.Throws<TemplateRenderException>(() => Render(count, new { Other = 1 }));
            Assert.Equal("Count", missing.FieldPath);
        }

        [Fact]
        public void Decimal_precision_is_not_routed_through_double()
        {
            var template = Template("Amount", new BgFieldFormat
            {
                Kind = BgFieldFormatKind.Number,
                Pattern = "0.00",
                Culture = "en-US"
            });
            var result = Render(template, new { Amount = "12345678901234567890.126" });
            Assert.Equal("12345678901234567890.13", Text(result));
        }

        [Fact]
        public void Invalid_number_date_format_and_oversized_padding_fail_before_output()
        {
            var overflow = Template("Value", new BgFieldFormat { Kind = BgFieldFormatKind.Number, Pattern = "0" });
            Assert.Throws<TemplateRenderException>(() => Render(overflow, new { Value = "999999999999999999999999999999999999" }));
            var invalidDate = Template("Value", new BgFieldFormat
            {
                Kind = BgFieldFormatKind.Date,
                Pattern = "yyyy-MM-dd"
            });
            Assert.Throws<TemplateRenderException>(() => Render(invalidDate, new { Value = "2024-13-40" }));
            var hugePad = Template("Value", new BgFieldFormat
            {
                Kind = BgFieldFormatKind.Text,
                PadLength = 32 * 1024 * 1024
            });
            Assert.Throws<TemplateRenderException>(() => Render(hugePad, new { Value = "x" }));
        }

        [Fact]
        public void Invalid_pattern_and_incomplete_date_are_rejected_before_encoding()
        {
            var badNumber = Template("Value", new BgFieldFormat { Kind = BgFieldFormatKind.Number, Pattern = "Q2" });
            var numberError = Assert.Throws<TemplateRenderException>(() => Render(badNumber, new { Value = "12" }));
            Assert.Equal("Value", numberError.FieldPath);
            var badPrecision = Template("Value", new BgFieldFormat { Kind = BgFieldFormatKind.Number, Pattern = "F999999999" });
            Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().Render(badPrecision,
                new { Value = 12m }, options: new TemplateRenderOptions { MaxOutputBytes = 512 }));
            var badDate = Template("Value", new BgFieldFormat
            {
                Kind = BgFieldFormatKind.Date,
                InputFormat = "MM-dd",
                Pattern = "yyyy-MM-dd"
            });
            var dateError = Assert.Throws<TemplateRenderException>(() => Render(badDate, new { Value = "01-02" }));
            Assert.Equal("Value", dateError.FieldPath);
            var missingDay = Template("Value", new BgFieldFormat
            {
                Kind = BgFieldFormatKind.Date,
                InputFormat = "yyyy-MM",
                Pattern = "yyyy-MM-dd"
            });
            Assert.Throws<TemplateRenderException>(() => Render(missingDay, new { Value = "2024-01" }));
        }

        [Fact]
        public void Quoted_date_tokens_and_missing_offset_are_rejected()
        {
            var quotedYear = Template("Value", new BgFieldFormat
            {
                Kind = BgFieldFormatKind.Date,
                InputFormat = "'y'MM-dd",
                Pattern = "yyyy-MM-dd"
            });
            Assert.Throws<TemplateRenderException>(() => Render(quotedYear, new { Value = "y01-02" }));
            var optionalOffset = Template("Value", new BgFieldFormat
            {
                Kind = BgFieldFormatKind.Date,
                InputFormat = "yyyy-MM-dd'T'HH:mm:ssK",
                Pattern = "yyyy-MM-dd HH:mm zzz"
            });
            Assert.Throws<TemplateRenderException>(() => Render(optionalOffset, new { Value = "2024-01-02T03:04:05" }));
            Assert.Equal("2024-01-02 03:04 +08:00",
                Text(Render(optionalOffset, new { Value = "2024-01-02T03:04:05+08:00" })));
        }

        [Fact]
        public void Short_formatted_result_fits_a_small_text_budget()
        {
            var number = Template("Value", new BgFieldFormat { Kind = BgFieldFormatKind.Number, Pattern = "0.00" });
            Assert.Equal("1.23", Text(new TemplateRenderer().RenderWithLayout(number, new { Value = 1.23m },
                options: new TemplateRenderOptions { MaxOutputBytes = 128 })));
            var date = Template("Value", new BgFieldFormat
            {
                Kind = BgFieldFormatKind.Date,
                Pattern = "yyyy-MM-dd"
            });
            Assert.Equal("2024-01-02", Text(new TemplateRenderer().RenderWithLayout(date,
                new { Value = "2024-01-02" }, options: new TemplateRenderOptions { MaxOutputBytes = 128 })));
            var longNumber = Template("Value", new BgFieldFormat { Kind = BgFieldFormatKind.Number, Pattern = "F100" });
            Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().RenderWithLayout(longNumber,
                new { Value = 1m }, options: new TemplateRenderOptions { MaxOutputBytes = 128 }));
        }

        [Fact]
        public void Low_text_budget_still_allows_a_short_formatted_result()
        {
            var template = Template("Value", new BgFieldFormat { Kind = BgFieldFormatKind.Number, Pattern = "0.00" });
            var result = new TemplateRenderer().RenderWithLayout(template, new { Value = 1.23m },
                options: new TemplateRenderOptions { MaxOutputBytes = 1024 });
            Assert.Equal("1.23", Text(result));
        }
        [Fact]
        public void Lowercase_standard_number_and_signed_text_min_values_fit_estimates()
        {
            var lower = Template("Value", new BgFieldFormat { Kind = BgFieldFormatKind.Number, Pattern = "f90" });
            var lowerText = string.Concat(Render(lower, new { Value = 1.2m }).Pages[0].Elements
                .Where(item => item.Kind == "element" && item.Type == 1).Select(item => item.Text));
            Assert.Equal(1.2m.ToString("f90", CultureInfo.InvariantCulture), lowerText);
            var text = Template("Value", new BgFieldFormat { Kind = BgFieldFormatKind.Text });
            Assert.Equal("-128", Text(Render(text, new { Value = (sbyte)-128 })));
            Assert.Equal("-32768", Text(Render(text, new { Value = (short)-32768 })));
            Assert.Equal("-2147483648", Text(Render(text, new { Value = int.MinValue })));
        }

        [Fact]
        public void Format_applies_to_shared_qr_field_path()
        {
            var template = new BpTemplate
            {
                Version = 2,
                Width = 200,
                Height = 200,
                Rows = new List<BpRow>
                {
                    new BpRow
                    {
                        Height = 200,
                        Cells = new List<BpCell>
                        {
                            new BpCell
                            {
                                Width = 200,
                                Elements = new List<BpElement>
                                {
                                    new BpQrCodeElement
                                    {
                                        UnitWidth = 4,
                                        Field = new BgField
                                        {
                                            Name = "Code",
                                            Format = new BgFieldFormat
                                            {
                                                Kind = BgFieldFormatKind.Text,
                                                Prefix = "QR-",
                                                Suffix = "-END"
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            };
            var result = Render(template, new { Code = "001" });
            var layout = result.Pages[0].Elements.Single(item => item.Kind == "element" && item.Type == 3);
            Assert.Equal("QR-001-END", layout.Text);
        }

        [Fact]
        public void Json_roundtrip_preserves_format_and_renders_the_same_text()
        {
            var template = Template("Amount", new BgFieldFormat
            {
                Kind = BgFieldFormatKind.Currency,
                Pattern = "C2",
                Culture = "en-US",
                Prefix = "USD ",
                Map = new Dictionary<string, string> { ["unused"] = "x" }
            });
            var loader = new TemplateLoader();
            var json = loader.SaveJson(template);
            var loaded = loader.LoadJson(json);
            var format = loaded.Rows[0].Cells[0].Elements[0].Field.Format;
            Assert.Equal(BgFieldFormatKind.Currency, format.Kind);
            Assert.Equal("C2", format.Pattern);
            Assert.Equal("en-US", format.Culture);
            Assert.Equal("USD $12.50", Text(Render(loaded, new { Amount = 12.5m })));
        }

        private static BpTemplate Template(string path, BgFieldFormat format)
            => new BpTemplate
            {
                Version = 2,
                Width = 400,
                Height = 80,
                Rows = new List<BpRow>
                {
                    new BpRow
                    {
                        Height = 80,
                        Cells = new List<BpCell>
                        {
                            new BpCell
                            {
                                Width = 400,
                                Elements = new List<BpElement>
                                {
                                    new BpFontElement
                                    {
                                        FontSize = 24,
                                        Field = new BgField { Name = path, Format = format }
                                    }
                                }
                            }
                        }
                    }
                }
            };

        private static string Text(TemplateRenderResult result)
            => Assert.Single(result.Pages[0].Elements.Where(item => item.Kind == "element" && item.Type == 1)).Text;

        private static TemplateRenderResult Render(BpTemplate template, object data)
            => new TemplateRenderer().RenderWithLayout(template, data);
    }
}
