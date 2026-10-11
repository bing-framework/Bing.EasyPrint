using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Bing.EasyPrint.CPCL;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>
/// 验证打印流水线的取消契约、状态恢复和旧 Provider 兼容行为。
/// </summary>
public sealed class PrintPipelineCancellationTests
{
    [Fact]
    public void Legacy_provider_is_not_called_when_validation_token_is_pre_cancelled()
    {
        var provider = new LegacyProvider();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            provider.Validate(DocumentWithTwoTextElements(), cancellation.Token));
        Assert.Equal(0, provider.ValidateCalls);
    }

    [Fact]
    public void Legacy_provider_cancellation_after_validate_is_not_accepted_as_success()
    {
        using var cancellation = new CancellationTokenSource();
        var provider = new LegacyProvider(() => cancellation.Cancel());

        Assert.Throws<OperationCanceledException>(() =>
            provider.Validate(DocumentWithTwoTextElements(), cancellation.Token));
        Assert.Equal(1, provider.ValidateCalls);
    }

    [Fact]
    public void Base_provider_calls_legacy_override_and_exposes_the_scoped_cancellation_token()
    {
        using var cancellation = new CancellationTokenSource();
        var provider = new LegacyOverrideProvider
        {
            CancelInLegacyOverride = true,
            OnLegacyOverride = cancellation.Cancel
        };

        Assert.Throws<OperationCanceledException>(() =>
            provider.Validate(DocumentWithTwoTextElements(), cancellation.Token));
        Assert.Equal(1, provider.LegacyValidateCalls);

        provider.CancelInLegacyOverride = false;
        provider.Validate(DocumentWithTwoTextElements(), CancellationToken.None);
        Assert.Equal(2, provider.LegacyValidateCalls);
    }

    [Fact]
    public void Element_validation_stops_after_first_cancel_and_recovers_with_a_new_token()
    {
        using var cancellation = new CancellationTokenSource();
        var provider = new ElementCancellationProvider
        {
            OnElement = index =>
            {
                if (index == 1) cancellation.Cancel();
            }
        };

        Assert.Throws<OperationCanceledException>(() =>
            provider.Validate(DocumentWithTwoTextElements(), cancellation.Token));
        Assert.Equal(1, provider.ElementCalls);

        provider.OnElement = null;
        provider.ResetElementCalls();
        provider.Validate(DocumentWithTwoTextElements(), CancellationToken.None);
        Assert.Equal(2, provider.ElementCalls);
    }

    [Fact]
    public void Check_does_not_convert_cancellation_into_a_first_issue()
    {
        using var cancellation = new CancellationTokenSource();
        var provider = new LegacyProvider(() => cancellation.Cancel());

        Assert.Throws<OperationCanceledException>(() =>
            provider.Check(DocumentWithTwoTextElements(), cancellation.Token));
    }

    [Fact]
    public void Three_protocol_commands_reject_pre_cancelled_build_and_keep_fresh_output_identical()
    {
        AssertPreCancelledBuildKeepsOutput(token =>
            new CPCLPrintCommand(Encoding.ASCII)
                .SetPage(200, 100)
                .DrawText(10, 10, "OK", 24, 0, false, false, false)
                .Build(token).GetBytes());

        AssertPreCancelledBuildKeepsOutput(token =>
            new EscPosPrintCommand(Encoding.ASCII)
                .Initialize()
                .Text("OK")
                .Build(token).GetBytes());

        AssertPreCancelledBuildKeepsOutput(token =>
            new ZplPrintCommand(Encoding.ASCII)
                .Start()
                .Label(61, 40)
                .Text(1, 2, "OK")
                .End()
                .Build(token).GetBytes());
    }

    [Fact]
    public void Delayed_zpl_build_output_cancellation_preserves_independent_segments_and_recovers()
    {
        using var cancellation = new CancellationTokenSource();
        var encoding = new CancelOnZplControlEncoding(cancellation.Cancel);
        var command = new ZplPrintCommand(encoding, DelayedZplMedia())
            .Start()
            .Label(61, 40)
            .Text(1, 2, "OK")
            .PrintWithCut(new PrintCutOptions { Mode = PrintCutMode.Full })
            .End();

        var expected = command.BuildOutput();
        encoding.Arm();

        Assert.Throws<OperationCanceledException>(() => command.BuildOutput(cancellation.Token));

        var recovered = command.BuildOutput(CancellationToken.None);
        Assert.Equal(expected.Segments.Count, recovered.Segments.Count);
        Assert.Equal(expected.Segments.Select(segment => segment.Kind),
            recovered.Segments.Select(segment => segment.Kind));
        Assert.Equal(expected.Segments.Select(segment => Convert.ToBase64String(segment.Data)),
            recovered.Segments.Select(segment => Convert.ToBase64String(segment.Data)));
    }

    [Fact]
    public void Cpcl_build_cancellation_restores_the_last_writer_and_output()
    {
        var encoding = new CancelOnFirstCharWriteEncoding();
        var command = new CPCLPrintCommand(encoding)
            .SetPage(200, 100)
            .WriteRawLine("T 24 0 10 10 OK");
        var previousWriter = command.Build();
        var previousBytes = previousWriter.GetBytes();

        using var cancellation = new CancellationTokenSource();
        encoding.Arm(cancellation.Cancel);

        Assert.Throws<OperationCanceledException>(() => command.Build(cancellation.Token));
        Assert.Same(previousWriter, command.Writer);
        Assert.Equal(previousBytes, command.Writer.GetBytes());
        Assert.Equal(previousBytes, command.Build().GetBytes());
    }

    [Fact]
    public void Raster_page_cancellation_does_not_append_the_page_and_a_new_token_recovers()
    {
        var command = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster).Initialize();
        var before = command.Build().GetBytes();
        using var cancellation = new CancellationTokenSource();

        Assert.Throws<OperationCanceledException>(() => command.RasterPage(8, 2, canvas =>
        {
            canvas.SetPixel(0, 0);
            cancellation.Cancel();
        }, cancellation.Token));

        Assert.Equal(before, command.Build().GetBytes());

        var recovered = command.RasterPage(8, 2, canvas => canvas.SetPixel(0, 0),
            CancellationToken.None).Build().GetBytes();
        var expected = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster)
            .Initialize()
            .RasterPage(8, 2, canvas => canvas.SetPixel(0, 0))
            .Build().GetBytes();
        Assert.Equal(expected, recovered);
    }

    private static void AssertPreCancelledBuildKeepsOutput(Func<CancellationToken, byte[]> build)
    {
        var before = build(CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() => build(cancellation.Token));
        Assert.Equal(before, build(CancellationToken.None));
    }

    private static PrintDocument DocumentWithTwoTextElements()
    {
        var document = new PrintDocument();
        document.AddPage(100, 100)
            .Add(new PrintTextElement(0, 0, "first", 12), "first")
            .Add(new PrintTextElement(0, 20, "second", 12), "second");
        return document;
    }

    private static ZplMediaOptions DelayedZplMedia()
        => new ZplMediaOptions
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

    private sealed class LegacyProvider : IPrintProtocolProvider
    {
        private readonly Action _onValidate;

        public LegacyProvider(Action onValidate = null) => _onValidate = onValidate;

        public int ValidateCalls { get; private set; }
        public PrintProtocol Protocol => PrintProtocol.Cpcl;
        public PrintCapabilities Capabilities { get; } =
            new PrintCapabilities(PrintProtocol.Cpcl, 200, true, true, true, true, false);

        public void Validate(PrintDocument document)
        {
            ValidateCalls++;
            _onValidate?.Invoke();
        }

        public IReadOnlyList<byte[]> Render(PrintDocument document,
            CancellationToken cancellationToken = default(CancellationToken))
            => Array.Empty<byte[]>();
    }

    private sealed class LegacyOverrideProvider : PrintProtocolProviderBase
    {
        public LegacyOverrideProvider() : base(Encoding.ASCII) { }

        public int LegacyValidateCalls { get; private set; }
        public bool CancelInLegacyOverride { get; set; }
        public Action OnLegacyOverride { get; set; }
        public override PrintProtocol Protocol => PrintProtocol.Cpcl;
        public override PrintCapabilities Capabilities { get; } =
            new PrintCapabilities(PrintProtocol.Cpcl, 200, true, true, true, true, false);

        public override void Validate(PrintDocument document)
        {
            LegacyValidateCalls++;
            if (CancelInLegacyOverride) OnLegacyOverride?.Invoke();
            ValidationCancellationToken.ThrowIfCancellationRequested();
            base.Validate(document);
        }

        public override IReadOnlyList<byte[]> Render(PrintDocument document,
            CancellationToken cancellationToken = default(CancellationToken))
            => Array.Empty<byte[]>();
    }

    private sealed class ElementCancellationProvider : PrintProtocolProviderBase
    {
        public ElementCancellationProvider() : base(Encoding.ASCII) { }

        public int ElementCalls { get; private set; }
        public Action<int> OnElement { get; set; }
        public override PrintProtocol Protocol => PrintProtocol.Cpcl;
        public override PrintCapabilities Capabilities { get; } =
            new PrintCapabilities(PrintProtocol.Cpcl, 200, true, true, true, true, false);

        protected override void ValidateElement(PrintElement element)
        {
            ElementCalls++;
            OnElement?.Invoke(ElementCalls);
            ValidationCancellationToken.ThrowIfCancellationRequested();
            base.ValidateElement(element);
        }

        public void ResetElementCalls() => ElementCalls = 0;

        public override IReadOnlyList<byte[]> Render(PrintDocument document,
            CancellationToken cancellationToken = default(CancellationToken))
            => Array.Empty<byte[]>();
    }

    private sealed class CancelOnFirstCharWriteEncoding : Encoding
    {
        private readonly Encoding _inner = Encoding.ASCII;
        private Action _cancel;
        private bool _armed;
        private int _triggered;

        public void Arm(Action cancel)
        {
            _cancel = cancel ?? throw new ArgumentNullException(nameof(cancel));
            _armed = true;
        }

        private void CancelOnWrite()
        {
            if (_armed && Interlocked.Exchange(ref _triggered, 1) == 0) _cancel();
        }

        public override int CodePage => _inner.CodePage;
        public override string WebName => _inner.WebName;
        public override int GetByteCount(char[] chars, int index, int count)
            => _inner.GetByteCount(chars, index, count);
        public override int GetByteCount(string chars) => _inner.GetByteCount(chars);
        public override byte[] GetBytes(string chars) => _inner.GetBytes(chars);
        public override int GetCharCount(byte[] bytes, int index, int count)
            => _inner.GetCharCount(bytes, index, count);
        public override int GetBytes(char[] chars, int charIndex, int charCount,
            byte[] bytes, int byteIndex)
        {
            CancelOnWrite();
            return _inner.GetBytes(chars, charIndex, charCount, bytes, byteIndex);
        }
        public override int GetChars(byte[] bytes, int byteIndex, int byteCount,
            char[] chars, int charIndex)
            => _inner.GetChars(bytes, byteIndex, byteCount, chars, charIndex);
        public override int GetMaxByteCount(int charCount) => _inner.GetMaxByteCount(charCount);
        public override int GetMaxCharCount(int byteCount) => _inner.GetMaxCharCount(byteCount);
    }

    private sealed class CancelOnZplControlEncoding : Encoding
    {
        private readonly Encoding _inner = Encoding.ASCII;
        private readonly Action _cancel;
        private bool _armed;
        private int _triggered;

        public CancelOnZplControlEncoding(Action cancel) =>
            _cancel = cancel ?? throw new ArgumentNullException(nameof(cancel));

        public void Arm() => _armed = true;

        public override int CodePage => _inner.CodePage;
        public override string WebName => _inner.WebName;
        public override int GetByteCount(char[] chars, int index, int count)
            => _inner.GetByteCount(chars, index, count);
        public override int GetByteCount(string chars) => _inner.GetByteCount(chars);
        public override int GetCharCount(byte[] bytes, int index, int count)
            => _inner.GetCharCount(bytes, index, count);
        public override byte[] GetBytes(string chars)
        {
            if (_armed && chars == "~JK\r\n" && Interlocked.Exchange(ref _triggered, 1) == 0)
                _cancel();
            return _inner.GetBytes(chars);
        }
        public override int GetBytes(string chars, int charIndex, int charCount, byte[] bytes, int byteIndex)
        {
            if (_armed && charIndex == 0 && charCount == chars.Length && chars == "~JK\r\n" &&
                Interlocked.Exchange(ref _triggered, 1) == 0) _cancel();
            return _inner.GetBytes(chars, charIndex, charCount, bytes, byteIndex);
        }
        public override int GetBytes(char[] chars, int charIndex, int charCount,
            byte[] bytes, int byteIndex)
            => _inner.GetBytes(chars, charIndex, charCount, bytes, byteIndex);
        public override int GetChars(byte[] bytes, int byteIndex, int byteCount,
            char[] chars, int charIndex)
            => _inner.GetChars(bytes, byteIndex, byteCount, chars, charIndex);
        public override int GetMaxByteCount(int charCount) => _inner.GetMaxByteCount(charCount);
        public override int GetMaxCharCount(int byteCount) => _inner.GetMaxCharCount(byteCount);
    }
}
