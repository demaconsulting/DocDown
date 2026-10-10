using DemaConsulting.DocDown.Core;
using DemaConsulting.DocDown.Office;
using DemaConsulting.DocDown.TestSupport;
using DemaConsulting.DocDown.Visio.Rendering.Tests.TestData;

namespace DemaConsulting.DocDown.Visio.Rendering.Tests;

/// <summary>
///     System-level integration tests for the DemaConsulting.DocDown.Visio.Rendering system, driven
///     end to end through <see cref="DocDownEngine"/> against the embedded probe drawing and
///     rasterized by the real CanvasNet.Vsdx-backed renderer.
/// </summary>
/// <remarks>
///     <para>
///         The highest-value scenario produces a genuine rendered page and asserts it is a valid
///         PNG with plausible dimensions, not merely that a file appeared. Every scenario also
///         confirms the invariant DocDown layout exists, so the rendering package's contribution is
///         verified against the same contract a host consumes.
///     </para>
///     <para>
///         Selection is a simple two-backend contest: with page rendering requested, this rendering
///         backend (priority 5) is the only backend for <c>.vsdx</c> that provides rendered pages, so
///         it always wins. With pages not requested, the managed Open Packaging backend wins instead,
///         since its priority, 10, outranks the rendering backend's, 5. Rendering through
///         CanvasNet.Vsdx is always available, since it is a fully-managed library with no native
///         stack that could be absent.
///     </para>
/// </remarks>
public class DocDownVisioRenderingTests
{
    /// <summary>Gets the ambient test cancellation token so async calls stay responsive to cancellation.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Proves a page-rendering request produces a real, valid PNG page of plausible dimensions.
    /// </summary>
    [Fact]
    public async Task DocDownVisioRendering_Render_ProbeDrawing_ProducesValidPngPages()
    {
        // Arrange: an engine with every Office backend and the rendering backend, and the embedded probe
        using var temp = new TempScratch();
        var engine = BuildEngine();
        var input = WriteFixture(temp, "probe.vsdx", VsdxRenderingFixtures.Probe());
        var scratch = Path.Combine(temp.Path, "out");
        var options = RenderingOptions();

        // Act: extract with page rendering requested at the default DPI
        var result = await engine.ExtractAsync(input, scratch, options, Ct);

        // Assert: the rendering backend was chosen and the run produced output
        Assert.Equal(ExtractionOutcome.Produced, result.Outcome);
        Assert.Equal("visio-rendering", result.SelectedExtractor?.Id);
        Assert.Empty(result.Notes);

        // Assert: the page image exists on disk and the contract layout is present
        var page1 = Path.Combine(scratch, "pages", "page0001.png");
        Assert.True(File.Exists(page1), $"expected a rendered page at '{page1}'");
        ContractAssert.LayoutPresent(scratch);

        // Assert: the file is a valid PNG with plausible dimensions, not merely present
        var bytes = await File.ReadAllBytesAsync(page1, Ct);
        Assert.True(Png.HasSignature(bytes), "the rendered page does not carry the PNG signature");
        var (width, height) = Png.ReadDimensions(bytes);
        Assert.True(width > 0 && height > 0, $"implausible dimensions {width}x{height}");
    }

    /// <summary>
    ///     Proves repeated renders of the same drawing in the same environment are byte-identical.
    /// </summary>
    [Fact]
    public async Task DocDownVisioRendering_Render_Deterministic_ProducesByteIdenticalPages()
    {
        // Arrange: the probe drawing rendered twice into separate scratch folders
        using var temp = new TempScratch();
        var engine = BuildEngine();
        var input = WriteFixture(temp, "probe.vsdx", VsdxRenderingFixtures.Probe());
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

        // Assert: every rendered page's bytes are identical across the two runs, not just the first
        var firstPages = Directory.GetFiles(Path.Combine(first, "pages"), "*.png")
            .Select(Path.GetFileName).OrderBy(name => name, StringComparer.Ordinal).ToList();
        var secondPages = Directory.GetFiles(Path.Combine(second, "pages"), "*.png")
            .Select(Path.GetFileName).OrderBy(name => name, StringComparer.Ordinal).ToList();
        Assert.Equal(firstPages, secondPages);
        Assert.NotEmpty(firstPages);
        foreach (var page in firstPages)
        {
            ContractAssert.FileEquals(Path.Combine(first, "pages", page!), Path.Combine(second, "pages", page!));
        }
    }

