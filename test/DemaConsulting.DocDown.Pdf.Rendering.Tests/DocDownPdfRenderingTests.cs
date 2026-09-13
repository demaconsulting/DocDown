using DemaConsulting.DocDown.Pdf.Rendering.Tests.TestData;
using DemaConsulting.DocDown.TestSupport;
using DocDown.Core;
using DocDown.Pdf;
using DocDown.Pdf.Rendering;

namespace DemaConsulting.DocDown.Pdf.Rendering.Tests;

/// <summary>
///     System-level integration tests for the DocDown.Pdf.Rendering system, driven end to end
///     through <see cref="DocDownEngine"/> against PDFs generated at test time and rasterized by the
///     real PDFium-backed renderer.
/// </summary>
/// <remarks>
///     <para>
///         The highest-value scenario produces a genuine rendered page and asserts it is a valid PNG
///         with plausible dimensions, not merely that a file appeared. Every scenario also confirms
///         the invariant DocDown layout exists, so the rendering package's contribution is verified
///         against the same contract a host consumes.
///     </para>
///     <para>
///         Selection is exercised both ways: with page rendering requested the rendering backend
///         must win, and with it not requested the lighter managed backend must win on the
///         identifier tie-break so no native code is touched. Rendering-required scenarios skip when
///         the native stack is unavailable in this environment, because absence of the native stack
///         is an environmental fact, not a product failure.
///     </para>
/// </remarks>
public class DocDownPdfRenderingTests
{
    /// <summary>A fixed timestamp used to make output byte-reproducible across runs.</summary>
    private static readonly DateTimeOffset FixedTimestamp = new(2024, 1, 2, 3, 4, 5, TimeSpan.Zero);

    /// <summary>Gets the ambient test cancellation token so async calls stay responsive to cancellation.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Proves a page-rendering request produces a real, valid PNG page of plausible dimensions.
    /// </summary>
    [Fact]
    public async Task DocDownPdfRendering_Render_GeneratedPdf_ProducesValidPngPages()
    {
        // Arrange: an engine with both PDF backends and a generated single-page document
        SkipWhenRendererUnavailable();
        using var temp = new TempScratch();
        var engine = BuildEngine();
        var input = WriteFixture(temp, "simple.pdf", RenderingFixtures.SimpleText());
        var scratch = Path.Combine(temp.Path, "out");
        var options = RenderingOptions();

        // Act: extract with page rendering requested at the default 150 DPI
        var result = await engine.ExtractAsync(input, scratch, options, Ct);

        // Assert: the rendering backend was chosen, the run produced output, and no note reports a shortfall
        Assert.Equal(ExtractionOutcome.Produced, result.Outcome);
        Assert.Equal("pdf-rendering", result.SelectedExtractor?.Id);
        Assert.Empty(result.Notes);

        // Assert: a page image exists on disk and the contract layout is present
        var pagePath = Path.Combine(scratch, "pages", "page0001.png");
        Assert.True(File.Exists(pagePath), $"expected a rendered page at '{pagePath}'");
        ContractAssert.LayoutPresent(scratch);

        // Assert: the file is a valid PNG, not merely present
        var bytes = await File.ReadAllBytesAsync(pagePath, Ct);
        Assert.True(Png.HasSignature(bytes), "the rendered page does not carry the PNG signature");
        var (width, height) = Png.ReadDimensions(bytes);

        // Assert: the dimensions are plausible for A4 at 150 DPI and portrait-oriented
        Assert.InRange(width, 1100, 1400);
        Assert.InRange(height, 1600, 1900);
        Assert.True(height > width, "an A4 page should render taller than it is wide");
    }

