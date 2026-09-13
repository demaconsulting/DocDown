using System.Text;
using DemaConsulting.DocDown.Pdf.Tests.TestData;
using DemaConsulting.DocDown.TestSupport;
using DocDown.Core;
using DocDown.Pdf;

namespace DemaConsulting.DocDown.Pdf.Tests;

/// <summary>
///     Golden-file tests that render real <c>summary.txt</c> output for representative PDF
///     extractions and compare it, byte for byte, against committed golden files.
/// </summary>
/// <remarks>
///     <para>
///         These make the generated summary a reviewable artifact. The runs use a fixed timestamp
///         and the single managed PDF backend, so the only machine-specific lines are the absolute
///         scratch path, the temporary source path, and the three host-environment lines
///         (operating system, runtime, runtime identifier); each is replaced with a fixed
///         placeholder before comparison. Everything else is deterministic and byte-compared.
///     </para>
///     <para>
///         Setting <c>DOCDOWN_UPDATE_GOLDEN=1</c> rewrites a committed golden, and regeneration is
///         refused when a CI environment is detected, so a golden can never silently self-heal.
///     </para>
/// </remarks>
public class PdfGoldenTests
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
    ///     Proves the summary for a simple PDF matches its committed golden.
    /// </summary>
    [Fact]
    public Task PdfGolden_Simple_MatchesCommittedGolden() =>
        AssertGoldenAsync("summary-pdf-simple.txt", "simple.pdf", PdfFixtures.SimpleText(), new ExtractionOptions { TimestampUtc = FixedTimestamp });

    /// <summary>
    ///     Proves the summary for an undecodable-image scenario matches its committed golden.
    /// </summary>
    [Fact]
    public Task PdfGolden_UndecodableImageNote_MatchesCommittedGolden() =>
        AssertGoldenAsync("summary-pdf-undecodable-image-note.txt", "undecodable.pdf", PdfFixtures.WithUndecodableImage(), new ExtractionOptions { TimestampUtc = FixedTimestamp });

    /// <summary>
    ///     Proves the summary for a page-rendering request with no renderer matches its committed golden.
    /// </summary>
    [Fact]
    public Task PdfGolden_RenderPagesNote_MatchesCommittedGolden() =>
        AssertGoldenAsync(
            "summary-pdf-render-pages-note.txt",
            "render-note.pdf",
            PdfFixtures.SimpleText(),
            new ExtractionOptions { TimestampUtc = FixedTimestamp, RenderPages = true });

    /// <summary>
    ///     Proves the summary for an unreadable encrypted PDF matches its committed golden.
    /// </summary>
    [Fact]
    public Task PdfGolden_UnreadableEncrypted_MatchesCommittedGolden() =>
        AssertGoldenAsync("summary-pdf-unreadable-encrypted.txt", "encrypted.pdf", PdfFixtures.Encrypted(), new ExtractionOptions { TimestampUtc = FixedTimestamp });

    /// <summary>
    ///     Extracts a fixture and asserts the normalized summary matches its committed golden.
    /// </summary>
    /// <param name="goldenName">The golden file name.</param>
    /// <param name="fixtureName">The fixture file name (its extension drives detection).</param>
    /// <param name="bytes">The fixture bytes.</param>
    /// <param name="options">The extraction options to run with.</param>
    /// <returns>A task that completes when the assertion is done.</returns>
    private static async Task AssertGoldenAsync(string goldenName, string fixtureName, byte[] bytes, ExtractionOptions options)
    {
        using var temp = new TempScratch();
        var engine = new DocDownBuilder().AddPdf().Build();
        var input = Path.Combine(temp.Path, fixtureName);
        await File.WriteAllBytesAsync(input, bytes, Ct);
        var scratch = Path.Combine(temp.Path, "out");

        await engine.ExtractAsync(DocumentSource.FromFile(input), scratch, options, Ct);

        var summary = await File.ReadAllTextAsync(Path.Combine(scratch, "summary.txt"), Ct);
        await AssertMatchesGoldenAsync(goldenName, Normalize(summary));
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
        return Path.Combine(root, "test", "DemaConsulting.DocDown.Pdf.Tests", "golden");
    }
}
