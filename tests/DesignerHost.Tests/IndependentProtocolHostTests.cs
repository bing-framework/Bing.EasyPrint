using System.Text;
using System.Text.Json;
using Bing.EasyPrint;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Templates;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[CollectionDefinition("HostProtocolState", DisableParallelization = true)]
public sealed class HostProtocolStateCollection { }

[Collection("HostProtocolState")]
public sealed class IndependentProtocolHostTests
{
    private const string RequestPrefix = "{\"template\":{\"Version\":2,\"Width\":100,\"Height\":100,\"Rows\":[{\"Height\":100,\"Cells\":[{\"Width\":100,\"Elements\":[{\"Type\":1,";
    private const string RequestSuffix = "\"Font\":{\"Kind\":1,\"Name\":\"FONT1\"},\"Field\":{\"Value\":\"DEV\"}}]}]}]},\"data\":{},\"protocolId\":\"independent.test\",\"protocolConfig\":{\"maxTextSize\":64}}";

    [Fact]
    public async Task Independent_protocol_negotiates_and_renders_declared_device_font_with_configured_limit()
    {
        var state = new IndependentState();
        var config = IndependentConfig.Parse("{\"maxTextSize\":64}");
        var capabilities = IndependentProtocolAdapter.CreateCoreCapabilities(config);
        var registration = new PrintProtocolRegistration("independent.test", "Independent Test",
            _ => new IndependentBackend(state), "{}", "{}", null, capabilities: capabilities,
            configurationValidator: json => _ = IndependentConfig.Parse(json));
        var registry = DesignerProtocolRegistry.CreateForTests(registration,
            json => new IndependentProtocolAdapter(IndependentConfig.Parse(json), state));
        var previous = DesignerRequestProcessor.ProtocolRegistry;
        try
        {
            DesignerRequestProcessor.ProtocolRegistry = registry;
            var context = CreateContext(RequestPrefix + "\"FontSize\":64," + RequestSuffix);
            await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.Preview);

            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
            Assert.Equal(1, state.RenderCalls);
            Assert.Equal("FONT1", state.DeviceFonts.Single());
            Assert.Equal(64, state.FontSizes.Single());
            Assert.Equal(1, state.ValidateCalls);
            var capabilitiesContext = CreateContext(
                "{\"protocolId\":\"independent.test\",\"protocolConfig\":{\"maxTextSize\":64}}");
            await DesignerRequestProcessor.CustomCapabilities(new TemplateRenderOptions
            {
                ProtocolId = "independent.test",
                CustomProtocol = new IndependentProtocolAdapter(config, state)
            }, CancellationToken.None).ExecuteAsync(capabilitiesContext);
            using var capabilitiesJson = ReadJson(capabilitiesContext);
            Assert.Equal(64, capabilitiesJson.RootElement.GetProperty("textSize").GetProperty("maximum").GetInt32());
            Assert.True(capabilitiesJson.RootElement.GetProperty("supportsLegacyDeviceFont").GetBoolean());

            var tooLarge = CreateContext(RequestPrefix + "\"FontSize\":65," + RequestSuffix);
            await DesignerRequestExecution.ExecuteAsync(tooLarge, DesignerRequestProcessor.Preview);
            Assert.Equal(StatusCodes.Status400BadRequest, tooLarge.Response.StatusCode);
            Assert.Equal(1, state.RenderCalls);
        }
        finally
        {
            DesignerRequestProcessor.ProtocolRegistry = previous;
        }
    }

    private static DefaultHttpContext CreateContext(string json)
    {
        var services = new ServiceCollection().AddLogging().AddOptions().BuildServiceProvider();
        var body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Body = body;
        context.Request.ContentLength = body.Length;
        context.Request.ContentType = "application/json; charset=utf-8";
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static JsonDocument ReadJson(DefaultHttpContext context)
    {
        context.Response.Body.Position = 0;
        return JsonDocument.Parse(context.Response.Body);
    }

    private sealed class IndependentConfig
    {
        public int MaxTextSize { get; private set; } = 64;
        public int CharacterWidth { get; private set; } = 5;
        public int LineHeight { get; private set; } = 10;

        internal static IndependentConfig Parse(string json)
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            var config = new IndependentConfig();
            if (document.RootElement.TryGetProperty("maxTextSize", out var max))
                config.MaxTextSize = max.GetInt32();
            return config;
        }
    }

    private sealed class IndependentState
    {
        internal int ValidateCalls { get; set; }
        internal int RenderCalls { get; set; }
        internal List<string> DeviceFonts { get; } = new List<string>();
        internal List<int> FontSizes { get; } = new List<int>();
    }

    private sealed class IndependentProtocolAdapter : ITemplateProtocolAdapter
    {
        private readonly IndependentConfig _config;
        private readonly IndependentState _state;

        internal IndependentProtocolAdapter(IndependentConfig config, IndependentState state)
        {
            _config = config;
            _state = state;
            Capabilities = TemplateCustomProtocolCapabilities.FromCore(CreateCoreCapabilities(config));
        }

        public string ProtocolId => "independent.test";
        public TemplateCustomProtocolCapabilities Capabilities { get; }

        internal static PrintProtocolCapabilities CreateCoreCapabilities(IndependentConfig config)
            => new PrintProtocolCapabilities(300, new[] { 1 }, textSizes: Array.Empty<int>(),
                fontKinds: new[] { 1 }, textRotations: new[] { RotationAngle.None },
                maxPageHeightDots: 4096, supportsCoordinates: false, supportsLegacyDeviceFont: true)
                .WithMaxTextSize(config.MaxTextSize);

        public ITemplateTextMetrics CreateTextMetrics(TemplateTextMeasurementRequest request)
            => new Metrics(_config);

        public TemplateBarcodeMeasurement MeasureBarcode(TemplateBarcodeMeasurementRequest request)
            => throw new NotSupportedException();

        public PrintElement ConvertBarcode(TemplateBarcodeConversionRequest request)
            => throw new NotSupportedException();

        public TemplateQrCodeMeasurement MeasureQrCode(TemplateQrCodeMeasurementRequest request)
            => throw new NotSupportedException();

        public PrintElement ConvertQrCode(TemplateQrCodeConversionRequest request)
            => throw new NotSupportedException();
    }

    private sealed class IndependentBackend : IPrintProtocolBackend
    {
        private readonly IndependentState _state;

        internal IndependentBackend(IndependentState state) => _state = state;

        public string ProtocolId => "independent.test";

        public void Validate(PrintDocument document, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _state.ValidateCalls++;
        }

        public IReadOnlyList<PrintPageOutput> Render(PrintDocument document,
            CancellationToken cancellationToken)
        {
            _state.RenderCalls++;
            _state.DeviceFonts.Clear();
            _state.FontSizes.Clear();
            foreach (var page in document.Pages)
            foreach (var element in page.Elements)
                if (element is PrintTextElement text)
                {
                    _state.DeviceFonts.Add(text.DeviceFontName);
                    _state.FontSizes.Add(text.FontSize);
                }
            return document.Pages.Select(_ => new PrintPageOutput(new[]
            {
                new PrintOutputSegment(Encoding.UTF8.GetBytes("device-font"))
            })).ToArray();
        }

        public void Dispose() { }
    }

    private sealed class Metrics : ITemplateTextMetrics
    {
        private readonly int _characterWidth;

        internal Metrics(IndependentConfig config)
        {
            _characterWidth = config.CharacterWidth;
            LineHeight = config.LineHeight;
        }

        public int LineHeight { get; }
        public string FontFamily => string.Empty;
        public bool HasDeviceTextElementCalibrations => false;
        public bool NeedsFullMeasure => false;
        public int Advance(string textElement) => (textElement ?? string.Empty).Length * _characterWidth;
        public long Measure(string text) => (long)(text ?? string.Empty).Length * _characterWidth;
        public TemplateTextBounds MeasureBounds(string text)
            => new TemplateTextBounds(0, 0, Advance(text), LineHeight);
    }
}
