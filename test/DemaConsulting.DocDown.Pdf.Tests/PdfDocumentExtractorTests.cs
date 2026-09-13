using DemaConsulting.DocDown.Pdf.Tests.TestData;
using DemaConsulting.DocDown.TestSupport;
using DocDown.Core;
using DocDown.Pdf;

namespace DemaConsulting.DocDown.Pdf.Tests;

/// <summary>
///     Unit tests for <see cref="PdfDocumentExtractor"/>, proving its declared identity and
///     capabilities, the cheapness and safety of its availability probe, its document-metadata
///     reporting, its page-range handling, its degradation gaps, and its self-test cases.
/// </summary>
/// <remarks>
///     These tests drive the extractor directly against a <see cref="RecordingSink"/> and a stub
///     context, so what the extractor emitted — and in what order — can be asserted without running
///     the real writers or touching the filesystem. The documents are the generated fixtures, so
///     the unit is exercised against real PDFs rather than against a mock of a parser.
/// </remarks>
public class PdfDocumentExtractorTests
{
    /// <summary>Gets the ambient test cancellation token so async calls stay responsive to cancellation.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Proves the extractor declares exactly the capabilities it can deliver (DeclaresCapabilities).
    /// </summary>
    [Fact]
    public void PdfDocumentExtractor_Descriptor_DeclaredProperties_MatchTheSupportedContract()
    {
        // Arrange: a freshly constructed extractor
        var extractor = new PdfDocumentExtractor();

        // Assert: identity, format, and priority are the documented values
        Assert.Equal("pdf", extractor.Id);
        Assert.Equal("PDF (PdfPig)", extractor.DisplayName);
        Assert.Equal([DocumentFormat.Pdf], extractor.SupportedFormats);
        Assert.Equal(0, extractor.Priority);

        // Assert: the three deliverable capabilities are declared and page rendering pointedly is not
        Assert.Equal(
            ExtractorCapabilities.Text | ExtractorCapabilities.EmbeddedImages | ExtractorCapabilities.DocumentMetadata,
            extractor.Capabilities);
        Assert.False(extractor.Capabilities.HasFlag(ExtractorCapabilities.RenderedPages));
    }

    /// <summary>
    ///     Proves the availability probe is unconditional, cheap, and side-effect free (ProbeIsCheapAndSafe).
    /// </summary>
    [Fact]
    public void PdfDocumentExtractor_ProbeAvailability_ManagedOnlyBackend_ReportsAvailableWithoutIo()
    {
        // Arrange: an extractor and a stopwatch to bound the probe's cost
        var extractor = new PdfDocumentExtractor();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act: probe repeatedly, since the contract permits the engine to call this often
        ExtractorAvailability availability = null!;
        for (var attempt = 0; attempt < 100; attempt++)
        {
            availability = extractor.ProbeAvailability();
        }

        stopwatch.Stop();

        // Assert: always available with the full declared set, and far inside the 50 ms budget
        Assert.True(availability.IsAvailable);
        Assert.Null(availability.UnavailableReason);
        Assert.Equal(extractor.Capabilities, availability.EffectiveCapabilities);
        Assert.True(stopwatch.Elapsed.TotalMilliseconds < 50 * 100,
            $"100 probes took {stopwatch.Elapsed.TotalMilliseconds}ms, which exceeds the per-probe budget");
    }

    /// <summary>
    ///     Proves the extractor reports the document's title, author, and page count (ReportsDocumentInfo).
    /// </summary>
    [Fact]
    public async Task PdfDocumentExtractor_ExtractAsync_DocumentWithMetadata_ReportsTitleAuthorAndPageCount()
    {
        // Arrange: a three-page fixture whose metadata the builder recorded
        var sink = new RecordingSink();

        // Act: extract through a recording sink
        await ExtractAsync(PdfFixtures.MultiPage(3), sink);

        // Assert: the metadata the document declares is reported verbatim, and the page count is real
        var info = Assert.Single(sink.DocumentInfos);
        Assert.Equal("DocDown Multi Page", info.Title);
        Assert.Equal("DocDown Test Suite", info.Author);
        Assert.Equal(3, info.PageCount);
    }

