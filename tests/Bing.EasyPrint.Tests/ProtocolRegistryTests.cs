using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Bing.EasyPrint.Printing;
using Xunit;

namespace Bing.EasyPrint.Tests
{
    public sealed class ProtocolRegistryTests
    {
        [Fact]
        public void Registry_freezes_and_rejects_duplicate_or_unknown_ids()
        {
            var registry = new PrintProtocolRegistry();
            registry.Register(new PrintProtocolRegistration("demo.one", "Demo One",
                configuration => new FakeBackend("demo.one")));

            Assert.True(registry.Contains("demo.one"));
            var descriptors = registry.Descriptors;
            Assert.Single(descriptors);
            Assert.Equal("demo.one", descriptors[0].Id);

            registry.Freeze();
            Assert.True(registry.IsFrozen);
            Assert.Throws<InvalidOperationException>(() => registry.Register(
                new PrintProtocolRegistration("demo.two", "Demo Two",
                    configuration => new FakeBackend("demo.two"))));
            Assert.Throws<KeyNotFoundException>(() => registry.CreateBackend("missing.protocol"));
            Assert.Throws<ArgumentException>(() => registry.CreateBackend(""));
        }

        [Fact]
        public void Registry_rejects_duplicate_ids_before_freeze()
        {
            var registry = new PrintProtocolRegistry();
            registry.Register(new PrintProtocolRegistration("demo.one", "Demo One",
                configuration => new FakeBackend("demo.one")));

            var error = Assert.Throws<InvalidOperationException>(() => registry.Register(
                new PrintProtocolRegistration("demo.one", "Duplicate",
                    configuration => new FakeBackend("demo.one"))));
            Assert.Contains("重复", error.Message);
        }

        [Fact]
        public void Registry_exposes_typed_capabilities_and_validates_configuration_before_factory()
        {
            var factoryCalls = 0;
            var capabilities = new PrintProtocolCapabilities(203, new[] { 1, 3, 4, 5, 7 },
                barcodeTypes: new[] { BarcodeType.Code128 }, barcodeRotations: new[] { RotationAngle.None },
                qrCodeRotations: new[] { RotationAngle.None, RotationAngle.Rotate90 },
                supportsBarcodes: true, supportsQrCode: true, supportsFilledRectangles: true,
                supportsSoftwareBitmap: true, softwareBitmapElementTypes: new[] { 3, 4 });
            var registry = new PrintProtocolRegistry();
            registry.Register(new PrintProtocolRegistration("demo.typed", "Typed",
                configuration => { factoryCalls++; return new FakeBackend("demo.typed"); },
                capabilities: capabilities,
                configurationValidator: configuration =>
                {
                    if (configuration.Contains("\"bad\"")) throw new ArgumentException("bad config");
                }));
            registry.Freeze();

            var descriptor = Assert.Single(registry.Descriptors);
            Assert.Same(capabilities, descriptor.Capabilities);
            Assert.True(descriptor.Capabilities!.SupportsBarcodeType(BarcodeType.Code128));
            Assert.True(descriptor.Capabilities.SupportsQrCodeRotation(RotationAngle.Rotate90));
            Assert.Throws<ArgumentException>(() => registry.ValidateConfiguration("demo.typed", "{\"bad\":true}"));
            Assert.Throws<ArgumentException>(() => registry.CreateBackend("demo.typed", "{\"bad\":true}"));
            Assert.Equal(0, factoryCalls);
            using var backend = registry.CreateBackend("demo.typed", "{}");
            Assert.Equal(1, factoryCalls);
        }
        [Fact]
        public void Registry_descriptor_snapshot_is_independent_of_registration_inputs()
        {
            var schema = "{\"type\":\"object\"}";
            var registry = new PrintProtocolRegistry();
            registry.Register(new PrintProtocolRegistration("demo.snapshot", "Snapshot",
                configuration => new FakeBackend("demo.snapshot"), schema, "{}", "{}"));

            var first = registry.Descriptors;
            registry.Freeze();
            schema = "{\"type\":\"changed\"}";

            var second = registry.Descriptors;
            Assert.NotSame(first, second);
            Assert.Equal("{\"type\":\"object\"}", second[0].ConfigSchemaJson);
            Assert.Equal("demo.snapshot", second[0].Id);
        }

