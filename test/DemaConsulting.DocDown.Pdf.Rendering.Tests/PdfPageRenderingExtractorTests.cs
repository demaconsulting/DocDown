using DemaConsulting.DocDown.Pdf.Rendering.Tests.TestData;
using DemaConsulting.DocDown.TestSupport;
using DocDown.Core;
using DocDown.Pdf.Rendering;

namespace DemaConsulting.DocDown.Pdf.Rendering.Tests;

/// <summary>
///     Unit tests for <see cref="PdfPageRenderingExtractor"/>: its declared identity, its cheap
///     non-throwing probe, its delegation of the managed aspects, and its per-page fault isolation.
/// </summary>
/// <remarks>
///     The delegation and fault-isolation scenarios run through the real <see cref="DocDownEngine"/>
///     so the extractor is exercised exactly as production selects and invokes it. The fault case
///     injects a rasterization function that throws, which is the only reliable way to drive the
///     per-page failure path without depending on the native renderer failing on cue.
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
        Assert.Equal("PDF pages (PDFtoImage/PDFium)", extractor.DisplayName);
        Assert.Contains(DocumentFormat.Pdf, extractor.SupportedFormats);
        Assert.Equal(0, extractor.Priority);
    }

    /// <summary>
    ///     Proves the availability probe is non-throwing and reflects the native stack honestly.
    /// </summary>
    [Fact]
    public void PdfPageRenderingExtractor_ProbeAvailability_AnyEnvironment_ReflectsNativeStackWithoutThrowing()
    {
        // Arrange
        var extractor = new PdfPageRenderingExtractor();

        // Act: the probe must not throw
        var availability = extractor.ProbeAvailability();

        // Assert: availability and rendered-page support move together, with a reason only when unavailable
        if (availability.IsAvailable)
        {
            Assert.Null(availability.UnavailableReason);
            Assert.True(availability.ProvidesRenderedPages);
        }
        else
        {
            var reason = Assert.IsType<string>(availability.UnavailableReason);
            Assert.False(string.IsNullOrWhiteSpace(reason));
            Assert.False(availability.ProvidesRenderedPages);
            Assert.StartsWith("PDF page rendering is unavailable: ", reason);
        }
    }

    /// <summary>
    ///     Proves the extractor delegates the managed aspects and writes real page PNGs.
    /// </summary>
    [Fact]
    public async Task PdfPageRenderingExtractor_ExtractAsync_RenderRequested_WritesPagePngs()
    {
        // Arrange: an engine with only the rendering backend and a generated document
        SkipWhenRendererUnavailable();
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
    ///     Proves a page whose rasterization faults becomes a recorded note, not an exception.
    /// </summary>
    [Fact]
    public async Task PdfPageRenderingExtractor_ExtractAsync_PageRenderFaults_ReportsNoteWithoutThrowing()
    {
        // Arrange: a rendering backend whose rasterization function always throws
        SkipWhenRendererUnavailable();
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
        SkipWhenRendererUnavailable();
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
    ///     Proves the extractor contributes a render round-trip self-test whose status reflects the
    ///     native stack honestly.
    /// </summary>
    [Fact]
    public void PdfPageRenderingExtractor_GetSelfTestCases_AnyEnvironment_ReportsHonestRenderCaseStatus()
    {
        // Arrange
        var extractor = new PdfPageRenderingExtractor();
        var probe = PageRenderer.ProbeAvailability();
        using var work = new TempScratch();
        var context = new SelfTestContext(work.Path, Ct);

        // Act: enumerate and run the contributed cases
        var cases = extractor.GetSelfTestCases().ToList();
        var single = Assert.Single(cases);
        var result = single.Run(context);

        // Assert: the case is named distinctly from the base backend's and reports pass or skip honestly
        Assert.Equal("pdf-rendering.renderRoundTrip", single.Name);
        Assert.Equal("pdf-rendering", single.Category);
        Assert.Equal(probe.IsAvailable ? SelfTestStatus.Passed : SelfTestStatus.Skipped, result.Status);
        if (!probe.IsAvailable)
        {
            var reason = Assert.IsType<string>(probe.Reason);
            var message = Assert.IsType<string>(result.Message);
            Assert.Contains(reason, message, StringComparison.Ordinal);
        }
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
    ///     Skips the calling test when the native PDF renderer is unavailable in this environment.
    /// </summary>
    /// <remarks>
    ///     The extractor can only be selected for a real render path when the underlying native
    ///     stack is loadable, so render-path scenarios are not meaningful otherwise.
    /// </remarks>
    private static void SkipWhenRendererUnavailable()
    {
        var probe = PageRenderer.ProbeAvailability();
        Assert.SkipWhen(
            !probe.IsAvailable,
            $"PDF page rendering is unavailable in this environment: {probe.Reason}.");
    }

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
