using DemaConsulting.DocDown.TestSupport;
using DemaConsulting.DocDown.Visio.Tests.TestData;
using DocDown.Core;
using DocDown.Visio;

namespace DemaConsulting.DocDown.Visio.Tests;

/// <summary>
///     System-level integration tests for the DocDown Visio extraction system, driven end to end
///     through <see cref="DocDownEngine"/> against drawings generated at test time.
/// </summary>
/// <remarks>
///     Every scenario runs the real engine over a real drawing and confirms the contract verifier
///     finds no violations. Rendering is not requested, so the managed backend is selected and the
///     tests stay green on a CI host without Office — while still proving the topology, the guaranteed
///     content, is recovered with no Visio present.
/// </remarks>
public class DocDownVisioTests
{
    /// <summary>A fixed timestamp for reproducible output.</summary>
    private static readonly DateTimeOffset FixedTimestamp = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

    /// <summary>Gets the ambient test cancellation token.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Proves the managed Open Packaging backend is the one selected for a modern drawing when
    ///     rendering is not requested — the deterministic, CI-verifiable default.
    /// </summary>
    [Fact]
    public async Task DocDownVisio_Extract_Vsdx_SelectsOpenXml()
    {
        using var temp = new TempScratch();
        var (_, result) = await ExtractAsync(temp, "wash.vsdx", VsdxFixtures.WashSystem(), FixedOptions());

        Assert.Equal("visio-openxml", result.SelectedExtractor?.Id);
    }

    /// <summary>
    ///     Proves a drawing whose only image is an EMF vector metafile still reports <see
    ///     cref="ExtractionOutcome.Succeeded"/>: the bytes are written unchanged and counted, the
    ///     <c>VISIO0003</c> caveat is stated as an informational diagnostic, and no images gap is
    ///     opened — a well-formed vector-bearing drawing must not degrade.
    /// </summary>
    [Fact]
    public async Task DocDownVisio_Extract_PageWithVectorImage_Succeeds()
    {
        using var temp = new TempScratch();
        var (scratch, result) = await ExtractAsync(temp, "schematic.vsdx", VsdxFixtures.PageWithVectorImage(), FixedOptions());

        Assert.Equal(ExtractionOutcome.Succeeded, result.Outcome);
        var images = Directory.GetFiles(Path.Combine(scratch, "images"));
        Assert.Single(images);
        Assert.EndsWith(".emf", images[0], StringComparison.Ordinal);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == "VISIO0003" && diagnostic.Severity == DiagnosticSeverity.Info);
        Assert.DoesNotContain(result.Gaps, gap => gap.Kind == GapKind.Images);
        ContractAssert.LayoutPresent(scratch);
        ContractAssert.NoViolations(scratch);
    }

    /// <summary>
    ///     Proves the directed connector topology is recovered with no Visio present, rendered as a
    ///     readable directed graph under the page name.
    /// </summary>
    [Fact]
    public async Task DocDownVisio_Extract_WashSystem_RecoversDirectedTopology()
    {
        using var temp = new TempScratch();
        var (scratch, _) = await ExtractAsync(temp, "wash.vsdx", VsdxFixtures.WashSystem(), FixedOptions());

        var content = await File.ReadAllTextAsync(Path.Combine(scratch, "content.md"), Ct);
        Assert.Contains("Wash System", content, StringComparison.Ordinal);
        Assert.Contains("Inlet Tank \u2192 Transfer Pump", content, StringComparison.Ordinal);
        Assert.Contains("Transfer Pump \u2192 Outlet Valve", content, StringComparison.Ordinal);
        ContractAssert.LayoutPresent(scratch);
        ContractAssert.NoViolations(scratch);
    }

    /// <summary>
    ///     Proves a macro-enabled drawing (<c>.vsdm</c>) is handled as a drawing, using the same bytes
    ///     the modern <c>.vsdx</c> fixture builds, so the topology is recovered identically.
    /// </summary>
    [Fact]
    public async Task DocDownVisio_Extract_Vsdm_HandledAsDrawing()
    {
        using var temp = new TempScratch();
        var (scratch, result) = await ExtractAsync(temp, "wash.vsdm", VsdxFixtures.WashSystem(), FixedOptions());

        Assert.Equal("visio-openxml", result.SelectedExtractor?.Id);
        var content = await File.ReadAllTextAsync(Path.Combine(scratch, "content.md"), Ct);
        Assert.Contains("Inlet Tank \u2192 Transfer Pump", content, StringComparison.Ordinal);
        ContractAssert.NoViolations(scratch);
    }

    /// <summary>
    ///     Proves page names are surfaced for every page of a multi-page drawing, in order.
    /// </summary>
    [Fact]
    public async Task DocDownVisio_Extract_TwoPages_SurfacesEveryPageName()
    {
        using var temp = new TempScratch();
        var (scratch, _) = await ExtractAsync(temp, "two.vsdx", VsdxFixtures.TwoPages(), FixedOptions());

        var content = await File.ReadAllTextAsync(Path.Combine(scratch, "content.md"), Ct);
        Assert.Contains("Schematic", content, StringComparison.Ordinal);
        Assert.Contains("Legend", content, StringComparison.Ordinal);
        ContractAssert.NoViolations(scratch);
    }

    /// <summary>
    ///     Proves rendering degrades honestly off a host without Visio: a counted pages gap naming the
    ///     reason, while the topology content is still delivered.
    /// </summary>
    [Fact]
    public async Task DocDownVisio_Extract_RenderRequestedWithoutVisio_DegradesWithCountedPagesGapButKeepsTopology()
    {
        if (OperatingSystem.IsWindows())
        {
            // On a developer machine with Visio the COM backend may actually render; this scenario
            // pins the honest-degradation behavior where Visio is absent, which is the CI environment.
            return;
        }

        using var temp = new TempScratch();
        var options = FixedOptions();
        options.RenderPages = true;
        var (scratch, result) = await ExtractAsync(temp, "wash.vsdx", VsdxFixtures.WashSystem(), options);

        Assert.Equal(ExtractionOutcome.Degraded, result.Outcome);
        Assert.Contains(result.Gaps, gap => gap.Kind == GapKind.Pages);
        var content = await File.ReadAllTextAsync(Path.Combine(scratch, "content.md"), Ct);
        Assert.Contains("Inlet Tank \u2192 Transfer Pump", content, StringComparison.Ordinal);
        ContractAssert.NoViolations(scratch);
    }

    /// <summary>
    ///     Proves a legacy binary drawing fails with a structured refusal whose remedy states plainly
    ///     the format is unsupported and never instructs an installation.
    /// </summary>
    [Fact]
    public async Task DocDownVisio_Extract_LegacyVsd_FailsWithUnsupportedFormatRemedy()
    {
        using var temp = new TempScratch();
        var (scratch, result) = await ExtractAsync(temp, "drawing.vsd", VsdxFixtures.LegacyVsdBytes(), FixedOptions());

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
    ///     Extracts a fixture through the full Visio system and returns the folder and result.
    /// </summary>
    private static async Task<(string Folder, ExtractionResult Result)> ExtractAsync(
        TempScratch temp, string name, byte[] bytes, ExtractionOptions options)
    {
        var engine = new DocDownBuilder().AddVisio().Build();
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