        [Fact]
        public void Provider_adapter_preserves_ordered_page_bytes_and_dispose()
        {
            var disposed = 0;
            var provider = new FakeProvider(new[]
            {
                new byte[] { 1, 2 },
                new byte[] { 3 },
            });
            using (var backend = new PrintProtocolProviderBackend("cpcl", provider, () => disposed++))
            {
                var document = new PrintDocument();
                document.AddPage(10, 10);
                document.AddPage(20, 20);

                backend.Validate(document, CancellationToken.None);
                var output = backend.Render(document, CancellationToken.None);

                Assert.Equal(2, output.Count);
                Assert.Equal(new byte[] { 1, 2 }, output[0].Data);
                Assert.Equal(new byte[] { 3 }, output[1].Data);
                Assert.Equal(1, provider.ValidateCalls);
            }
            Assert.Equal(1, disposed);
        }

        [Fact]
        public void Builtin_provider_adapter_keeps_page_bytes_and_order()
        {
            var provider = PrintProtocolProviderFactory.Create(PrintProtocol.Cpcl, System.Text.Encoding.UTF8);
            var document = new PrintDocument();
            var page = document.AddPage(100, 50);
            page.Add(new PrintTextElement(2, 3, "ABC", 24));
            provider.Validate(document);
            var expected = provider.Render(document);

            using var backend = new PrintProtocolProviderBackend("cpcl", provider);
            var actual = backend.Render(document, CancellationToken.None);

            Assert.Equal(expected.Count, actual.Count);
            for (var index = 0; index < expected.Count; index++)
                Assert.Equal(expected[index], actual[index].Data);
        }

                [Fact]
        public void Composer_places_finishing_after_job_and_checks_declared_length()
        {
            var backend = new FakeFinishingBackend("demo.finish", true, 4);
            var document = CreateDocument(2);
            var request = new PrintProtocolFinishingRequest
            {
                FeedAmount = 2,
                Cut = true,
                Placement = PrintProtocolFinishingPlacement.JobEnd,
            };

            var result = PrintProtocolOutputComposer.Render(backend, document, request);

            Assert.Equal(2, result.Pages.Count);
            Assert.Single(result.FinishingFiles);
            Assert.Equal(new byte[] { 1 }, result.Pages[0].Data);
            Assert.Equal(new byte[] { 2 }, result.Pages[1].Data);
            Assert.Equal(new byte[] { 7, 7, 7, 7 }, result.FinishingFiles[0].Output.Data);
            Assert.Equal(1, backend.ValidateCalls);
            Assert.Equal(1, backend.FinishValidateCalls);
            Assert.Equal(1, backend.RenderCalls);
        }

        [Fact]
        public void Composer_places_finishing_after_each_page()
        {
            var backend = new FakeFinishingBackend("demo.finish", true, 2);
            var request = new PrintProtocolFinishingRequest
            {
                Placement = PrintProtocolFinishingPlacement.PageEnd,
            };

            var result = PrintProtocolOutputComposer.Render(backend, CreateDocument(2), request);

            Assert.Equal(2, result.Pages.Count);
            Assert.Equal(2, result.FinishingFiles.Count);
            Assert.Equal(new byte[] { 1 }, result.Pages[0].Data);
            Assert.Equal(new byte[] { 2 }, result.Pages[1].Data);
            Assert.Equal(new byte[] { 7, 7 }, result.FinishingFiles[0].Output.Data);
            Assert.Equal(new byte[] { 7, 7 }, result.FinishingFiles[1].Output.Data);
        }

