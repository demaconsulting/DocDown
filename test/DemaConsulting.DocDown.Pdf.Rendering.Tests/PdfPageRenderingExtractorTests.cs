using DemaConsulting.DocDown.Pdf.Rendering.Tests.TestData;
using DemaConsulting.DocDown.TestSupport;
using DocDown.Core;
using DocDown.Pdf;
using DocDown.Pdf.Rendering;

namespace DemaConsulting.DocDown.Pdf.Rendering.Tests;

/// <summary>
///     Unit tests for <see cref="PdfPageRenderingExtractor"/>: its declared descriptor, its cheap
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
    ///     Proves the extractor declares the full superset descriptor selection reasons about.
    /// </summary>
    [Fact]
    public void PdfPageRenderingExtractor_Descriptor_DeclaresFullSupersetCapabilities()
    {
        // Arrange & Act
        var extractor = new PdfPageRenderingExtractor();

        // Assert: the identifier, format, priority, and the four capabilities
        Assert.Equal("pdf-rendering", extractor.Id);
        Assert.Contains(DocumentFormat.Pdf, extractor.SupportedFormats);
        Assert.Equal(0, extractor.Priority);
        Assert.Equal(
            ExtractorCapabilities.Text | ExtractorCapabilities.EmbeddedImages
            | ExtractorCapabilities.DocumentMetadata | ExtractorCapabilities.RenderedPages,
            extractor.Capabilities);
    }

    /// <summary>
    ///     Proves the availability probe is non-throwing and reports the native stack as usable here.
    /// </summary>
    [Fact]
    public void PdfPageRenderingExtractor_ProbeAvailability_DeployedStack_ReportsAvailableWithoutThrowing()
    {
        // Arrange
        var extractor = new PdfPageRenderingExtractor();

        // Act: the probe must not throw
        var availability = extractor.ProbeAvailability();

        // Assert: available here (the test host ships the native stack), with the full capability set
        Assert.True(availability.IsAvailable, availability.UnavailableReason);
        Assert.Equal(extractor.Capabilities, availability.EffectiveCapabilities);
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

        // Assert: text was delegated to the managed backend (content exists) and a page was rendered
        Assert.Equal("pdf-rendering", result.SelectedExtractor?.Id);
        Assert.True(File.Exists(Path.Combine(scratch, "content.md")));
        var page = Path.Combine(scratch, "pages", "page0001.png");
        Assert.True(File.Exists(page));
        Assert.True(Png.HasSignature(await File.ReadAllBytesAsync(page, Ct)));
        ContractAssert.NoViolations(scratch);
    }

    /// <summary>
    ///     Proves a page whose rasterization faults becomes a counted gap, not an exception.
    /// </summary>
    [Fact]
    public async Task PdfPageRenderingExtractor_ExtractAsync_PageRenderFaults_ReportsCountedGapWithoutThrowing()
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

        // Assert: the run degraded, named the failure with the package's own code, and rendered no page
        Assert.Equal(ExtractionOutcome.Degraded, result.Outcome);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "PDFR0001");
        var gap = Assert.Single(result.Gaps, candidate =>
            candidate.Kind == GapKind.Pages && candidate.Scope == GapScope.PartiallyExtracted
            && candidate.AffectedCount == 1);
        Assert.Contains("1", gap.AffectedItems ?? []);
        Assert.Empty(result.PagePaths);
        ContractAssert.NoViolations(scratch);
    }

    /// <summary>
    ///     Proves the extractor contributes a render round-trip self-test that passes where the
    ///     native stack is deployed.
    /// </summary>
    [Fact]
    public void PdfPageRenderingExtractor_GetSelfTestCases_DeployedStack_ContributesPassingRenderCase()
    {
        // Arrange
        var extractor = new PdfPageRenderingExtractor();
        using var work = new TempScratch();
        var context = new SelfTestContext(work.Path, Ct);

        // Act: enumerate and run the contributed cases
        var cases = extractor.GetSelfTestCases().ToList();
        var single = Assert.Single(cases);
        var result = single.Run(context);

        // Assert: the case is named distinctly from the base backend's and passes here
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