    /// <summary>
    ///     Proves the extractor records the parser and the absence of page rendering as environment facts.
    /// </summary>
    [Fact]
    public async Task PdfDocumentExtractor_ExtractAsync_AnyDocument_ReportsEnvironmentFacts()
    {
        // Arrange: a recording sink over a simple document
        var sink = new RecordingSink();

        // Act: extract through the sink
        await ExtractAsync(PdfFixtures.SimpleText(), sink);

        // Assert: the environment names what parsed the document and that rendering is not on offer,
        // both attributed to the DocDown.Pdf component that reported them
        Assert.Contains(sink.EnvironmentFacts, fact => fact.Key == "pdf.parser" && fact.Available == true && fact.Source == "DocDown.Pdf");
        var rendering = Assert.Single(sink.EnvironmentFacts, fact => fact.Key == "pdf.pageRendering");
        Assert.False(rendering.Available);
        Assert.Equal("DocDown.Pdf", rendering.Source);
    }

    /// <summary>
    ///     Proves a requested page range restricts what is extracted (HonorsPageRange).
    /// </summary>
    [Fact]
    public async Task PdfDocumentExtractor_ExtractAsync_PageRange_ExtractsOnlyTheRequestedPages()
    {
        // Arrange: a four-page document with only pages two and three requested
        var sink = new RecordingSink();
        var options = new ExtractionOptions { Pages = new PageRange(2, 3) };

        // Act: extract the requested range
        await ExtractAsync(PdfFixtures.MultiPage(4), sink, options);

        // Assert: the markdown carries only the in-range pages, and the part count records the slice
        var content = string.Concat(sink.ContentWrites);
        Assert.Contains("Page 2 marker", content, StringComparison.Ordinal);
        Assert.Contains("Page 3 marker", content, StringComparison.Ordinal);
        Assert.DoesNotContain("Page 1 marker", content, StringComparison.Ordinal);
        Assert.DoesNotContain("Page 4 marker", content, StringComparison.Ordinal);
        Assert.Equal(2, sink.DocumentInfos[0].PartCount);
    }

