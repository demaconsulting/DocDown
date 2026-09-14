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
public class DocDownToolTests : IClassFixture<ValidationRuns>
{
    /// <summary>The system-level validation runs shared by the scenarios in this class.</summary>
    private readonly ValidationRuns _runs;

    /// <summary>
    ///     Initializes the test class with its shared validation runs.
    /// </summary>
    /// <param name="runs">The shared runs, injected by xUnit.</param>
    /// <remarks>
    ///     A class fixture is the narrowest scope that works: xUnit already runs one class's tests
    ///     sequentially, so the two runs never overlap, and no other class is constrained. These are
    ///     the suite's only command-line validation runs, and the only place Microsoft Office is
    ///     driven — every unit-level assertion about <c>--validate</c> uses a managed engine instead.
    /// </remarks>
    public DocDownToolTests(ValidationRuns runs) => _runs = runs;

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
        Assert.True(exit == 0, log);
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
        Assert.True(exit == 0, log);
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
        Assert.Equal("3.0", root.GetProperty("schemaVersion").GetString());
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
        // Arrange / Act: the class's shared self-validation that wrote a TRX results file
        var (exit, log) = (_runs.Trx.ExitCode, _runs.Trx.Log);
        var trx = _runs.Trx.ResultsPath;

        // Assert: the file exists, re-parses, and carries the engine self-test cases
        Assert.True(exit == 0, log);
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
        // Arrange / Act: the class's shared self-validation that wrote a JUnit results file
        var (exit, log) = (_runs.JUnit.ExitCode, _runs.JUnit.Log);
        var xml = _runs.JUnit.ResultsPath;

        // Assert
        Assert.True(exit == 0, log);
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
        // Arrange / Act: the class's shared self-validation that wrote a TRX results file
        var trx = _runs.Trx.ResultsPath;

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
        // Arrange / Act: the class's shared self-validation that wrote a TRX results file
        var trx = _runs.Trx.ResultsPath;

