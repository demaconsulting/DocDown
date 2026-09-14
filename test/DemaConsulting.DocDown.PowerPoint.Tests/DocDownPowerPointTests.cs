using System.Text.Json;
using DemaConsulting.DocDown.PowerPoint.Tests.TestData;
using DemaConsulting.DocDown.TestSupport;
using DocDown.Core;
using DocDown.PowerPoint;

namespace DemaConsulting.DocDown.PowerPoint.Tests;

/// <summary>
///     System-level integration tests for the DocDown PowerPoint extraction system, driven end to
///     end through <see cref="DocDownEngine"/> against decks generated at test time.
/// </summary>
/// <remarks>
///     Every scenario runs the real engine over a real deck and confirms the invariant output
///     layout is present. Rendering is not requested, so the managed backend is selected and the
///     tests stay green on a CI host without Office.
/// </remarks>
public class DocDownPowerPointTests
{
    /// <summary>Gets the ambient test cancellation token.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Proves the managed Open XML backend is the one selected for a modern deck when rendering is
    ///     not requested — the deterministic, CI-verifiable default.
    /// </summary>
    [Fact]
    public async Task DocDownPowerPoint_Extract_Pptx_SelectsOpenXml()
    {
        using var temp = new TempScratch();
        var (_, result) = await ExtractAsync(temp, "deck.pptx", PptxFixtures.DeckWithNotes(), FixedOptions());

        Assert.Equal("powerpoint-openxml", result.SelectedExtractor?.Id);
    }

    /// <summary>
    ///     Proves a deck's embedded image is extracted into the <c>images/</c> folder, byte-for-byte,
    ///     and the run succeeds cleanly with the full contract layout.
    /// </summary>
    [Fact]
    public async Task DocDownPowerPoint_Extract_DeckWithImage_WritesEmbeddedImage()
    {
        using var temp = new TempScratch();
        var (scratch, result) = await ExtractAsync(temp, "deck.pptx", PptxFixtures.DeckWithImage(), FixedOptions());

        Assert.Equal(ExtractionOutcome.Produced, result.Outcome);
        var images = Directory.GetFiles(Path.Combine(scratch, "images"));
        Assert.Single(images);
        Assert.EndsWith(".png", images[0], StringComparison.Ordinal);
        Assert.Empty(result.Notes);
        ContractAssert.LayoutPresent(scratch);
    }

    /// <summary>
    ///     Proves a deck whose only image is an EMF vector metafile still reports <see
    ///     cref="ExtractionOutcome.Produced"/>: the bytes are written unchanged and no vector-only
    ///     caveat is recorded.
    /// </summary>
    [Fact]
    public async Task DocDownPowerPoint_Extract_DeckWithVectorImage_Succeeds()
    {
        using var temp = new TempScratch();
        var (scratch, result) = await ExtractAsync(temp, "deck.pptx", PptxFixtures.DeckWithVectorImage(), FixedOptions());

        Assert.Equal(ExtractionOutcome.Produced, result.Outcome);
        var images = Directory.GetFiles(Path.Combine(scratch, "images"));
        Assert.Single(images);
        Assert.EndsWith(".emf", images[0], StringComparison.Ordinal);
        Assert.Empty(result.Notes);
        ContractAssert.LayoutPresent(scratch);
    }

    /// <summary>
    ///     Proves speaker notes — the half no render can supply — are extracted into the content, and
    ///     the layout is present with no violations.
    /// </summary>
    [Fact]
    public async Task DocDownPowerPoint_Extract_DeckWithNotes_ExtractsSpeakerNotes()
    {
        using var temp = new TempScratch();
        var (scratch, _) = await ExtractAsync(temp, "deck.pptx", PptxFixtures.DeckWithNotes(), FixedOptions());

        var content = await File.ReadAllTextAsync(Path.Combine(scratch, "content.md"), Ct);
        Assert.Contains("Say this out loud on slide one.", content, StringComparison.Ordinal);
        Assert.Contains("Remember the caveat on slide two.", content, StringComparison.Ordinal);
        Assert.Contains("Overview", content, StringComparison.Ordinal);
        ContractAssert.LayoutPresent(scratch);
    }