    /// <summary>
    ///     Proves a page-rendering request produces the gap naming where the capability lives (ReportsRenderGap).
    /// </summary>
    [Fact]
    public async Task PdfDocumentExtractor_ExtractAsync_RenderPagesRequested_ReportsPackageClassGap()
    {
        // Arrange: a simple document extracted with page rendering demanded
        var sink = new RecordingSink();
        var options = new ExtractionOptions { RenderPages = true };

        // Act: extract with a capability this backend does not provide
        var outcome = await ExtractAsync(PdfFixtures.SimpleText(), sink, options);

        // Assert: the run degrades and the gap names the class of package that provides rendering
        Assert.Equal(ExtractionOutcome.Degraded, outcome);
        var gap = Assert.Single(sink.Gaps, candidate => candidate.Kind == GapKind.Pages);
        Assert.Equal("pages/", gap.Target);
        Assert.Equal(GapScope.Unavailable, gap.Scope);
        Assert.Contains("page-rendering extractor package", gap.Reason, StringComparison.Ordinal);

        // Assert: the wording states a fact rather than issuing an instruction that cannot succeed
        Assert.DoesNotContain("install", gap.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Null(gap.Remedy);
    }

    /// <summary>
    ///     Proves a document with no text layer is reported rather than silently emptied (ReportsNoTextLayer).
    /// </summary>
    [Fact]
    public async Task PdfDocumentExtractor_ExtractAsync_NoTextLayer_ReportsCodedGapAndDegrades()
    {
        // Arrange: a purely graphical page carrying no glyphs at all
        var sink = new RecordingSink();

        // Act: extract the scanned document
        var outcome = await ExtractAsync(PdfFixtures.NoTextLayer(), sink);

        // Assert: the run degrades with a coded diagnostic and a gap that explains the absence
        Assert.Equal(ExtractionOutcome.Degraded, outcome);
        Assert.Contains(sink.Diagnostics, diagnostic => diagnostic.Code == "PDF0003");
        var gap = Assert.Single(sink.Gaps, candidate => candidate.Kind == GapKind.Text);
        Assert.Equal("content.md", gap.Target);
        Assert.Contains("text layer", gap.Reason, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves a document with no pages completes with an explanation rather than throwing.
    /// </summary>
    [Fact]
    public async Task PdfDocumentExtractor_ExtractAsync_ZeroPageDocument_ReportsStructureGapWithoutThrowing()
    {
        // Arrange: a valid document declaring no pages
        var sink = new RecordingSink();

        // Act: extract the empty document
        var outcome = await ExtractAsync(PdfFixtures.ZeroPage(), sink);

        // Assert: no exception, a degraded outcome, and a gap that states why there is no content
        Assert.Equal(ExtractionOutcome.Degraded, outcome);
        var gap = Assert.Single(sink.Gaps, candidate => candidate.Kind == GapKind.Structure);
        Assert.Contains("no pages", gap.Reason, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves a malformed document's parser fault propagates for Core to convert (MapsParserFailures).
    /// </summary>
    /// <remarks>
    ///     The extractor deliberately does not translate parser faults itself: Core catches any
    ///     backend exception and turns it into a structured failure with the full layout still
    ///     written, so translating here would duplicate that machinery and lose the parser's own
    ///     explanation. This test pins that contract at the unit boundary.
    /// </remarks>
    [Fact]
    public async Task PdfDocumentExtractor_ExtractAsync_MalformedDocument_PropagatesParserFaultForCore()
    {
        // Arrange: a truncated document with no cross-reference table
        var sink = new RecordingSink();

        // Act + Assert: the parser's own fault surfaces rather than being flattened into an outcome
        var exception = await Assert.ThrowsAnyAsync<Exception>(
            async () => await ExtractAsync(PdfFixtures.Malformed(), sink));
        Assert.False(string.IsNullOrWhiteSpace(exception.Message));
    }

    /// <summary>
    ///     Proves an encrypted document's parser fault propagates for Core to convert (MapsParserFailures).
    /// </summary>
    [Fact]
    public async Task PdfDocumentExtractor_ExtractAsync_EncryptedDocument_PropagatesParserFaultForCore()
    {
        // Arrange: a valid document behind a security handler the parser does not implement
        var sink = new RecordingSink();

        // Act + Assert: the fault surfaces with an explanation naming the condition
        var exception = await Assert.ThrowsAnyAsync<Exception>(
            async () => await ExtractAsync(PdfFixtures.Encrypted(), sink));
        Assert.Contains("encrypted", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Proves the extractor contributes the documented self-test cases (ContributesSelfTests).
    /// </summary>
    [Fact]
    public void PdfDocumentExtractor_GetSelfTestCases_DeployedBackend_ReturnsParseAndRenderingCases()
    {
        // Arrange: an extractor and a work folder for the cases to run against
        var extractor = new PdfDocumentExtractor();
        using var work = new TempScratch();
        var context = new SelfTestContext(work.Path, Ct);

        // Act: enumerate the cases and run each one
        var cases = extractor.GetSelfTestCases().ToList();
        var parse = Assert.Single(cases, candidate => candidate.Name == "pdf.parseRoundTrip").Run(context);
        var rendering = Assert.Single(cases, candidate => candidate.Name == "pdf.pageRendering").Run(context);

        // Assert: the parser proves itself here, and the capability this package lacks is skipped
        Assert.All(cases, candidate => Assert.Equal("pdf", candidate.Category));
        Assert.Equal(SelfTestStatus.Passed, parse.Status);
        Assert.Equal(SelfTestStatus.Skipped, rendering.Status);
        Assert.False(string.IsNullOrWhiteSpace(rendering.Message));
    }

    /// <summary>
    ///     Proves a null context is rejected as a caller error (boundary).
    /// </summary>
    [Fact]
    public async Task PdfDocumentExtractor_ExtractAsync_NullContext_ThrowsArgumentNullException()
    {
        // Arrange: an extractor and a valid source with no context
        var extractor = new PdfDocumentExtractor();
        var source = DocumentSource.FromStream(new MemoryStream(PdfFixtures.SimpleText()), "simple.pdf");

        // Act + Assert: the context is the extractor's only channel and is mandatory
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await extractor.ExtractAsync(source, null!));
    }

    /// <summary>
    ///     Runs the extractor over a generated document with a recording sink.
    /// </summary>
    /// <param name="document">The generated document bytes.</param>
    /// <param name="sink">The sink to record through.</param>
    /// <param name="options">The options to extract with, or <see langword="null"/> for the defaults.</param>
    /// <returns>The extractor's own view of the outcome.</returns>
    /// <remarks>
    ///     Uses a stream-backed source so the unit's own buffering of a possibly non-seekable stream
    ///     is exercised on every scenario rather than only where it is asserted.
    /// </remarks>
    private static async ValueTask<ExtractionOutcome> ExtractAsync(
        byte[] document, RecordingSink sink, ExtractionOptions? options = null)
    {
        var extractor = new PdfDocumentExtractor();
        using var stream = new MemoryStream(document);
        var source = DocumentSource.FromStream(stream, "fixture.pdf");
        var context = new StubExtractionContext(options ?? new ExtractionOptions(), sink, Ct);
        return await extractor.ExtractAsync(source, context);
    }
}

/// <summary>
///     A minimal <see cref="IExtractionContext"/> for driving an extractor outside the engine.
/// </summary>
/// <remarks>
///     The engine's real context is assembled from a selection result and an environment the engine
///     owns, none of which a unit test of a backend needs. Supplying a plain stand-in keeps these
///     tests scoped to the extractor rather than to the engine that normally builds the context.
///     Immutable after construction.
/// </remarks>
internal sealed class StubExtractionContext : IExtractionContext
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="StubExtractionContext"/> class.
    /// </summary>
    /// <param name="options">The options the extractor should observe.</param>
    /// <param name="sink">The sink the extractor writes through.</param>
    /// <param name="cancellationToken">The token the extractor should observe.</param>
    /// <remarks>Fills the detection, selection, and environment members with values matching the PDF backend.</remarks>
    public StubExtractionContext(ExtractionOptions options, IExtractionSink sink, CancellationToken cancellationToken)
    {
        Options = options;
        Sink = sink;
        CancellationToken = cancellationToken;
        DetectedFormat = new FormatDetection(DocumentFormat.Pdf, DetectionBasis.Extension, 0.9);
        SelectedExtractor = new ExtractorDescriptor(
            "pdf", "PDF (PdfPig)", [DocumentFormat.Pdf],
            ExtractorCapabilities.Text | ExtractorCapabilities.EmbeddedImages | ExtractorCapabilities.DocumentMetadata,
            0);
        Environment = new ExtractionEnvironment("TestOS", "X64", "test-runtime", "test-rid", []);
    }

    /// <inheritdoc />
    public ExtractionOptions Options { get; }

    /// <inheritdoc />
    public IExtractionSink Sink { get; }

    /// <inheritdoc />
    public FormatDetection DetectedFormat { get; }

    /// <inheritdoc />
    public ExtractorDescriptor SelectedExtractor { get; }

    /// <inheritdoc />
    public ExtractionEnvironment Environment { get; }

    /// <inheritdoc />
    public CancellationToken CancellationToken { get; }
}