    /// <summary>
    ///     Proves repeated renders of the same document in the same environment are byte-identical.
    /// </summary>
    [Fact]
    public async Task DocDownPdfRendering_Render_Deterministic_ProducesByteIdenticalPages()
    {
        // Arrange: one document rendered twice into separate scratch folders
        SkipWhenRendererUnavailable();
        using var temp = new TempScratch();
        var engine = BuildEngine();
        var input = WriteFixture(temp, "simple.pdf", RenderingFixtures.SimpleText());
        var first = Path.Combine(temp.Path, "first");
        var second = Path.Combine(temp.Path, "second");

        // Act: render the same input twice
        var firstResult = await engine.ExtractAsync(input, first, RenderingOptions(), Ct);
        var secondResult = await engine.ExtractAsync(input, second, RenderingOptions(), Ct);

        // Assert: both runs completed cleanly
        Assert.Equal(ExtractionOutcome.Produced, firstResult.Outcome);
        Assert.Equal(ExtractionOutcome.Produced, secondResult.Outcome);
        ContractAssert.LayoutPresent(first);
        ContractAssert.LayoutPresent(second);

        // Assert: the rendered page bytes are identical across the two runs
        ContractAssert.FileEquals(Path.Combine(first, "pages", "page0001.png"), Path.Combine(second, "pages", "page0001.png"));
    }

    /// <summary>
    ///     Proves the rendering backend is selected when page rendering is requested.
    /// </summary>
    [Fact]
    public async Task DocDownPdfRendering_Select_PagesRequested_RenderingBackendWins()
    {
        // Arrange
        SkipWhenRendererUnavailable();
        using var temp = new TempScratch();
        var engine = BuildEngine();
        var input = WriteFixture(temp, "simple.pdf", RenderingFixtures.SimpleText());
        var scratch = Path.Combine(temp.Path, "out");

        // Act: request rendered pages
        var result = await engine.ExtractAsync(input, scratch, RenderingOptions(), Ct);

        // Assert: the rendering backend won, pages were produced, and nothing was left incomplete
        Assert.Equal(ExtractionOutcome.Produced, result.Outcome);
        Assert.Equal("pdf-rendering", result.SelectedExtractor?.Id);
        Assert.NotEmpty(result.PagePaths);
        Assert.Empty(result.Notes);
        ContractAssert.LayoutPresent(scratch);
    }

    /// <summary>
    ///     Proves the lighter managed backend is selected when page rendering is not requested.
    /// </summary>
    [Fact]
    public async Task DocDownPdfRendering_Select_PagesNotRequested_BaseBackendWins()
    {
        // Arrange
        using var temp = new TempScratch();
        var engine = BuildEngine();
        var input = WriteFixture(temp, "simple.pdf", RenderingFixtures.SimpleText());
        var scratch = Path.Combine(temp.Path, "out");

        // Act: extract without requesting rendered pages
        var result = await engine.ExtractAsync(input, scratch, FixedOptions(), Ct);

        // Assert: the managed backend won on the identifier tie-break and produced no pages
        Assert.Equal(ExtractionOutcome.Produced, result.Outcome);
        Assert.Equal("pdf", result.SelectedExtractor?.Id);
        Assert.Empty(result.PagePaths);
        Assert.Empty(result.Notes);
        ContractAssert.LayoutPresent(scratch);
    }

    /// <summary>
    ///     Proves a requested page range restricts which pages are rasterized.
    /// </summary>
    [Fact]
    public async Task DocDownPdfRendering_Render_PageRange_RestrictsRenderedPages()
    {
        // Arrange: a five-page document with a page range limiting rendering to pages 2 through 3
        SkipWhenRendererUnavailable();
        using var temp = new TempScratch();
        var engine = BuildEngine();
        var input = WriteFixture(temp, "multi.pdf", RenderingFixtures.MultiPage(5));
        var scratch = Path.Combine(temp.Path, "out");
        var options = RenderingOptions();
        options.Pages = new PageRange(2, 3);

        // Act
        var result = await engine.ExtractAsync(input, scratch, options, Ct);

        // Assert: exactly the two pages in range were rendered, named by their document page number
        Assert.Equal(ExtractionOutcome.Produced, result.Outcome);
        Assert.Equal("pdf-rendering", result.SelectedExtractor?.Id);
        var pageFiles = Directory.GetFiles(Path.Combine(scratch, "pages"), "*.png")
            .Select(Path.GetFileName).OrderBy(name => name, StringComparer.Ordinal).ToList();
        Assert.Equal(["page0002.png", "page0003.png"], pageFiles);
        ContractAssert.LayoutPresent(scratch);
    }

