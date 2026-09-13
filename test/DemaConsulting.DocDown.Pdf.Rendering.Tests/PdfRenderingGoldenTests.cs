using System.Text;
using DemaConsulting.DocDown.Pdf.Rendering.Tests.TestData;
using DemaConsulting.DocDown.TestSupport;
using DocDown.Core;
using DocDown.Pdf;
using DocDown.Pdf.Rendering;

namespace DemaConsulting.DocDown.Pdf.Rendering.Tests;

/// <summary>
///     Golden-file tests that render real <c>summary.txt</c> output for representative PDF rendering
///     extractions and compare it, byte for byte, against committed golden files.
/// </summary>
/// <remarks>
///     <para>
///         The run uses a fixed timestamp and a synthetic PDF fixture, so the only machine-specific
///         lines are the absolute scratch path, the temporary source path, and the three host
///         environment lines. Each of those lines is normalized away before comparison; everything
///         else is compared byte for byte.
///     </para>
///     <para>
///         Setting <c>DOCDOWN_UPDATE_GOLDEN=1</c> rewrites the committed golden, and regeneration is
///         refused when a CI environment is detected, so a golden can never silently self-heal.
///     </para>
/// </remarks>
public class PdfRenderingGoldenTests
{
    /// <summary>A fixed timestamp so the rendered header is reproducible across runs.</summary>
    private static readonly DateTimeOffset FixedTimestamp = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

    /// <summary>The line prefixes whose values are machine-specific and are normalized away.</summary>
    private static readonly (string Prefix, string Placeholder)[] VariableLines =
    [
        ("Scratch folder  : ", "Scratch folder  : <scratch-folder>"),
        ("Source document : ", "Source document : <source-document>"),
        ("  Operating system   : ", "  Operating system   : <operating-system>"),
        ("  Runtime            : ", "  Runtime            : <runtime>"),
        ("  Runtime identifier : ", "  Runtime identifier : <runtime-identifier>")
    ];

    /// <summary>Gets the ambient test cancellation token.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Proves the summary for a simple rendered PDF matches its committed golden.
    /// </summary>
    [Fact]
    public async Task PdfRenderingGolden_Summary_SimpleRenderedPdf_MatchesCommittedGolden()
    {
        // Arrange: a generated one-page PDF rendered through the real backend
        SkipWhenRendererUnavailable();
        using var temp = new TempScratch();
        var engine = new DocDownBuilder().AddPdf().AddPdfRendering().Build();
        var input = Path.Combine(temp.Path, "simple.pdf");
        await File.WriteAllBytesAsync(input, RenderingFixtures.SimpleText(), Ct);
        var scratch = Path.Combine(temp.Path, "out");
        var options = new ExtractionOptions { TimestampUtc = FixedTimestamp, RenderPages = true };

        // Act: extract and capture the rendered summary
        var result = await engine.ExtractAsync(input, scratch, options, Ct);
        var summary = await File.ReadAllTextAsync(Path.Combine(scratch, "summary.txt"), Ct);

        // Assert: the run produced the invariant layout and the normalized summary matches its golden
        Assert.Equal(ExtractionOutcome.Produced, result.Outcome);
        ContractAssert.LayoutPresent(scratch);
        await AssertMatchesGoldenAsync("summary-pdf-rendering-simple.txt", Normalize(summary));
    }

    /// <summary>
    ///     Normalizes the machine-specific lines so the remainder can be byte-compared.
    /// </summary>
    /// <param name="summary">The rendered summary text.</param>
    /// <returns>The normalized summary.</returns>
    private static string Normalize(string summary)
    {
        var builder = new StringBuilder(summary.Length);
        foreach (var line in summary.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var placeholder = VariableLines.FirstOrDefault(entry => line.StartsWith(entry.Prefix, StringComparison.Ordinal));
            builder.Append(placeholder.Placeholder ?? line).Append('\n');
        }

        builder.Length -= 1;
        return builder.ToString();
    }

    /// <summary>
    ///     Compares the normalized summary against the committed golden, or regenerates it.
    /// </summary>
    /// <param name="goldenName">The golden file name.</param>
    /// <param name="normalized">The normalized summary.</param>
    /// <returns>A task that completes when the comparison is done.</returns>
    private static async Task AssertMatchesGoldenAsync(string goldenName, string normalized)
    {
        var goldenPath = Path.Combine(GoldenFolder(), goldenName);

        if (Environment.GetEnvironmentVariable("DOCDOWN_UPDATE_GOLDEN") == "1")
        {
            Assert.True(
                Environment.GetEnvironmentVariable("CI") is null &&
                Environment.GetEnvironmentVariable("GITHUB_ACTIONS") is null,
                "DOCDOWN_UPDATE_GOLDEN must not be set in CI: golden files would self-heal instead of failing.");
            Directory.CreateDirectory(Path.GetDirectoryName(goldenPath)!);
            await File.WriteAllTextAsync(goldenPath, normalized, new UTF8Encoding(false), Ct);
        }

        Assert.True(File.Exists(goldenPath), $"Golden file '{goldenPath}' does not exist.");
        var expected = (await File.ReadAllTextAsync(goldenPath, Ct)).Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.Equal(expected, normalized);
    }

    /// <summary>
    ///     Locates the committed golden folder by walking up to the solution file.
    /// </summary>
    /// <returns>The absolute path of the golden folder.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the repository root cannot be located.</exception>
    private static string GoldenFolder()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DocDown.slnx")))
        {
            directory = directory.Parent;
        }

        var root = directory?.FullName
            ?? throw new InvalidOperationException("Could not locate the repository root from the test output folder.");
        return Path.Combine(root, "test", "DemaConsulting.DocDown.Pdf.Rendering.Tests", "golden");
    }

    /// <summary>
    ///     Skips the calling test when the native PDF renderer is unavailable in this environment.
    /// </summary>
    /// <remarks>
    ///     The committed golden captures the real rendered-page path, so it is meaningful only where
    ///     the native rendering stack can load.
    /// </remarks>
    private static void SkipWhenRendererUnavailable()
    {
        var probe = PageRenderer.ProbeAvailability();
        Assert.SkipWhen(
            !probe.IsAvailable,
            $"PDF page rendering is unavailable in this environment: {probe.Reason}.");
    }
}