        // Assert: the distinctly named rendering case is present and did not fail
        var parsed = TrxSerializer.Deserialize(File.ReadAllText(trx));
        var render = Assert.Single(parsed.Results, r => r.Name == "pdf-rendering.renderRoundTrip");
        Assert.NotEqual(TestOutcome.Failed, render.Outcome);
    }

    /// <summary>
    ///     Proves the Visio and PowerPoint COM render cases actually execute and pass where the
    ///     application is installed.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         These two cases are the only place the COM boundary — activation, read-only open,
    ///         export, and session teardown — is exercised anywhere, so a machine that has Office
    ///         must prove it works rather than accept a skip. A skip here would mean the suite had
    ///         quietly stopped testing the thing these cases exist for.
    ///     </para>
    ///     <para>
    ///         Where the application is absent the case is legitimately not executed, which is the
    ///         honest answer for that environment and is the only reason this test tolerates
    ///         anything other than a pass. A failure is never tolerated on any machine.
    ///     </para>
    /// </remarks>
    [Fact]
    public void DocDownTool_Validate_ComRenderCases_ExecuteAndPassWhereOfficeIsInstalled()
    {
        // Arrange / Act: the shared command-line self-validation over the shipped engine
        var trx = _runs.Trx.ResultsPath;
        var parsed = TrxSerializer.Deserialize(File.ReadAllText(trx));

        // The engine's own availability report decides what to expect, so the expectation is
        // derived from this environment rather than assumed from the platform
        var backends = new DocDownBuilder().AddVisio().AddPowerPoint().Build().GetBackends();
        bool Available(string id) =>
            backends.Any(b => b.Descriptor.Id == id && b.Availability.IsAvailable);

        // Assert: each COM render case ran, and passed wherever its application is present
        AssertComRenderCase(parsed, "visio.com.render", Available("visio-com"));
        AssertComRenderCase(parsed, "powerpoint.com.render", Available("powerpoint-com"));
    }

    /// <summary>
    ///     Asserts one COM render case is present, never failed, and passed where its application is installed.
    /// </summary>
    /// <param name="results">The parsed results of the shared validation run.</param>
    /// <param name="caseName">The self-test case name to inspect.</param>
    /// <param name="applicationAvailable">Whether the backing Office application is available here.</param>
    /// <remarks>
    ///     The availability flag comes from the engine's own backend report, which is the same answer
    ///     selection acts on, so the expectation is derived from the environment rather than assumed.
    /// </remarks>
    private static void AssertComRenderCase(
        DemaConsulting.TestResults.TestResults results, string caseName, bool applicationAvailable)
    {
        var renderCase = Assert.Single(results.Results, r => r.Name == caseName);
        Assert.NotEqual(TestOutcome.Failed, renderCase.Outcome);

        if (applicationAvailable)
        {
            Assert.True(
                renderCase.Outcome == TestOutcome.Passed,
                $"'{caseName}' must execute and pass where its application is installed, but was "
                + $"{renderCase.Outcome}: {renderCase.ErrorMessage}");
        }
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
        Assert.True(exit == 0, log);
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
        Assert.True(exit == 0, log);
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
        Assert.True(exit == 0, log);
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

    /// <summary>
    ///     Proves the produced <c>.nupkg</c> ships no native debug symbols and carries natives for
    ///     exactly the supported runtime identifiers.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Both controls are size controls with no functional signal, so nothing else in the
    ///         suite would notice their loss. They were added after the packaged tool was measured at
    ///         592 MB, of which roughly 460 MB was third-party native <c>.pdb</c> files duplicated
    ///         across three target frameworks, and most of the remainder was natives for platforms
    ///         DocDown does not support. A dependency update that reintroduces either would silently
    ///         restore that bulk; this test makes it fail the build instead.
    ///     </para>
    ///     <para>
    ///         The runtime-identifier check is an allow list, mirroring the project file: it asserts
    ///         the set of shipped runtime identifiers is exactly the supported set, so a newly
    ///         published platform cannot slip in unnoticed the way the original bulk did. Note that
    ///         <c>osx</c> is not a fourth platform — SkiaSharp publishes macOS as one fat dylib that
    ///         runtime-identifier fallback resolves for an Apple Silicon host.
    ///     </para>
    ///     <para>
    ///         Inspects the real package artifact rather than project metadata, for the same reason
    ///         as <see cref="DocDownTool_Package_Nupkg_IsRidAgnosticDotNetTool" />: the claim is about
    ///         what ships, not about what the project file says.
    ///     </para>
    /// </remarks>
    [Fact]
    public void DocDownTool_Package_Nupkg_ExcludesNativeSymbolsAndUnsupportedRuntimes()
    {
        // Arrange: pack the tool project to a temporary folder and locate the produced .nupkg
        using var temp = new TempScratch();
        var root = CliHarness.FindRepositoryRoot();
        var project = Path.Combine(root, "src", "DemaConsulting.DocDown.Tool", "DemaConsulting.DocDown.Tool.csproj");
        var nupkg = NuGetPackHelper.PackAndLocateNupkg(project, "DemaConsulting.DocDown.Tool", temp.Path);

        using var archive = ZipFile.OpenRead(nupkg);
        var natives = archive.Entries
            .Select(entry => entry.FullName.Replace('\\', '/'))
            .Where(name => name.Contains("/runtimes/", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Assert: the native stack is present at all, so the assertions below are meaningful rather
        // than vacuously true against an empty set.
        Assert.NotEmpty(natives);

        // Assert: no native debug symbols ride along with the native binaries.
        Assert.DoesNotContain(natives, name => name.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase));

        // Assert: the shipped runtime identifiers are exactly the supported set. Kept in step with
        // SupportedToolRuntimeIdentifiers in DemaConsulting.DocDown.Tool.csproj. Asserting the whole
        // set — rather than probing for known-bad names — is what stops a newly published platform
        // from being admitted silently.
        var shipped = natives
            .Select(name => name.Split("/runtimes/", StringSplitOptions.None)[1].Split('/')[0])
            .Distinct()
            .OrderBy(rid => rid, StringComparer.Ordinal)
            .ToList();

        string[] supported = ["linux-x64", "osx", "osx-arm64", "win-x64"];
        Assert.Equal(supported, shipped);
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
