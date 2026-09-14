using System.Text.Json;
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
    /// <summary>Gets the ambient test cancellation token.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Proves the descriptor advertises the managed backend's identity and that its availability
    ///     truthfully states page rendering is not provided here.
    /// </summary>
    [Fact]
    public void WordOpenXmlExtractor_Descriptor_HasPriority10AndManagedAvailability()
    {
        var extractor = new WordOpenXmlExtractor();
        var availability = extractor.ProbeAvailability();

        Assert.Equal("word-openxml", extractor.Id);
        Assert.Equal(10, extractor.Priority);
        Assert.Contains(CoreFormat.Docx, extractor.SupportedFormats);
        Assert.True(((IDocumentExtractor)extractor).PageRenderingApplicable);
        Assert.True(availability.IsAvailable);
        Assert.False(availability.ProvidesRenderedPages);
        Assert.Null(availability.UnavailableReason);
    }

    /// <summary>
    ///     Proves an EMF image is written as-is rather than lost, and the run still reports
    ///     <see cref="ExtractionOutcome.Produced"/>.
    /// </summary>
    [Fact]
    public async Task WordOpenXmlExtractor_Extract_VectorImage_WritesAsIs()
    {
        using var temp = new TempScratch();
        var scratch = await ExtractAsync(temp, "vector.docx", DocxFixtures.DocumentWithVectorImage());

        Assert.Equal(ExtractionOutcome.Produced, scratch.Result.Outcome);
        Assert.Single(Directory.GetFiles(Path.Combine(scratch.Folder, "images")));
        Assert.Empty(scratch.Result.Notes);
        ContractAssert.LayoutPresent(scratch.Folder);
    }

    /// <summary>
    ///     Proves merged table cells produce a note describing the flattening required by markdown.
    /// </summary>
    [Fact]
    public async Task WordOpenXmlExtractor_Extract_MergedCells_ReportsFlatteningNote()
    {
        using var temp = new TempScratch();
        var scratch = await ExtractAsync(temp, "merged.docx", DocxFixtures.DocumentWithMergedCells());

        Assert.Equal(ExtractionOutcome.Produced, scratch.Result.Outcome);
        Assert.Contains(scratch.Result.Notes, note => note.Message.Contains("flattened", StringComparison.Ordinal));
        Assert.Contains(scratch.Result.Notes, note => note.Message.Contains("table structure", StringComparison.Ordinal));
        ContractAssert.LayoutPresent(scratch.Folder);
    }

    /// <summary>
    ///     Proves an empty document still produces the invariant layout and records a zero-count text
    ///     inventory instead of a separate gap.
    /// </summary>
    [Fact]
    public async Task WordOpenXmlExtractor_Extract_EmptyDocument_ProducesZeroCountTextInventory()
    {
        using var temp = new TempScratch();
        var scratch = await ExtractAsync(temp, "empty.docx", DocxFixtures.EmptyDocument());

        Assert.Equal(ExtractionOutcome.Produced, scratch.Result.Outcome);
        Assert.Empty(scratch.Result.Notes);

        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(scratch.Folder, "manifest.json"), Ct));
        var textBlocks = manifest.RootElement
            .GetProperty("contentFeatures")
            .EnumerateArray()
            .Single(feature => feature.GetProperty("label").GetString() == "text blocks");

        Assert.Equal(0, textBlocks.GetProperty("count").GetInt32());
        ContractAssert.LayoutPresent(scratch.Folder);
    }

    /// <summary>
    ///     Proves a page-rendering request records a reasoned note without making the extraction unreadable.
    /// </summary>
    [Fact]
    public async Task WordOpenXmlExtractor_Extract_PagesRequested_ReportsReasonedNote()
    {
        using var temp = new TempScratch();
        var scratch = await ExtractAsync(temp, "clean.docx", DocxFixtures.CleanDocument(),
            options => options.RenderPages = true);

        Assert.Equal(ExtractionOutcome.Produced, scratch.Result.Outcome);
        Assert.Contains(scratch.Result.Notes, note => note.Message.Contains("Page rendering was requested", StringComparison.Ordinal));
        Assert.Contains(scratch.Result.Notes, note => note.Message.Contains("pages were not rendered", StringComparison.Ordinal));
        var summary = await File.ReadAllTextAsync(Path.Combine(scratch.Folder, "summary.txt"), Ct);
        Assert.DoesNotContain("install", summary, StringComparison.OrdinalIgnoreCase);
        ContractAssert.LayoutPresent(scratch.Folder);
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
        ContractAssert.LayoutPresent(scratch.Folder);
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
    ///     Proves an image-bearing document's inline image links resolve on disk from the root
    ///     <c>content.md</c>, end to end through the engine.
    /// </summary>
    /// <remarks>
    ///     A Word document is one continuous flow, so its image links stay root-relative and must
    ///     resolve from the scratch root exactly as written.
    /// </remarks>
    [Fact]
    public async Task WordOpenXmlExtractor_Extract_ImageLinks_ResolveOnDisk()
    {
        using var temp = new TempScratch();
        var scratch = await ExtractAsync(temp, "images.docx", DocxFixtures.DocumentWithImage());

        // Every inline link must resolve from the directory of the file that carries it
        var resolved = MarkdownImageLinks.AssertAllImageLinksResolveOnDisk(scratch.Folder);
        Assert.True(resolved >= 1);
        ContractAssert.LayoutPresent(scratch.Folder);
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
        var options = new ExtractionOptions();
        configure?.Invoke(options);
        var result = await engine.ExtractAsync(DocumentSource.FromFile(input), scratch, options, Ct);
        return (scratch, result);
    }
}