        [Fact]
        public void Composer_counts_each_page_end_finishing_file_against_the_budget()
        {
            var request = new PrintProtocolFinishingRequest
            {
                Placement = PrintProtocolFinishingPlacement.PageEnd,
            };

            Assert.Throws<ArgumentOutOfRangeException>(() => PrintProtocolOutputComposer.Render(
                new FakeFinishingBackend("demo.finish", true, 2), CreateDocument(2), request,
                maxOutputBytes: 5));
            var result = PrintProtocolOutputComposer.Render(
                new FakeFinishingBackend("demo.finish", true, 2), CreateDocument(2), request,
                maxOutputBytes: 6);
            Assert.Equal(2, result.Pages.Count);
            Assert.Equal(2, result.FinishingFiles.Count);
        }
[Fact]
        public void Composer_rejects_unsupported_finishing_mismatched_length_and_budget()
        {
            var document = CreateDocument(1);
            var request = new PrintProtocolFinishingRequest();

            var unsupported = new FakeFinishingBackend("demo.finish", false, 1);
            Assert.Throws<NotSupportedException>(() => PrintProtocolOutputComposer.Render(
                unsupported, document, request));
            Assert.Equal(0, unsupported.RenderCalls);
            Assert.Throws<InvalidOperationException>(() => PrintProtocolOutputComposer.Render(
                new FakeFinishingBackend("demo.finish", true, 3), document, request));
            Assert.Throws<ArgumentOutOfRangeException>(() => PrintProtocolOutputComposer.Render(
                new FakeFinishingBackend("demo.finish", true, 1), document, request, maxOutputBytes: 1));

        }
        [Fact]
        public void Composer_observes_cancellation_before_returning_partial_output()
        {
            var backend = new FakeFinishingBackend("demo.finish", true, 1);
            using var source = new CancellationTokenSource();
            source.Cancel();

            Assert.Throws<OperationCanceledException>(() => PrintProtocolOutputComposer.Render(
                backend, CreateDocument(1), null, cancellationToken: source.Token));
            Assert.Equal(0, backend.RenderCalls);
        }

        [Fact]
        public void Composer_expands_physical_copies_and_orders_page_end_controls()
        {
            var backend = new FakeFinishingBackend("demo.finish", true, 2);
            var document = CreateDocument(1);
            document.Pages[0].Copies = 2;
            var result = PrintProtocolOutputComposer.Render(backend, document, new PrintProtocolFinishingRequest
            {
                Placement = PrintProtocolFinishingPlacement.PageEnd
            });

            Assert.Equal(2, result.Pages.Count);
            Assert.Equal(2, result.FinishingFiles.Count);
            Assert.Equal(new[] { 0, 0 }, result.PageOrigins.Select(origin => origin.SourcePageIndex));
            Assert.Equal(new[] { 0, 1 }, result.PageOrigins.Select(origin => origin.CopyIndex));
            Assert.Equal(new int?[] { 0, 1 }, result.FinishingFiles.Select(control => control.PageIndex));
            Assert.Equal(new[] { 1, 2 }, result.FinishingFiles.Select(control => control.FileIndex));
            Assert.Equal(2, backend.LastDocumentPages);
            Assert.Equal(1, backend.RenderCalls);
        }

        [Fact]
        public void Composer_job_end_runs_once_after_all_physical_copies()
        {
            var backend = new FakeFinishingBackend("demo.finish", true, 2);
            var document = CreateDocument(1);
            document.Pages[0].Copies = 2;
            var result = PrintProtocolOutputComposer.Render(backend, document, new PrintProtocolFinishingRequest
            {
                Placement = PrintProtocolFinishingPlacement.JobEnd
            });

            Assert.Equal(2, result.Pages.Count);
            Assert.Single(result.FinishingFiles);
            Assert.Null(result.FinishingFiles[0].PageIndex);
            Assert.Equal(1, result.FinishingFiles[0].FileIndex);
        }

        [Fact]
        public void Composer_rejects_physical_page_limit_before_backend_work()
        {
            var backend = new FakeFinishingBackend("demo.finish", true, 1);
            var document = CreateDocument(1);
            document.Pages[0].Copies = 101;

            Assert.Throws<ArgumentOutOfRangeException>(() => PrintProtocolOutputComposer.Render(
                backend, document, null, maxOutputBytes: null, maxPages: 100));
            Assert.Equal(0, backend.ValidateCalls);
            Assert.Equal(0, backend.RenderCalls);
        }