    /// <summary>
    ///     Proves the requested DPI controls the rendered page's pixel dimensions.
    /// </summary>
    [Fact]
    public async Task DocDownPdfRendering_Render_HigherDpi_ProducesLargerPage()
    {
        // Arrange: the same document rendered at two different DPIs
        SkipWhenRendererUnavailable();
        using var temp = new TempScratch();
        var engine = BuildEngine();
        var input = WriteFixture(temp, "simple.pdf", RenderingFixtures.SimpleText());
        var lowScratch = Path.Combine(temp.Path, "low");
        var highScratch = Path.Combine(temp.Path, "high");

        var lowOptions = RenderingOptions();
        lowOptions.PageRenderDpi = 72;
        var highOptions = RenderingOptions();
        highOptions.PageRenderDpi = 200;

        // Act
        var lowResult = await engine.ExtractAsync(input, lowScratch, lowOptions, Ct);
        var highResult = await engine.ExtractAsync(input, highScratch, highOptions, Ct);

        // Assert: both runs completed cleanly
        Assert.Equal(ExtractionOutcome.Produced, lowResult.Outcome);
        Assert.Equal(ExtractionOutcome.Produced, highResult.Outcome);
        ContractAssert.LayoutPresent(lowScratch);
        ContractAssert.LayoutPresent(highScratch);

        // Assert: the higher-DPI render is strictly larger in both dimensions
        var (lowWidth, lowHeight) = Png.ReadDimensions(
            await File.ReadAllBytesAsync(Path.Combine(lowScratch, "pages", "page0001.png"), Ct));
        var (highWidth, highHeight) = Png.ReadDimensions(
            await File.ReadAllBytesAsync(Path.Combine(highScratch, "pages", "page0001.png"), Ct));
        Assert.True(highWidth > lowWidth, "a higher DPI should widen the raster");
        Assert.True(highHeight > lowHeight, "a higher DPI should heighten the raster");
    }

    /// <summary>
    ///     Builds an engine with the managed PDF backend and the rendering backend registered.
    /// </summary>
    /// <returns>A configured engine.</returns>
    /// <remarks>Registers through the public extensions so every test also exercises the seams a host uses.</remarks>
    private static DocDownEngine BuildEngine() => new DocDownBuilder().AddPdf().AddPdfRendering().Build();

    /// <summary>
    ///     Creates options that request rendered pages with the reproducible fixed timestamp.
    /// </summary>
    /// <returns>An options instance with rendering enabled.</returns>
    /// <remarks>The fixed timestamp is what makes the determinism scenario a real comparison.</remarks>
    private static ExtractionOptions RenderingOptions() => new() { TimestampUtc = FixedTimestamp, RenderPages = true };

    /// <summary>
    ///     Creates options carrying the fixed timestamp for reproducible output, with rendering off.
    /// </summary>
    /// <returns>An options instance with a fixed timestamp.</returns>
    /// <remarks>Used by the base-backend selection scenario, which must not request rendered pages.</remarks>
    private static ExtractionOptions FixedOptions() => new() { TimestampUtc = FixedTimestamp };

    /// <summary>
    ///     Skips the calling test when the native PDF renderer is unavailable in this environment.
    /// </summary>
    /// <remarks>
    ///     Rendering scenarios verify genuine raster output, so they are meaningful only where the
    ///     PDFium-backed stack can load. An unavailable renderer is an environment fact, not a
    ///     product failure.
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
