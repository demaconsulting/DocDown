using System.IO.Compression;
using System.Text.Json;
using DemaConsulting.DocDown.TestSupport;
using DemaConsulting.DocDown.Tool.Tests.TestData;
using DemaConsulting.TestResults;
using DemaConsulting.TestResults.IO;
using DocDown.Core;
using DocDown.Excel;
using DocDown.Pdf;
using DocDown.Pdf.Rendering;
using DocDown.PowerPoint;
using DocDown.Visio;
using DocDown.Word;

namespace DemaConsulting.DocDown.Tool.Tests;

/// <summary>
///     System-level integration tests for the <c>docdown</c> command-line tool, driven end to end
///     through the tool's in-process entry point against a PDF generated at test time.
/// </summary>
/// <remarks>
///     Each scenario runs the real tool over a real document and asserts on its captured output and
///     exit code. The tool builds its engine exactly as production does —
///     <c>new DocDownBuilder().AddPdf().AddPdfRendering().AddWord().AddVisio().AddPowerPoint().AddExcel().Build()</c> — so these tests exercise the
///     same explicit, reflection-free registration the shipped tool uses.
/// </remarks>
public class DocDownToolTests
{
    /// <summary>
    ///     Proves a generated PDF extracts cleanly: the tool prints the absolute path to
    ///     <c>summary.txt</c> and exits zero.
    /// </summary>
    [Fact]
    public void DocDownTool_Extract_GeneratedPdf_PrintsAbsoluteSummaryPathAndExitsZero()
    {
        // Arrange: a generated single-page text PDF and a fresh scratch folder
        using var temp = new TempScratch();
        var input = WriteFixture(temp, "input.pdf", ToolPdfFixtures.SimpleText());
        var scratch = Path.Combine(temp.Path, "out");

        // Act: run the tool
        var (exit, log) = CliHarness.Run("--input", input, "--scratch", scratch);

        // Assert: exit zero and an absolute summary.txt path that exists on disk
        Assert.Equal(0, exit);
        Assert.Contains("Extraction produced the output layout.", log, StringComparison.Ordinal);
        Assert.DoesNotContain("note(s) recorded", log, StringComparison.Ordinal);
        var summaryLine = FindSummaryLine(log);
        Assert.True(Path.IsPathFullyQualified(summaryLine));
        Assert.EndsWith("summary.txt", summaryLine, StringComparison.Ordinal);
        Assert.True(File.Exists(summaryLine));
    }