        [Fact]
        public void Composer_accepts_physical_page_boundary_and_reports_origins()
        {
            var backend = new FakeFinishingBackend("demo.finish", true, 1);
            var document = CreateDocument(1);
            document.Pages[0].Copies = 100;

            var result = PrintProtocolOutputComposer.Render(backend, document, null, maxOutputBytes: null, maxPages: 100);
            Assert.Equal(100, result.Pages.Count);
            Assert.Equal(100, result.PageOrigins.Count);
            Assert.Equal(99, result.PageOrigins[99].CopyIndex);
            Assert.Equal(1, backend.RenderCalls);
        }

        [Fact]
        public void Composer_releases_temporary_reservations_when_finishing_callback_fails()
        {
            var document = CreateDocument(10);
            using var scope = PrintResourceScope.Begin(8 * 1024 * 1024);
            var backend = new FakeFinishingBackend("demo.finish", true, 1)
            {
                OnFinishing = () => throw new InvalidOperationException("finish failed")
            };
            Assert.Throws<InvalidOperationException>(() => PrintProtocolOutputComposer.Render(
                backend, document, new PrintProtocolFinishingRequest
                {
                    Placement = PrintProtocolFinishingPlacement.PageEnd
                }));
            Assert.Equal(0, scope.CurrentBytes);
        }

        [Fact]
        public void Legacy_composer_preserves_native_page_copies_and_controls()
        {
            var backend = new FakeFinishingBackend("cpcl", true, 1);
            var document = CreateDocument(1);
            document.Pages[0].Copies = 2;
            document.Pages[0].FeedLines = 1;

            var result = PrintProtocolOutputComposer.RenderLegacy(backend, document);

            Assert.Single(result.Pages);
            Assert.Empty(result.PageOrigins);
            Assert.Equal(2, backend.LastPageCopies);
            Assert.Equal(1, backend.LastFeedLines);
        }
        [Fact]
        public void Composer_observes_cancellation_raised_inside_render_and_finishing_callbacks()
        {
            using var renderCancel = new CancellationTokenSource();
            var renderBackend = new FakeFinishingBackend("demo.finish", true, 1)
            {
                OnRender = () => renderCancel.Cancel()
            };
            var pageEnd = new PrintProtocolFinishingRequest
            {
                Placement = PrintProtocolFinishingPlacement.PageEnd
            };
            Assert.Throws<OperationCanceledException>(() => PrintProtocolOutputComposer.Render(
                renderBackend, CreateDocument(1), pageEnd, cancellationToken: renderCancel.Token));
            Assert.Equal(1, renderBackend.RenderCalls);
            Assert.Equal(0, renderBackend.FinishCalls);

            using var finishCancel = new CancellationTokenSource();
            var finishBackend = new FakeFinishingBackend("demo.finish", true, 1)
            {
                OnFinishing = () => finishCancel.Cancel()
            };
            Assert.Throws<OperationCanceledException>(() => PrintProtocolOutputComposer.Render(
                finishBackend, CreateDocument(1), pageEnd, cancellationToken: finishCancel.Token));
            Assert.Equal(1, finishBackend.FinishCalls);
        }

        [Fact]
        public void Composer_propagates_finishing_callback_failure_without_partial_result()
        {
            var backend = new FakeFinishingBackend("demo.finish", true, 1)
            {
                OnFinishing = () => throw new InvalidOperationException("finish failed")
            };
            var error = Assert.Throws<InvalidOperationException>(() => PrintProtocolOutputComposer.Render(
                backend, CreateDocument(1), new PrintProtocolFinishingRequest
                {
                    Placement = PrintProtocolFinishingPlacement.PageEnd
                }));
            Assert.Equal("finish failed", error.Message);
            Assert.Equal(1, backend.FinishCalls);
        }