    /// <summary>
    ///     Proves a deck with speaker notes reports the inventory count in both the summary and the
    ///     manifest.
    /// </summary>
    [Fact]
    public async Task DocDownPowerPoint_Extract_DeckWithNotes_ReportsSpeakerNotesInventory()
    {
        using var temp = new TempScratch();
        var (scratch, result) = await ExtractAsync(temp, "deck.pptx", PptxFixtures.DeckWithNotes(), FixedOptions());

        Assert.Equal(ExtractionOutcome.Produced, result.Outcome);
        Assert.Empty(result.Notes);

        var summary = await File.ReadAllTextAsync(Path.Combine(scratch, "summary.txt"), Ct);
        Assert.Contains("2 sets of speaker notes", summary, StringComparison.Ordinal);

        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(scratch, "manifest.json"), Ct));
        var feature = manifest.RootElement
            .GetProperty("contentFeatures")
            .EnumerateArray()
            .Single(candidate => candidate.GetProperty("label").GetString() == "sets of speaker notes");
        Assert.Equal(2, feature.GetProperty("count").GetInt32());
    }

    /// <summary>
    ///     Proves a deck without speaker notes succeeds cleanly and states the absence as a counted
    ///     zero in the summary's content outline and the manifest's <c>contentFeatures</c> — a
    ///     notes-less deck is well-formed, so the absence is inventory, never a gap or a diagnostic.
    /// </summary>
    /// <remarks>
    ///     This is the end-to-end proof that a looked-for feature survives to both artifacts at zero:
    ///     a consuming agent reading either one can tell "we read every notes slide and there are
    ///     none" from "notes are not something DocDown counts".
    /// </remarks>
    [Fact]
    public async Task DocDownPowerPoint_Extract_DeckWithoutNotes_ReportsZeroNotesInSummaryAndManifest()
    {
        using var temp = new TempScratch();
        var (scratch, result) = await ExtractAsync(temp, "deck.pptx", PptxFixtures.DeckWithoutNotes(), FixedOptions());

        Assert.Equal(ExtractionOutcome.Produced, result.Outcome);
        Assert.Empty(result.Notes);

        var summary = await File.ReadAllTextAsync(Path.Combine(scratch, "summary.txt"), Ct);
        Assert.Contains("0 sets of speaker notes", summary, StringComparison.Ordinal);
        Assert.Contains("Nothing was left incomplete.", summary, StringComparison.Ordinal);

        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(scratch, "manifest.json"), Ct));
        Assert.Equal("3.0", manifest.RootElement.GetProperty("schemaVersion").GetString());
        Assert.Equal("produced", manifest.RootElement.GetProperty("status").GetString());
        Assert.Equal(0, manifest.RootElement.GetProperty("notes").GetArrayLength());
        var feature = manifest.RootElement
            .GetProperty("contentFeatures")
            .EnumerateArray()
            .Single(candidate => candidate.GetProperty("label").GetString() == "sets of speaker notes");
        Assert.Equal(0, feature.GetProperty("count").GetInt32());
        ContractAssert.LayoutPresent(scratch);
    }

    /// <summary>
    ///     Proves a legacy binary deck fails as unreadable with a structured explanation that states
    ///     plainly the format is unsupported and never instructs an installation.
    /// </summary>
    [Fact]
    public async Task DocDownPowerPoint_Extract_LegacyPpt_FailsWithUnsupportedFormatExplanation()
    {
        using var temp = new TempScratch();
        var (scratch, result) = await ExtractAsync(temp, "deck.ppt", PptxFixtures.LegacyPptBytes(), FixedOptions());

        Assert.Equal(ExtractionOutcome.Unreadable, result.Outcome);
        var failure = Assert.IsType<ExtractionFailure>(result.Failure);
        Assert.Equal("No registered extractor supports the detected format.", failure.Summary);
        Assert.Contains(
            "DocDown does not support the legacy binary Office formats",
            failure.Explanation,
            StringComparison.Ordinal);
        Assert.DoesNotContain("install", failure.Explanation, StringComparison.OrdinalIgnoreCase);

        var summary = await File.ReadAllTextAsync(Path.Combine(scratch, "summary.txt"), Ct);
        Assert.Contains("UNREADABLE  - no output could be produced; see \"Failure\"", summary, StringComparison.Ordinal);
        Assert.Contains("No backend was selected. See \"Failure\" for the reason.", summary, StringComparison.Ordinal);

        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(scratch, "manifest.json"), Ct));
        Assert.Equal("unreadable", manifest.RootElement.GetProperty("status").GetString());
        Assert.True(manifest.RootElement.TryGetProperty("failure", out _));

        ContractAssert.LayoutPresent(scratch);
    }

    /// <summary>
    ///     Extracts a fixture through the full PowerPoint system and returns the folder and result.
    /// </summary>
    private static async Task<(string Folder, ExtractionResult Result)> ExtractAsync(
        TempScratch temp, string name, byte[] bytes, ExtractionOptions options)
    {
        var engine = new DocDownBuilder().AddPowerPoint().Build();
        var input = WriteFixture(temp, name, bytes);
        var scratch = Path.Combine(temp.Path, "out");
        var result = await engine.ExtractAsync(DocumentSource.FromFile(input), scratch, options, Ct);
        return (scratch, result);
    }

    /// <summary>Creates options carrying the fixed timestamp for reproducible output.</summary>
    private static ExtractionOptions FixedOptions() => new();

    /// <summary>Writes a fixture to disk.</summary>
    private static string WriteFixture(TempScratch temp, string name, byte[] bytes)
    {
        var path = Path.Combine(temp.Path, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }
}