    /// <summary>
    ///     Proves an unreadable input renders the structured failure (not a bare exception) and
    ///     exits non-zero.
    /// </summary>
    [Fact]
    public void DocDownTool_Extract_UnreadableInput_RendersStructuredFailureAndExitsNonZero()
    {
        // Arrange: a path that does not exist
        using var temp = new TempScratch();
        var input = Path.Combine(temp.Path, "missing.pdf");
        var scratch = Path.Combine(temp.Path, "out");

        // Act
        var (exit, log) = CliHarness.Run("--input", input, "--scratch", scratch);

        // Assert: non-zero and the structured explanation prose, not a stack trace
        Assert.Equal(1, exit);
        Assert.Contains("The source document 'missing.pdf' could not be read.", log, StringComparison.Ordinal);
        Assert.Contains("Detected format:", log, StringComparison.Ordinal);
        Assert.DoesNotContain("Extraction failed.", log, StringComparison.Ordinal);
        Assert.DoesNotContain("at DocDown.", log, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves the default engine registers exactly the PDF, Word, Visio, PowerPoint, and Excel
    ///     backends through the explicit builder seams, with no reflection.
    /// </summary>
    [Fact]
    public void DocDownTool_Build_DefaultEngine_RegistersBackendsExplicitly()
    {
        // Arrange & Act: the same one-liner the tool uses to build its engine
        var engine = new DocDownBuilder().AddPdf().AddPdfRendering().AddWord().AddVisio().AddPowerPoint().AddExcel().Build();
        var backends = engine.GetBackends();

        // Assert: every backend package's extractors are registered explicitly
        Assert.Equal(8, backends.Count);
        Assert.Contains(backends, backend => backend.Descriptor.Id == "pdf");
        Assert.Contains(backends, backend => backend.Descriptor.Id == "pdf-rendering");
        Assert.Contains(backends, backend => backend.Descriptor.Id == "word-openxml");
        Assert.Contains(backends, backend => backend.Descriptor.Id == "visio-openxml");
        Assert.Contains(backends, backend => backend.Descriptor.Id == "visio-com");
        Assert.Contains(backends, backend => backend.Descriptor.Id == "powerpoint-openxml");
        Assert.Contains(backends, backend => backend.Descriptor.Id == "powerpoint-com");
        Assert.Contains(backends, backend => backend.Descriptor.Id == "excel-openxml");
    }

    /// <summary>
    ///     Proves <c>--no-images</c> is reported as a note in the produced summary and manifest.
    /// </summary>
    [Fact]
    public void DocDownTool_Extract_NoImages_ReportsRecordedNoteInSummaryAndManifest()
    {
        // Arrange
        using var temp = new TempScratch();
        var input = WriteFixture(temp, "input.pdf", ToolPdfFixtures.SimpleText());
        var scratch = Path.Combine(temp.Path, "out");

        // Act: suppress embedded images
        var (exit, log) = CliHarness.Run("--input", input, "--scratch", scratch, "--no-images");

        // Assert: output is still produced, with one recorded note and the new produced wording
        Assert.Equal(0, exit);
        Assert.Contains("Extraction produced the output layout.", log, StringComparison.Ordinal);
        Assert.Contains("1 note(s) recorded; see summary.txt for detail.", log, StringComparison.Ordinal);

        var summaryPath = FindSummaryLine(log);
        var summary = File.ReadAllText(summaryPath);
        Assert.Contains(
            "Status          : PRODUCED  - the standard output layout was written.",
            summary,
            StringComparison.Ordinal);
        Assert.Contains("Could not read", summary, StringComparison.Ordinal);
        Assert.Contains(
            "Embedded image extraction was disabled by the caller; no images were written.",
            summary,
            StringComparison.Ordinal);

        var manifestPath = Path.Combine(Path.GetDirectoryName(summaryPath)!, "manifest.json");
        Assert.True(File.Exists(manifestPath));
        using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var root = manifest.RootElement;
        Assert.Equal("2.0", root.GetProperty("schemaVersion").GetString());
        var note = Assert.Single(root.GetProperty("notes").EnumerateArray().Select(static element => element.GetString()));
        Assert.Equal("Embedded image extraction was disabled by the caller; no images were written.", note);
        Assert.False(root.TryGetProperty("artifacts", out _));
        Assert.False(root.TryGetProperty("diagnostics", out _));
        Assert.False(root.TryGetProperty("gaps", out _));
        Assert.False(root.TryGetProperty("selection", out _));
    }

    /// <summary>
    ///     Proves <c>--validate --results &lt;file&gt;.trx</c> writes a well-formed TRX file that
    ///     re-parses.
    /// </summary>
    [Fact]
    public void DocDownTool_Validate_ResultsTrx_ProducesWellFormedTrx()
    {
        // Arrange
        using var temp = new TempScratch();
        var trx = Path.Combine(temp.Path, "docdown-validate-windows.trx");

        // Act
        var (exit, _) = CliHarness.Run("--validate", "--results", trx);

        // Assert: the file exists, re-parses, and carries the engine self-test cases
        Assert.Equal(0, exit);
        Assert.True(File.Exists(trx));
        var parsed = TrxSerializer.Deserialize(File.ReadAllText(trx));
        Assert.NotEmpty(parsed.Results);
        Assert.Contains(parsed.Results, r => r.Name == "core.manifest-schema");
        Assert.DoesNotContain(parsed.Results, r => r.Name == "core.gap-accuracy");
        Assert.Contains(parsed.Results, r => r.Name == "pdf.parseRoundTrip");
    }

    /// <summary>
    ///     Proves <c>--validate --results &lt;file&gt;.xml</c> writes a well-formed JUnit file that
    ///     re-parses.
    /// </summary>
    [Fact]
    public void DocDownTool_Validate_ResultsXml_ProducesWellFormedJUnit()
    {
        // Arrange
        using var temp = new TempScratch();
        var xml = Path.Combine(temp.Path, "docdown-validate-windows.xml");

        // Act
        var (exit, _) = CliHarness.Run("--validate", "--results", xml);

        // Assert
        Assert.Equal(0, exit);
        Assert.True(File.Exists(xml));
        var parsed = JUnitSerializer.Deserialize(File.ReadAllText(xml));
        Assert.NotEmpty(parsed.Results);
    }

    /// <summary>
    ///     Proves the always-skipped PDF page-rendering self-test is recorded as
    ///     <see cref="TestOutcome.NotExecuted"/>, distinct from a failure.
    /// </summary>
    [Fact]
    public void DocDownTool_Validate_SkippedPdfRenderCase_RecordedAsNotExecuted()
    {
        // Arrange
        using var temp = new TempScratch();
        var trx = Path.Combine(temp.Path, "docdown-validate-windows.trx");

        // Act
        var (_, _) = CliHarness.Run("--validate", "--results", trx);

        // Assert: the page-rendering case is not-executed, and not a failure
        var parsed = TrxSerializer.Deserialize(File.ReadAllText(trx));
        var render = Assert.Single(parsed.Results, r => r.Name == "pdf.pageRendering");
        Assert.Equal(TestOutcome.NotExecuted, render.Outcome);
        Assert.NotEqual(TestOutcome.Failed, render.Outcome);
    }

    /// <summary>
    ///     Proves the rendering backend's self-test case surfaces under <c>--validate</c> and is
    ///     recorded as passed where the native stack is available or not-executed where it is not —
    ///     never as a failure, and always under a name distinct from the base backend's case.
    /// </summary>
    [Fact]
    public void DocDownTool_Validate_RenderingSelfTestCase_IsRecordedAndNotFailed()
    {
        // Arrange
        using var temp = new TempScratch();
        var trx = Path.Combine(temp.Path, "docdown-validate-windows.trx");

        // Act
        var (_, _) = CliHarness.Run("--validate", "--results", trx);

        // Assert: the distinctly named rendering case is present and did not fail
        var parsed = TrxSerializer.Deserialize(File.ReadAllText(trx));
        var render = Assert.Single(parsed.Results, r => r.Name == "pdf-rendering.renderRoundTrip");
        Assert.NotEqual(TestOutcome.Failed, render.Outcome);
    }

    /// <summary>
    ///     Proves <c>--list-backends</c> reports the PDF backend as available.
    /// </summary>
    [Fact]
    public void DocDownTool_ListBackends_DefaultEngine_ListsPdfAvailable()
    {
        // Act
        var (exit, log) = CliHarness.Run("--list-backends");
        var lines = log.Split('\n').Select(static line => line.TrimEnd('\r')).ToList();
        var pdfIndex = lines.FindIndex(static line => line == "  pdf - PDF (PdfPig)");
        var renderingIndex = lines.FindIndex(static line => line == "  pdf-rendering - PDF pages (PDFtoImage/PDFium)");

        // Assert
        Assert.Equal(0, exit);
        Assert.Contains("Registered backends:", log, StringComparison.Ordinal);
        Assert.DoesNotContain("capabilities:", log, StringComparison.Ordinal);
        Assert.True(pdfIndex >= 0);
        Assert.Equal("    formats: pdf", lines[pdfIndex + 1]);
        Assert.Equal("    status: available", lines[pdfIndex + 2]);
        Assert.True(renderingIndex >= 0);
        Assert.Equal("    formats: pdf", lines[renderingIndex + 1]);
        var renderingStatus = lines[renderingIndex + 2];
        Assert.True(
            renderingStatus == "    status: available"
            || renderingStatus.StartsWith("    status: unavailable (", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Proves <c>--version</c> prints an informational version string.
    /// </summary>
    [Fact]
    public void DocDownTool_Version_PrintsInformationalVersion()
    {
        // Act
        var (exit, log) = CliHarness.Run("--version");

        // Assert
        Assert.Equal(0, exit);
        Assert.Matches(@"\d+\.\d+\.\d+", log);
    }

    /// <summary>
    ///     Proves <c>--help</c> prints the usage and options.
    /// </summary>
    [Fact]
    public void DocDownTool_Help_PrintsUsageAndOptions()
    {
        // Act
        var (exit, log) = CliHarness.Run("--help");

        // Assert
        Assert.Equal(0, exit);
        Assert.Contains("Usage:", log, StringComparison.Ordinal);
        Assert.Contains("Options:", log, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves an unrecognized argument makes the tool exit non-zero with an error and no stack trace.
    /// </summary>
    [Fact]
    public void DocDownTool_BadArgument_WritesErrorAndExitsNonZero()
    {
        // Arrange: capture standard error while invoking the real Main
        var originalError = Console.Error;
        using var captured = new StringWriter();
        Console.SetError(captured);
        int exit;
        try
        {
            exit = global::DocDown.Tool.Program.Main(["--bogus-argument"]);
        }
        finally
        {
            Console.SetError(originalError);
        }

        // Assert: exit 1, an error message, and no stack trace
        Assert.Equal(1, exit);
        Assert.Contains("Error:", captured.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("   at ", captured.ToString(), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves the produced <c>.nupkg</c> is a RID-agnostic .NET tool named <c>docdown</c>.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Inspects the real package artifact rather than the project's source metadata. The claim
    ///         under test is <em>not</em> "the tool carries no native assets" — the tool carries the
    ///         rendering package's native binaries transitively by design. The claim is that the
    ///         package stays <em>runtime-identifier-agnostic</em>: every tool asset is placed under the
    ///         literal <c>any</c> platform folder (<c>tools/&lt;tfm&gt;/any/…</c>), so a single package
    ///         installs on every RID and resolves the correct
    ///         <c>runtimes/&lt;rid&gt;/native</c> asset at run time. A RID-specific tool would instead
    ///         place assets under <c>tools/&lt;tfm&gt;/&lt;rid&gt;/</c>.
    ///     </para>
    ///     <para>
    ///         Mirrors the artifact-level nupkg inspection in <c>DocDownPdfTests</c>: it packs the tool
    ///         project to a temporary folder and opens the produced archive. Slow but faithful.
    ///     </para>
    /// </remarks>
    [Fact]
    public void DocDownTool_Package_Nupkg_IsRidAgnosticDotNetTool()
    {
        // Arrange: pack the tool project to a temporary folder and locate the produced .nupkg
        using var temp = new TempScratch();
        var root = CliHarness.FindRepositoryRoot();
        var project = Path.Combine(root, "src", "DemaConsulting.DocDown.Tool", "DemaConsulting.DocDown.Tool.csproj");
        var nupkg = NuGetPackHelper.PackAndLocateNupkg(project, "DemaConsulting.DocDown.Tool", temp.Path);

        using var archive = ZipFile.OpenRead(nupkg);
        var toolEntries = archive.Entries
            .Select(entry => entry.FullName.Replace('\\', '/'))
            .Where(name => name.StartsWith("tools/", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Assert: there are tool assets to inspect at all
        Assert.NotEmpty(toolEntries);

        // Assert: every tools/<tfm>/<seg>/... entry places its RID segment at exactly "any" — the
        // RID-agnostic proof. A RID-specific tool would use a concrete runtime identifier here.
        foreach (var entry in toolEntries)
        {
            var segments = entry.Split('/');
            if (segments.Length < 3)
            {
                continue;
            }

            Assert.Equal("any", segments[2]);
        }

        // Assert: a DotnetToolSettings.xml declares the docdown command — packaged-as-tool proven at
        // the artifact level, subsuming the old source-metadata check.
        var settings = archive.Entries.FirstOrDefault(entry =>
            entry.FullName.Replace('\\', '/').EndsWith("/DotnetToolSettings.xml", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(settings);
        using var reader = new StreamReader(settings.Open());
        var xml = reader.ReadToEnd();
        Assert.Contains("Command Name=\"docdown\"", xml, StringComparison.Ordinal);
    }

    /// <summary>Writes fixture bytes to a file under the temporary folder and returns its path.</summary>
    private static string WriteFixture(TempScratch temp, string name, byte[] bytes)
    {
        var path = Path.Combine(temp.Path, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    /// <summary>Finds the line of tool output that names the produced <c>summary.txt</c>.</summary>
    private static string FindSummaryLine(string log)
    {
        var line = log
            .Split('\n')
            .Select(l => l.TrimEnd('\r'))
            .FirstOrDefault(l => l.EndsWith("summary.txt", StringComparison.Ordinal));
        Assert.NotNull(line);
        return line;
    }
}
