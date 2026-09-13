using DemaConsulting.DocDown.TestSupport;
using DemaConsulting.DocDown.Word.Tests.TestData;
using DocDown.Core;
using DocDown.Word;
using DocDown.Word.OpenXml;
using CoreFormat = DocDown.Core.DocumentFormat;

namespace DemaConsulting.DocDown.Word.Tests.OpenXml;

/// <summary>
///     Integration tests for the Open XML backend, driven through the engine against documents
///     synthesized at test time.
/// </summary>
public class WordOpenXmlExtractorTests
{
    /// <summary>A fixed timestamp for reproducible output.</summary>
    private static readonly DateTimeOffset FixedTimestamp = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

    /// <summary>Gets the ambient test cancellation token.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Proves the descriptor advertises priority 10 and the declared capabilities.
    /// </summary>
    [Fact]
    public void WordOpenXmlExtractor_Descriptor_HasPriority10AndCapabilities()
    {
        var extractor = new WordOpenXmlExtractor();

        Assert.Equal("word-openxml", extractor.Id);
        Assert.Equal(10, extractor.Priority);
        Assert.Contains(CoreFormat.Docx, extractor.SupportedFormats);
        Assert.True(extractor.Capabilities.HasFlag(ExtractorCapabilities.DocumentStructure));
        Assert.False(extractor.Capabilities.HasFlag(ExtractorCapabilities.RenderedPages));
    }

    /// <summary>
    ///     Proves a force-PNG request that cannot be honored is explained rather than hidden.
    /// </summary>
    [Fact]
    public async Task WordOpenXmlExtractor_Extract_ForcePng_ExplainsUnhonoredMode()
    {
        using var temp = new TempScratch();
        var scratch = await ExtractAsync(temp, "images.docx", DocxFixtures.DocumentWithImage(),
            options => options.ImageOutput = ImageOutputMode.ForcePng);

        Assert.Contains(scratch.Result.Diagnostics, diagnostic => diagnostic.Code == "WORD0007");
        Assert.Contains(scratch.Result.Gaps, gap => gap.Target == "images/"
            && gap.Reason.Contains("PNG output was requested", StringComparison.Ordinal));
        ContractAssert.NoViolations(scratch.Folder);
    }

    /// <summary>
    ///     Proves an EMF image is written as-is with an informational readability caveat rather than
    ///     lost, and — because the bytes are byte-complete and no better environment would yield more
    ///     — the run still reports <see cref="ExtractionOutcome.Succeeded"/> with no images gap.
    /// </summary>
    [Fact]
    public async Task WordOpenXmlExtractor_Extract_VectorImage_WritesAsIsWithCaveat()
    {
        using var temp = new TempScratch();
        var scratch = await ExtractAsync(temp, "vector.docx", DocxFixtures.DocumentWithVectorImage());

        Assert.Equal(ExtractionOutcome.Succeeded, scratch.Result.Outcome);
        Assert.Single(Directory.GetFiles(Path.Combine(scratch.Folder, "images")));
        Assert.Contains(scratch.Result.Diagnostics, diagnostic =>
            diagnostic.Code == "WORD0006" && diagnostic.Severity == DiagnosticSeverity.Info);
        Assert.DoesNotContain(scratch.Result.Gaps, gap => gap.Kind == GapKind.Images);
        ContractAssert.NoViolations(scratch.Folder);
    }

    /// <summary>
    ///     Proves merged table cells produce a counted structural gap.
    /// </summary>
    [Fact]
    public async Task WordOpenXmlExtractor_Extract_MergedCells_ReportsCountedStructuralGap()
    {
        using var temp = new TempScratch();
        var scratch = await ExtractAsync(temp, "merged.docx", DocxFixtures.DocumentWithMergedCells());

        var gap = Assert.Single(scratch.Result.Gaps, candidate => candidate.Kind == GapKind.Structure);
        Assert.Equal(GapScope.PartiallyExtracted, gap.Scope);
        Assert.True(gap.AffectedCount >= 1);
        Assert.Contains(scratch.Result.Diagnostics, diagnostic => diagnostic.Code == "WORD0005");
        ContractAssert.NoViolations(scratch.Folder);
    }

    /// <summary>
    ///     Proves an empty document degrades with an honest no-text gap.
    /// </summary>
    [Fact]
    public async Task WordOpenXmlExtractor_Extract_EmptyDocument_DegradesWithNoTextGap()
    {
        using var temp = new TempScratch();
        var scratch = await ExtractAsync(temp, "empty.docx", DocxFixtures.EmptyDocument());

        Assert.Equal(ExtractionOutcome.Degraded, scratch.Result.Outcome);
        Assert.Contains(scratch.Result.Diagnostics, diagnostic => diagnostic.Code == "WORD0001");
        ContractAssert.NoViolations(scratch.Folder);
    }

