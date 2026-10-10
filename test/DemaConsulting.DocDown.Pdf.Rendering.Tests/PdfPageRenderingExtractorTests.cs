using DemaConsulting.DocDown.Core;
using DemaConsulting.DocDown.Pdf.Rendering;
using DemaConsulting.DocDown.Pdf.Rendering.Tests.TestData;
using DemaConsulting.DocDown.TestSupport;

namespace DemaConsulting.DocDown.Pdf.Rendering.Tests;

/// <summary>
///     Unit tests for <see cref="PdfPageRenderingExtractor"/>: its declared identity, its
///     unconditional availability, its delegation of the managed aspects, and its per-page fault
///     isolation.
/// </summary>
/// <remarks>
///     The delegation and fault-isolation scenarios run through the real <see cref="DocDownEngine"/>
///     so the extractor is exercised exactly as production selects and invokes it. The fault case
///     injects a rasterization function that throws, which is the only reliable way to drive the
///     per-page failure path without depending on the rasterizer failing on cue.
/// </remarks>
public class PdfPageRenderingExtractorTests
{
    /// <summary>Gets the ambient test cancellation token so async calls stay responsive to cancellation.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Proves the extractor declares the stable identity selection and reporting depend on.
    /// </summary>
    [Fact]
    public void PdfPageRenderingExtractor_Descriptor_DeclaresStableIdentity()
    {
        // Arrange & Act
        var extractor = new PdfPageRenderingExtractor();

        // Assert: the identifier, display name, format, and priority stay stable
        Assert.Equal("pdf-rendering", extractor.Id);
        Assert.Equal("PDF pages (CanvasNet.Pdf)", extractor.DisplayName);
        Assert.Contains(DocumentFormat.Pdf, extractor.SupportedFormats);
        Assert.Equal(0, extractor.Priority);
    }

    /// <summary>
    ///     Proves the availability probe is unconditionally available and provides rendered pages.
    /// </summary>
    [Fact]
    public void PdfPageRenderingExtractor_ProbeAvailability_AnyEnvironment_ReportsUnconditionallyAvailable()
    {
        // Arrange
        var extractor = new PdfPageRenderingExtractor();

        // Act
        var availability = extractor.ProbeAvailability();

        // Assert: rendering is always available, with no reason, since CanvasNet.Pdf is fully managed
        Assert.True(availability.IsAvailable);
        Assert.Null(availability.UnavailableReason);
        Assert.True(availability.ProvidesRenderedPages);
    }

    /// <summary>
    ///     Proves the extractor delegates the managed aspects and writes real page PNGs.
    /// </summary>
    [Fact]
    public async Task PdfPageRenderingExtractor_ExtractAsync_RenderRequested_WritesPagePngs()
    {
        // Arrange: an engine with only the rendering backend and a generated document
        using var temp = new TempScratch();
        var engine = new DocDownBuilder().AddPdfRendering().Build();
        var input = WriteFixture(temp, "simple.pdf", RenderingFixtures.SimpleText());
        var scratch = Path.Combine(temp.Path, "out");

        // Act: request rendered pages
        var result = await engine.ExtractAsync(input, scratch, RenderOptions(), Ct);

        // Assert: the extractor produced output through the delegated managed path and rasterized a page
        Assert.Equal(ExtractionOutcome.Produced, result.Outcome);
        Assert.Equal("pdf-rendering", result.SelectedExtractor?.Id);
        Assert.Empty(result.Notes);
        Assert.True(File.Exists(Path.Combine(scratch, "content.md")));
        var page = Path.Combine(scratch, "pages", "page0001.png");
        Assert.True(File.Exists(page));
        Assert.True(Png.HasSignature(await File.ReadAllBytesAsync(page, Ct)));
        ContractAssert.LayoutPresent(scratch);
    }

    /// <summary>
    ///     Proves rendering is skipped, at no rasterization cost, when the host registers only this
    ///     backend yet does not request page rendering.
    /// </summary>
    /// <remarks>
    ///     When only this backend is registered for <c>.pdf</c>, it is the sole selection candidate
    ///     even with a plain (non-rendering) request, so it must guard its own rendering work
    ///     internally rather than assume selection only ever hands it a rendering request.
    /// </remarks>
    [Fact]
    public async Task PdfPageRenderingExtractor_ExtractAsync_RenderNotRequested_WritesNoPages()
    {
        // Arrange: only the rendering backend is registered, and rendering is not requested
        using var temp = new TempScratch();
        var engine = new DocDownBuilder().AddPdfRendering().Build();
        var input = WriteFixture(temp, "simple.pdf", RenderingFixtures.SimpleText());
        var scratch = Path.Combine(temp.Path, "out");

        // Act
        var result = await engine.ExtractAsync(input, scratch, new ExtractionOptions { RenderPages = false }, Ct);

        // Assert: plain extraction succeeds, but no pages folder content is written
        Assert.Equal(ExtractionOutcome.Produced, result.Outcome);
        Assert.Equal("pdf-rendering", result.SelectedExtractor?.Id);
        Assert.Empty(result.Notes);
        Assert.Empty(result.PagePaths);
        Assert.True(File.Exists(Path.Combine(scratch, "content.md")));
        ContractAssert.LayoutPresent(scratch);
    }

