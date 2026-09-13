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
///     Every scenario runs the real engine over a real deck and confirms the contract verifier finds
///     no violations, so a reported gap always matches what is on disk. Rendering is not requested,
///     so the managed backend is selected and the tests stay green on a CI host without Office.
/// </remarks>
public class DocDownPowerPointTests
{
    /// <summary>A fixed timestamp for reproducible output.</summary>
    private static readonly DateTimeOffset FixedTimestamp = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

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

        Assert.Equal(ExtractionOutcome.Succeeded, result.Outcome);
        var images = Directory.GetFiles(Path.Combine(scratch, "images"));
        Assert.Single(images);
        Assert.EndsWith(".png", images[0], StringComparison.Ordinal);
        ContractAssert.LayoutPresent(scratch);
        ContractAssert.NoViolations(scratch);
    }

    /// <summary>
    ///     Proves a deck whose only image is an EMF vector metafile still reports <see
    ///     cref="ExtractionOutcome.Succeeded"/>: the bytes are written unchanged and counted, the
    ///     <c>PPTX0003</c> caveat is stated as an informational diagnostic, and no images gap is
    ///     opened — a well-formed vector-bearing deck must not degrade.
    /// </summary>
    [Fact]
    public async Task DocDownPowerPoint_Extract_DeckWithVectorImage_Succeeds()
    {
        using var temp = new TempScratch();
        var (scratch, result) = await ExtractAsync(temp, "deck.pptx", PptxFixtures.DeckWithVectorImage(), FixedOptions());

        Assert.Equal(ExtractionOutcome.Succeeded, result.Outcome);
        var images = Directory.GetFiles(Path.Combine(scratch, "images"));
        Assert.Single(images);
        Assert.EndsWith(".emf", images[0], StringComparison.Ordinal);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == "PPTX0003" && diagnostic.Severity == DiagnosticSeverity.Info);
        Assert.DoesNotContain(result.Gaps, gap => gap.Kind == GapKind.Images);
        ContractAssert.LayoutPresent(scratch);
        ContractAssert.NoViolations(scratch);
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
        ContractAssert.NoViolations(scratch);
    }

    /// <summary>
    ///     Proves a deck with speaker notes reports no speaker-notes gap.
    /// </summary>
    [Fact]
    public async Task DocDownPowerPoint_Extract_DeckWithNotes_ReportsNoNotesGap()
    {
        using var temp = new TempScratch();
        var (_, result) = await ExtractAsync(temp, "deck.pptx", PptxFixtures.DeckWithNotes(), FixedOptions());

        Assert.DoesNotContain(result.Gaps, gap => gap.Target == "notes");
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

        Assert.Equal(ExtractionOutcome.Succeeded, result.Outcome);
        Assert.DoesNotContain(result.Gaps, candidate => candidate.Target == "notes");
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Code == "PPTX0002");

        var summary = await File.ReadAllTextAsync(Path.Combine(scratch, "summary.txt"), Ct);
        Assert.Contains("0 sets of speaker notes", summary, StringComparison.Ordinal);

        var manifest = await File.ReadAllTextAsync(Path.Combine(scratch, "manifest.json"), Ct);
        Assert.Contains("\"sets of speaker notes\"", manifest, StringComparison.Ordinal);
        ContractAssert.NoViolations(scratch);
    }

    /// <summary>
    ///     Proves a legacy binary deck fails with a structured refusal whose remedy states plainly the
    ///     format is unsupported and never instructs an installation.
    /// </summary>
    [Fact]
    public async Task DocDownPowerPoint_Extract_LegacyPpt_FailsWithUnsupportedFormatRemedy()
    {
        using var temp = new TempScratch();
        var (scratch, result) = await ExtractAsync(temp, "deck.ppt", PptxFixtures.LegacyPptBytes(), FixedOptions());

        Assert.Equal(ExtractionOutcome.Failed, result.Outcome);
        Assert.NotNull(result.Failure);
        Assert.Equal(ExtractionFailureKind.NoExtractorForFormat, result.Failure.Kind);
        Assert.Contains(
            "DocDown does not support the legacy binary Office formats",
            result.Failure.Remedy!,
            StringComparison.Ordinal);
        Assert.DoesNotContain("install", result.Failure.Remedy!, StringComparison.OrdinalIgnoreCase);
        ContractAssert.LayoutPresent(scratch);
        ContractAssert.NoViolations(scratch);
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
    private static ExtractionOptions FixedOptions() => new() { TimestampUtc = FixedTimestamp };

    /// <summary>Writes a fixture to disk.</summary>
    private static string WriteFixture(TempScratch temp, string name, byte[] bytes)
    {
        var path = Path.Combine(temp.Path, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }
}