    /// <summary>
    ///     Proves a page-rendering request degrades with a reasoned, declarative gap.
    /// </summary>
    [Fact]
    public async Task WordOpenXmlExtractor_Extract_PagesRequested_DegradesWithReasonedGap()
    {
        using var temp = new TempScratch();
        var scratch = await ExtractAsync(temp, "clean.docx", DocxFixtures.CleanDocument(),
            options => options.RenderPages = true);

        Assert.Equal(ExtractionOutcome.Degraded, scratch.Result.Outcome);
        Assert.Contains(scratch.Result.Gaps, gap => gap.Kind == GapKind.Pages
            && gap.Reason.Contains("does not render pages", StringComparison.Ordinal));
        var summary = await File.ReadAllTextAsync(Path.Combine(scratch.Folder, "summary.txt"), Ct);
        Assert.DoesNotContain("install", summary, StringComparison.OrdinalIgnoreCase);
        ContractAssert.NoViolations(scratch.Folder);
    }

    /// <summary>
    ///     Proves per-part mode splits the document at every top-level heading.
    /// </summary>
    [Fact]
    public async Task WordOpenXmlExtractor_Extract_PerPart_SplitsAtHeading1()
    {
        using var temp = new TempScratch();
        var scratch = await ExtractAsync(temp, "twoparts.docx", DocxFixtures.DocumentWithTwoSections(),
            options => options.ContentSplit = ContentSplitMode.PerPart);

        var partsDir = Path.Combine(scratch.Folder, "parts");
        Assert.True(Directory.Exists(partsDir), "a parts/ folder should exist for per-part mode");
        Assert.True(Directory.GetFiles(partsDir).Length >= 2, "each Heading 1 should become a part");
        ContractAssert.NoViolations(scratch.Folder);
    }

    /// <summary>
    ///     Proves a header logo identical to a body image reuses one deduplicated image path.
    /// </summary>
    [Fact]
    public async Task WordOpenXmlReader_Read_HeaderWithLogo_ReusesDeduplicatedImagePath()
    {
        using var temp = new TempScratch();
        var scratch = await ExtractAsync(temp, "logo.docx", DocxFixtures.DocumentWithHeaderLogo());

        Assert.Single(Directory.GetFiles(Path.Combine(scratch.Folder, "images")));
        ContractAssert.NoViolations(scratch.Folder);
    }

    /// <summary>
    ///     Proves the extractor contributes runnable self-test cases describing its own behavior.
    /// </summary>
    [Fact]
    public void WordOpenXmlExtractor_SelfValidation_ReportsCases()
    {
        var engine = new DocDownBuilder().AddWord().Build();
        using var work = new TempScratch();
        var context = new SelfTestContext(work.Path, Ct);

        var cases = engine.GetSelfTestCases().Where(candidate => candidate.Category == "word-openxml").ToList();
        var results = cases.Select(candidate => candidate.Run(context)).ToList();

        Assert.Equal(2, cases.Count);
        Assert.Contains(results, result => result.Status == SelfTestStatus.Passed);
        Assert.Contains(results, result => result.Status == SelfTestStatus.Skipped);
        Assert.DoesNotContain(results, result => result.Status == SelfTestStatus.Failed);
    }

    /// <summary>
    ///     Proves that under <c>--split per-part</c> an image-bearing document's inline image links
    ///     resolve on disk from their own <c>parts/*.md</c> files, end to end through the engine.
    /// </summary>
    /// <remarks>
    ///     Word splits at each Heading 1 under per-part, routing the image into a <c>parts/</c> file
    ///     where the root-relative <c>images/…</c> link would dangle without the Core write-path
    ///     rewrite. Resolving every link against its containing file pins that the single Core fix also
    ///     covers Word — the class of check that would have caught the defect.
    /// </remarks>
    [Fact]
    public async Task WordOpenXmlExtractor_Extract_PerPartImageLinks_ResolveOnDisk()
    {
        using var temp = new TempScratch();
        var scratch = await ExtractAsync(temp, "images.docx", DocxFixtures.DocumentWithImage(),
            options => options.ContentSplit = ContentSplitMode.PerPart);

        // The image lands in a parts/ file; every inline link must resolve from its own directory
        Assert.True(Directory.Exists(Path.Combine(scratch.Folder, "parts")), "per-part mode should create parts/");
        var resolved = MarkdownImageLinks.AssertAllImageLinksResolveOnDisk(scratch.Folder);
        Assert.True(resolved >= 1);
        ContractAssert.NoViolations(scratch.Folder);
    }

    /// <summary>
    ///     Extracts a fixture through the Open XML backend and returns the folder and result.
    /// </summary>
    /// <param name="temp">The owning temporary folder.</param>
    /// <param name="name">The fixture file name.</param>
    /// <param name="bytes">The fixture bytes.</param>
    /// <param name="configure">An optional options mutation.</param>
    /// <returns>The extraction folder and result.</returns>
    private static async Task<(string Folder, ExtractionResult Result)> ExtractAsync(
        TempScratch temp, string name, byte[] bytes, Action<ExtractionOptions>? configure = null)
    {
        var engine = new DocDownBuilder().AddWord().Build();
        var input = Path.Combine(temp.Path, name);
        await File.WriteAllBytesAsync(input, bytes, Ct);
        var scratch = Path.Combine(temp.Path, "out-" + Path.GetFileNameWithoutExtension(name));
        var options = new ExtractionOptions { TimestampUtc = FixedTimestamp };
        configure?.Invoke(options);
        var result = await engine.ExtractAsync(DocumentSource.FromFile(input), scratch, options, Ct);
        return (scratch, result);
    }
}