        private static PrintDocument CreateDocument(int pageCount)
        {
            var document = new PrintDocument();
            for (var index = 0; index < pageCount; index++)
                document.AddPage(100, 100);
            return document;
        }

        private sealed class FakeBackend : IPrintProtocolBackend
        {
            public FakeBackend(string protocolId) => ProtocolId = protocolId;

            public string ProtocolId { get; }
            public int ValidateCalls { get; private set; }

            public void Validate(PrintDocument document, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ValidateCalls++;
            }

            public IReadOnlyList<PrintPageOutput> Render(PrintDocument document,
                CancellationToken cancellationToken)
            {
                var pages = new List<PrintPageOutput>();
                for (var index = 0; index < document.Pages.Count; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    pages.Add(new PrintPageOutput(new[]
                    {
                        new PrintOutputSegment(new[] { (byte)(index + 1) })
                    }));
                }
                return pages;
            }

            public void Dispose() { }
        }

        private sealed class FakeFinishingBackend : IPrintProtocolFinishingBackend
        {
            private readonly int _declaredLength;

            public FakeFinishingBackend(string protocolId, bool supportsFinishing, int declaredLength)
            {
                ProtocolId = protocolId;
                SupportsFinishing = supportsFinishing;
                _declaredLength = declaredLength;
            }

            public string ProtocolId { get; }
            public bool SupportsFinishing { get; }
            public int ValidateCalls { get; private set; }
            public int FinishValidateCalls { get; private set; }
            public int RenderCalls { get; private set; }
            public int FinishCalls { get; private set; }
            public int LastDocumentPages { get; private set; }
            public int LastPageCopies { get; private set; }
            public int LastFeedLines { get; private set; }
            public Action OnRender { get; set; }
            public Action OnFinishing { get; set; }

            public void Validate(PrintDocument document, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ValidateCalls++;
                LastDocumentPages = document.Pages.Count;
                if (document.Pages.Count > 0)
                {
                    LastPageCopies = document.Pages[0].Copies;
                    LastFeedLines = document.Pages[0].FeedLines;
                }
            }

            public IReadOnlyList<PrintPageOutput> Render(PrintDocument document,
                CancellationToken cancellationToken)
            {
                RenderCalls++;
                LastDocumentPages = document.Pages.Count;
                OnRender?.Invoke();
                var pages = new List<PrintPageOutput>();
                for (var index = 0; index < document.Pages.Count; index++)
                    pages.Add(new PrintPageOutput(new[]
                    {
                        new PrintOutputSegment(new[] { (byte)(index + 1) })
                    }));
                return pages;
            }

            public void ValidateFinishing(PrintDocument document, PrintProtocolFinishingRequest request,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                FinishValidateCalls++;
            }

            public long GetFinishingLength(PrintProtocolFinishingRequest request,
                CancellationToken cancellationToken) => _declaredLength;

            public PrintPageOutput RenderFinishing(PrintProtocolFinishingRequest request,
                CancellationToken cancellationToken)
            {
                FinishCalls++;
                OnFinishing?.Invoke();
                var bytes = new byte[_declaredLength == 3 ? 2 : _declaredLength];
                for (var index = 0; index < bytes.Length; index++) bytes[index] = 7;
                return new PrintPageOutput(new[]
                {
                    new PrintOutputSegment(bytes, PrintOutputSegmentKind.Control)
                });
            }

            public void Dispose() { }
        }

        private sealed class FakeProvider : IPrintProtocolProvider
        {
            private readonly IReadOnlyList<byte[]> _pages;

            public FakeProvider(IReadOnlyList<byte[]> pages) => _pages = pages;

            public PrintProtocol Protocol => PrintProtocol.Cpcl;
            public PrintCapabilities Capabilities => null;
            public int ValidateCalls { get; private set; }

            public void Validate(PrintDocument document) => ValidateCalls++;

            public IReadOnlyList<byte[]> Render(PrintDocument document,
                CancellationToken cancellationToken = default) => _pages;
        }
    }
}