    /// <summary>
    ///     Proves a page whose rasterization faults becomes a recorded note, not an exception.
    /// </summary>
    [Fact]
    public async Task PdfPageRenderingExtractor_ExtractAsync_PageRenderFaults_ReportsNoteWithoutThrowing()
    {
        // Arrange: a rendering backend whose rasterization function always throws
        using var temp = new TempScratch();
        var engine = new DocDownBuilder()
            .AddExtractor(() => new PdfPageRenderingExtractor(
                (_, _, _) => throw new InvalidOperationException("simulated render fault")))
            .Build();
        var input = WriteFixture(temp, "simple.pdf", RenderingFixtures.SimpleText());
        var scratch = Path.Combine(temp.Path, "out");

        // Act: the fault must be isolated to the page, not propagated to the caller
        var result = await engine.ExtractAsync(input, scratch, RenderOptions(), Ct);

        // Assert: the run still produced the standard layout, recorded the page note, and wrote no page file
        Assert.Equal(ExtractionOutcome.Produced, result.Outcome);
        Assert.Equal("pdf-rendering", result.SelectedExtractor?.Id);
        Assert.Contains(
            result.Notes,
            note => string.Equals("Page 1 could not be rasterized.", note.Message, StringComparison.Ordinal));
        Assert.Empty(result.PagePaths);
        ContractAssert.LayoutPresent(scratch);
    }

    /// <summary>
    ///     Proves a page-count fault costs only the rendered pages, not the whole extraction.
    /// </summary>
    [Fact]
    public async Task PdfPageRenderingExtractor_ExtractAsync_PageCountFaults_ReportsNoteAndKeepsDelegatedContent()
    {
        // Arrange: a rendering backend whose page-count function throws before any page is reached
        using var temp = new TempScratch();
        var engine = new DocDownBuilder()
            .AddExtractor(() => new PdfPageRenderingExtractor(
                PageRenderer.Render,
                _ => throw new InvalidOperationException("simulated count fault")))
            .Build();
        var input = WriteFixture(temp, "simple.pdf", RenderingFixtures.SimpleText());
        var scratch = Path.Combine(temp.Path, "out");

        // Act: the count runs before the per-page loop, so its fault must still be isolated
        var result = await engine.ExtractAsync(input, scratch, RenderOptions(), Ct);

        // Assert: the delegated managed content stands and only the pages are reported lost
        Assert.Equal(ExtractionOutcome.Produced, result.Outcome);
        Assert.Null(result.Failure);
        Assert.Contains(
            result.Notes,
            note => string.Equals(
                "Pages could not be counted, so no page images were rendered.",
                note.Message,
                StringComparison.Ordinal));
        Assert.Empty(result.PagePaths);
        ContractAssert.LayoutPresent(scratch);
    }

    /// <summary>
    ///     Proves this backend records its own "no pages were produced" note when the requested
    ///     page range selects no page, without relying on Core.
    /// </summary>
    /// <remarks>
    ///     This note is recorded entirely from this backend's own local knowledge (the selected
    ///     page count), never from counting unrelated notes elsewhere in the sink, so it cannot be
    ///     wrongly suppressed by a note the delegated base backend records for an unrelated reason.
    /// </remarks>
    [Fact]
    public async Task PdfPageRenderingExtractor_ExtractAsync_PageRangeSelectsNoPage_ReportsEmptyPagesNote()
    {
        // Arrange: the generated document has one page; request a range beyond it
        using var temp = new TempScratch();
        var engine = new DocDownBuilder().AddPdfRendering().Build();
        var input = WriteFixture(temp, "simple.pdf", RenderingFixtures.SimpleText());
        var scratch = Path.Combine(temp.Path, "out");
        var options = RenderOptions();
        options.Pages = new PageRange(5, 6);

        // Act
        var result = await engine.ExtractAsync(input, scratch, options, Ct);

        // Assert: a single note explains the empty outcome and no page file was written
        Assert.Equal(ExtractionOutcome.Produced, result.Outcome);
        var note = Assert.Single(result.Notes);
        Assert.Equal(
            "Page rendering was requested and a renderer was available, but no pages were produced.",
            note.Message);
        Assert.Empty(result.PagePaths);
        ContractAssert.LayoutPresent(scratch);
    }

    /// <summary>
    ///     Proves the extractor contributes a render round-trip self-test that always passes.
    /// </summary>
    [Fact]
    public void PdfPageRenderingExtractor_GetSelfTestCases_AnyEnvironment_PassesRenderCase()
    {
        // Arrange
        var extractor = new PdfPageRenderingExtractor();
        using var work = new TempScratch();
        var context = new SelfTestContext(work.Path, Ct);

        // Act: enumerate and run the contributed cases
        var cases = extractor.GetSelfTestCases().ToList();
        var single = Assert.Single(cases);
        var result = single.Run(context);

        // Assert: the case is named distinctly from the base backend's and always passes, since
        // rendering is always available
        Assert.Equal("pdf-rendering.renderRoundTrip", single.Name);
        Assert.Equal("pdf-rendering", single.Category);
        Assert.Equal(SelfTestStatus.Passed, result.Status);
    }

    /// <summary>
    ///     Proves the constructor rejects a null rasterization function.
    /// </summary>
    [Fact]
    public void PdfPageRenderingExtractor_Construct_NullRenderFunction_Throws()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new PdfPageRenderingExtractor(null!));
    }

    /// <summary>
    ///     Creates options requesting rendered pages.
    /// </summary>
    /// <returns>An options instance with rendering enabled.</returns>
    /// <remarks>Keeps the render request in one place for the delegation and fault scenarios.</remarks>
    private static ExtractionOptions RenderOptions() => new() { RenderPages = true };

    /// <summary>
    ///     Materializes a generated fixture as a file on disk.
    /// </summary>
    /// <param name="temp">The owning temporary folder.</param>
    /// <param name="name">The file name, whose extension drives format detection.</param>
    /// <param name="bytes">The generated document bytes.</param>
    /// <returns>The absolute path of the written file.</returns>
    /// <remarks>The name ends in <c>.pdf</c> because detection trusts the file extension.</remarks>
    private static string WriteFixture(TempScratch temp, string name, byte[] bytes)
    {
        var path = Path.Combine(temp.Path, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }
}