    /// <summary>
    ///     Proves the rendering backend is selected when page rendering is requested, since it is
    ///     the only page-renderer registered for <c>.vsdx</c>.
    /// </summary>
    [Fact]
    public async Task DocDownVisioRendering_Select_PagesRequested_RenderingBackendWins()
    {
        // Arrange
        using var temp = new TempScratch();
        var engine = BuildEngine();
        var input = WriteFixture(temp, "probe.vsdx", VsdxRenderingFixtures.Probe());
        var scratch = Path.Combine(temp.Path, "out");

        // Act: request rendered pages
        var result = await engine.ExtractAsync(input, scratch, RenderingOptions(), Ct);

        // Assert: the rendering backend won — it is the only backend for .vsdx that provides
        // rendered pages — and pages were produced with nothing left incomplete
        Assert.Equal(ExtractionOutcome.Produced, result.Outcome);
        Assert.Equal("visio-rendering", result.SelectedExtractor?.Id);
        Assert.NotEmpty(result.PagePaths);
        Assert.Empty(result.Notes);
        ContractAssert.LayoutPresent(scratch);
    }

    /// <summary>
    ///     Proves the managed Open Packaging backend is selected when page rendering is not requested.
    /// </summary>
    [Fact]
    public async Task DocDownVisioRendering_Select_PagesNotRequested_BaseBackendWins()
    {
        // Arrange
        using var temp = new TempScratch();
        var engine = BuildEngine();
        var input = WriteFixture(temp, "probe.vsdx", VsdxRenderingFixtures.Probe());
        var scratch = Path.Combine(temp.Path, "out");

        // Act: extract without requesting rendered pages
        var result = await engine.ExtractAsync(input, scratch, FixedOptions(), Ct);

        // Assert: the managed Open Packaging backend won — its priority, 10, outranks the rendering
        // backend's, 5 — and produced no pages
        Assert.Equal(ExtractionOutcome.Produced, result.Outcome);
        Assert.Equal("visio-openxml", result.SelectedExtractor?.Id);
        Assert.Empty(result.PagePaths);
        Assert.Empty(result.Notes);
        ContractAssert.LayoutPresent(scratch);
    }

    /// <summary>
    ///     Proves a requested page range restricts which pages are rasterized.
    /// </summary>
    [Fact]
    public async Task DocDownVisioRendering_Render_PageRange_RestrictsRenderedPages()
    {
        // Arrange: the single-page probe drawing with a page range selecting the one page
        using var temp = new TempScratch();
        var engine = BuildEngine();
        var input = WriteFixture(temp, "probe.vsdx", VsdxRenderingFixtures.Probe());
        var scratch = Path.Combine(temp.Path, "out");
        var options = RenderingOptions();
        options.Pages = new PageRange(1, 1);

        // Act
        var result = await engine.ExtractAsync(input, scratch, options, Ct);

        // Assert: exactly the one page in range was rendered, named by its document page number
        Assert.Equal(ExtractionOutcome.Produced, result.Outcome);
        Assert.Equal("visio-rendering", result.SelectedExtractor?.Id);
        var pageFiles = Directory.GetFiles(Path.Combine(scratch, "pages"), "*.png")
            .Select(Path.GetFileName).OrderBy(name => name, StringComparer.Ordinal).ToList();
        Assert.Equal(["page0001.png"], pageFiles);
        ContractAssert.LayoutPresent(scratch);
    }

    /// <summary>
    ///     Proves the requested DPI controls the rendered page's pixel dimensions.
    /// </summary>
    [Fact]
    public async Task DocDownVisioRendering_Render_HigherDpi_ProducesLargerPage()
    {
        // Arrange: the same drawing rendered at two different DPIs
        using var temp = new TempScratch();
        var engine = BuildEngine();
        var input = WriteFixture(temp, "probe.vsdx", VsdxRenderingFixtures.Probe());
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
    ///     Builds an engine with every Office backend and the rendering backend registered.
    /// </summary>
    /// <returns>A configured engine.</returns>
    /// <remarks>Registers through the public extensions so every test also exercises the seams a host uses.</remarks>
    private static DocDownEngine BuildEngine() => new DocDownBuilder().AddOffice().AddVisioRendering().Build();

    /// <summary>
    ///     Creates options that request rendered pages.
    /// </summary>
    /// <returns>An options instance with rendering enabled.</returns>
    private static ExtractionOptions RenderingOptions() => new() { RenderPages = true };

    /// <summary>
    ///     Creates options with rendering off.
    /// </summary>
    /// <returns>A default options instance.</returns>
    /// <remarks>Used by the base-backend selection scenario, which must not request rendered pages.</remarks>
    private static ExtractionOptions FixedOptions() => new();

    /// <summary>
    ///     Materializes a fixture as a file on disk.
    /// </summary>
    /// <param name="temp">The owning temporary folder.</param>
    /// <param name="name">The file name, whose extension drives format detection.</param>
    /// <param name="bytes">The drawing bytes.</param>
    /// <returns>The absolute path of the written file.</returns>
    /// <remarks>The name ends in <c>.vsdx</c> because detection trusts the file extension.</remarks>
    private static string WriteFixture(TempScratch temp, string name, byte[] bytes)
    {
        var path = Path.Combine(temp.Path, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }
}
