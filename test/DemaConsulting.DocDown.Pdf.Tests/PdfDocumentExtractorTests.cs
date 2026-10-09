using DemaConsulting.DocDown.Core;
using DemaConsulting.DocDown.Pdf;
using DemaConsulting.DocDown.Pdf.Tests.TestData;
using DemaConsulting.DocDown.TestSupport;

namespace DemaConsulting.DocDown.Pdf.Tests;

/// <summary>
///     Unit tests for <see cref="PdfDocumentExtractor"/>, proving its identity, its cheap and safe
///     availability probe, its document-metadata reporting, its environment facts, its page-range
///     handling, its zero-count inventory reporting, and its self-test cases.
/// </summary>
/// <remarks>
///     These tests drive the extractor directly against a <see cref="RecordingSink"/> and a stub
///     context, so what the extractor emitted — and in what order — can be asserted without running
///     the real writers or touching the filesystem. The documents are generated fixtures, so the
///     unit is exercised against real PDFs rather than against a mock parser.
/// </remarks>
public class PdfDocumentExtractorTests
{
    /// <summary>Gets the ambient test cancellation token so async calls stay responsive to cancellation.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Proves the extractor declares the supported identity and selection properties.
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
    }

    /// <summary>
    ///     Proves the availability probe is unconditional, cheap, side-effect free, and does not offer rendering.
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

        // Assert: always available, never claims page rendering, and far inside the 50 ms budget
        Assert.True(availability.IsAvailable);
        Assert.Null(availability.UnavailableReason);
        Assert.False(availability.ProvidesRenderedPages);
        Assert.True(stopwatch.Elapsed.TotalMilliseconds < 50 * 100,
            $"100 probes took {stopwatch.Elapsed.TotalMilliseconds}ms, which exceeds the per-probe budget");
    }

    /// <summary>
    ///     Proves the extractor reports the document's title, author, and page count.
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
        // both attributed to the DemaConsulting.DocDown.Pdf component that reported them
        Assert.Contains(sink.EnvironmentFacts, fact => fact.Key == "pdf.parser" && fact.Available == true && fact.Source == "DemaConsulting.DocDown.Pdf");
        var rendering = Assert.Single(sink.EnvironmentFacts, fact => fact.Key == "pdf.pageRendering");
        Assert.False(rendering.Available);
        Assert.Equal("DemaConsulting.DocDown.Pdf", rendering.Source);
    }

    /// <summary>
    ///     Proves annotations reach the sink as review comments located by page.
    /// </summary>
    /// <remarks>
    ///     The wording of a comment's location belongs to this unit rather than to the annotation
    ///     extractor, so it is asserted here: for a PDF the honest unit of position is the page,
    ///     because an annotation's rectangle locates it on the sheet rather than within the prose.
    /// </remarks>
    [Fact]
    public async Task PdfDocumentExtractor_ExtractAsync_AnnotatedDocument_ReportsReviewComments()
    {
        // Arrange: a recording sink over a two-page annotated document
        var sink = new RecordingSink();

        // Act: extract through the sink
        await ExtractAsync(PdfFixtures.WithAnnotations(), sink);

        // Assert: every reviewer remark arrived, each located by the page it was written on
        Assert.Equal(5, sink.ReviewComments.Count);
        var first = sink.ReviewComments[0];
        Assert.Equal("Alice Reviewer", first.Author);
        Assert.Equal("Section 2 needs a citation.", first.Body);
        Assert.Equal("Page 1", first.Location);
        Assert.Equal("Page 2", sink.ReviewComments[^1].Location);
    }

    /// <summary>
    ///     Proves the content inventory names the document's reviewer comments and how many distinct
    ///     people wrote them, counted from the same annotation walk that produced the comments.
    /// </summary>
    /// <remarks>
    ///     Reviewer commentary leaves <c>content.md</c> entirely, so the inventory is the only place
    ///     <c>summary.txt</c> says a PDF carries any at all; without it a reader paging through the
    ///     summary cannot tell an annotated document from a clean one. The annotations are read once
    ///     and shared between the inventory and the review comments, so the two can never disagree —
    ///     which is what asserting both counts against the same extraction checks. The unattributed
    ///     remark is excluded from the author count because the document names nobody for it.
    /// </remarks>
    [Fact]
    public async Task PdfDocumentExtractor_ExtractAsync_AnnotatedDocument_InventoriesComments()
    {
        // Arrange: a recording sink over the two-page annotated document
        var sink = new RecordingSink();

        // Act: extract through the sink
        await ExtractAsync(PdfFixtures.WithAnnotations(), sink);

        // Assert: the inventory counts match the comments actually reported
        Assert.Contains(sink.ContentFeatures, feature => feature is { Label: "comments", Count: 5, LookedFor: true });
        Assert.Contains(
            sink.ContentFeatures,
            feature => feature is { Label: "distinct comment authors", Count: 4, LookedFor: true });
        Assert.Equal(5, sink.ReviewComments.Count);
    }

    /// <summary>
    ///     Proves a page range narrows reviewer comments along with the content.
    /// </summary>
    /// <remarks>
    ///     Comments and content are driven from the same page selection, so a comment can never arrive
    ///     from a page the output does not contain.
    /// </remarks>
    [Fact]
    public async Task PdfDocumentExtractor_ExtractAsync_PageRange_RestrictsReviewComments()
    {
        // Arrange: the annotated document with only its second page requested
        var sink = new RecordingSink();
        var options = new ExtractionOptions { Pages = new PageRange(2, 2) };

        // Act: extract the requested range
        await ExtractAsync(PdfFixtures.WithAnnotations(), sink, options);

        // Assert: only the second page's remark is reported
        var only = Assert.Single(sink.ReviewComments);
        Assert.Equal("Page 2", only.Location);
        Assert.Equal("Rewrite this conclusion.", only.Body);
    }

    /// <summary>
    ///     Proves a document with no annotations reports no review comments.
    /// </summary>
    [Fact]
    public async Task PdfDocumentExtractor_ExtractAsync_DocumentWithoutAnnotations_ReportsNoReviewComments()
    {
        // Arrange: a recording sink over an ordinary text document
        var sink = new RecordingSink();

        // Act: extract through the sink
        await ExtractAsync(PdfFixtures.SimpleText(), sink);

        // Assert: nothing is reported, so Core has no reason to write a review-comments artifact
        Assert.Empty(sink.ReviewComments);
    }

    /// <summary>
    ///     Proves a requested page range restricts what is extracted.
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
    ///     Proves a text-free scanned document still writes image-backed content and reports zero text counts.
    /// </summary>
    [Fact]
    public async Task PdfDocumentExtractor_ExtractAsync_NoTextLayer_WritesImageBackedContentWithZeroTextCounts()
    {
        // Arrange: a purely graphical page carrying no glyphs at all
        var sink = new RecordingSink();

        // Act: extract the scanned document
        var outcome = await ExtractAsync(PdfFixtures.NoTextLayer(), sink);

        // Assert: the extraction still produces output, but no headings or paragraphs were counted
        Assert.Equal(ExtractionOutcome.Produced, outcome);
        var content = Assert.Single(sink.ContentWrites);
        Assert.Contains("<!-- docdown:page 1 -->", content, StringComparison.Ordinal);
        Assert.Contains("](images/0001-image.png)", content, StringComparison.Ordinal);
        Assert.DoesNotContain("quick brown fox", content, StringComparison.Ordinal);
        Assert.Empty(sink.Notes);
        Assert.Collection(
            sink.ContentFeatures,
            feature =>
            {
                Assert.Equal("pages", feature.Label);
                Assert.Equal(1, feature.Count);
            },
            feature =>
            {
                Assert.Equal("headings", feature.Label);
                Assert.Equal(0, feature.Count);
            },
            feature =>
            {
                Assert.Equal("paragraphs", feature.Label);
                Assert.Equal(0, feature.Count);
            },
            feature =>
            {
                Assert.Equal("inline images", feature.Label);
                Assert.Equal(1, feature.Count);
            },
            feature =>
            {
                Assert.Equal("comments", feature.Label);
                Assert.Equal(0, feature.Count);
            },
            feature =>
            {
                Assert.Equal("distinct comment authors", feature.Label);
                Assert.Equal(0, feature.Count);
            });
    }

    /// <summary>
    ///     Proves a document with no pages completes with explicit zero-count inventory rather than throwing.
    /// </summary>
    [Fact]
    public async Task PdfDocumentExtractor_ExtractAsync_ZeroPageDocument_ReportsZeroCountInventoryWithoutThrowing()
    {
        // Arrange: a valid document declaring no pages
        var sink = new RecordingSink();

        // Act: extract the empty document
        var outcome = await ExtractAsync(PdfFixtures.ZeroPage(), sink);

        // Assert: no exception, a produced outcome, title-only content, and zero-count inventory
        Assert.Equal(ExtractionOutcome.Produced, outcome);
        Assert.Equal(0, Assert.Single(sink.DocumentInfos).PageCount);
        var content = Assert.Single(sink.ContentWrites);
        Assert.StartsWith("# DocDown Empty", content, StringComparison.Ordinal);
        Assert.DoesNotContain("<!-- docdown:page", content, StringComparison.Ordinal);
        Assert.Empty(sink.Images);
        Assert.Empty(sink.Notes);
        Assert.Collection(
            sink.ContentFeatures,
            feature =>
            {
                Assert.Equal("pages", feature.Label);
                Assert.Equal(0, feature.Count);
            },
            feature =>
            {
                Assert.Equal("headings", feature.Label);
                Assert.Equal(0, feature.Count);
            },
            feature =>
            {
                Assert.Equal("paragraphs", feature.Label);
                Assert.Equal(0, feature.Count);
            },
            feature =>
            {
                Assert.Equal("inline images", feature.Label);
                Assert.Equal(0, feature.Count);
            },
            feature =>
            {
                Assert.Equal("comments", feature.Label);
                Assert.Equal(0, feature.Count);
            },
            feature =>
            {
                Assert.Equal("distinct comment authors", feature.Label);
                Assert.Equal(0, feature.Count);
            });
    }

    /// <summary>
    ///     Proves a malformed document's parser fault propagates for Core to convert.
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
    ///     Proves an encrypted document's parser fault propagates for Core to convert.
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
    ///     Proves the extractor contributes the documented self-test cases.
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
    ///     Proves a null context is rejected as a caller error.
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
        DetectedFormat = new FormatDetection(DocumentFormat.Pdf, DetectionBasis.Extension);
        SelectedExtractor = new ExtractorDescriptor("pdf", "PDF (PdfPig)", [DocumentFormat.Pdf], 0);
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
