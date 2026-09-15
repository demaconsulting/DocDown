using DocDown.Core;
using DocDown.Excel.Markdown;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using CoreFormat = DocDown.Core.DocumentFormat;

namespace DocDown.Excel.OpenXml;

/// <summary>
///     The Excel Open XML backend: extracts every worksheet's cell values, formulas, and addressing
///     from a <c>.xlsx</c> into the DocDown output contract, and deliberately never renders.
/// </summary>
/// <remarks>
///     <para>
///         A workbook, in this product's domain, is a prose and data container: its cells hold
///         requirement quotations, pasted transcripts, exact numbers, and the formulas that produced
///         them, and its worksheets may also carry embedded pictures. This backend preserves cell
///         values verbatim at full length, keeps formulas alongside their computed values, cites
///         every fact by sheet and cell address, extracts the workbook's embedded images through
///         the sink, and records inventory counts and plain-language notes for any attempted chart
///         or image step it could not complete. A workbook has no page grid to rasterize, so page
///         rendering is not applicable rather than merely unavailable, and a page request against
///         it is honored with silence.
///     </para>
///     <para>
///         Because nothing about this backend is environment-dependent — no native binary, no
///         external application — <see cref="ProbeAvailability"/> is unconditional, performs no
///         I/O, and reports an available extractor that does not render pages. Adverse workbooks are
///         not translated into results here: Core catches any exception and converts it into a
///         structured unreadable result with the full layout still written where possible.
///         Instances hold no per-extraction state and are safe to register once and reuse.
///     </para>
/// </remarks>
public sealed class ExcelOpenXmlExtractor : IDocumentExtractor, ISelfValidating
{
    /// <summary>The embedded document the parse round-trip self-test reads.</summary>
    /// <remarks>A real file authored in Microsoft Excel, carrying a named sheet of labelled cells and numbers.</remarks>
    private const string ProbeResourceName = "DemaConsulting.DocDown.Office.Resources.probe.xlsx";

    /// <summary>
    ///     Creates an Excel Open XML extractor ready to register with a <see cref="DocDownBuilder"/>.
    /// </summary>
    /// <remarks>
    ///     Construction is deliberately trivial: the extractor holds no per-extraction state, opens
    ///     no file, and probes no environment here, so a single instance can be registered once and
    ///     reused across concurrent extractions. Most callers never call this directly and instead
    ///     use <c>AddExcel</c>, which registers a factory with the builder.
    /// </remarks>
    public ExcelOpenXmlExtractor()
    {
    }

    /// <inheritdoc />
    public string Id => "excel-openxml";

    /// <inheritdoc />
    public string DisplayName => "Excel (Open XML SDK)";

    /// <inheritdoc />
    public IReadOnlyCollection<CoreFormat> SupportedFormats => [CoreFormat.Xlsx];

    /// <inheritdoc />
    public int Priority => 10;

    /// <inheritdoc />
    /// <remarks>
    ///     A workbook is non-paginated: it has no page grid to rasterize, so a page-rendering request
    ///     applies to nothing. Reporting this here lets the engine honor a <c>--pages</c> request
    ///     against a spreadsheet with silence rather than a false shortfall.
    /// </remarks>
    public bool PageRenderingApplicable => false;

    /// <inheritdoc />
    /// <remarks>
    ///     Always available, and never reports rendered pages. There is nothing to probe: the Open
    ///     XML SDK is a managed assembly that ships inside this package, and a spreadsheet has no
    ///     page grid to render in the first place. Performs no I/O and cannot throw.
    /// </remarks>
    public ExtractorAvailability ProbeAvailability() => ExtractorAvailability.Available();

    /// <inheritdoc />
    public async ValueTask<ExtractionOutcome> ExtractAsync(DocumentSource source, IExtractionContext context)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(context);

        var sink = context.Sink;
        var cancellationToken = context.CancellationToken;

        // Record what parsed this document, and state plainly that a workbook is non-paginated
        sink.ReportEnvironmentFact(new EnvironmentFact(
            "DocDown.Excel", "excel.backend", "Open XML SDK (managed)", Available: true));
        sink.ReportEnvironmentFact(new EnvironmentFact(
            "DocDown.Excel", "excel.pageRendering", "not applicable to a non-paginated workbook", Available: false));

        // Buffer the source: the package reader must seek, and a stream source is not guaranteed seekable
        var bytes = await ReadSourceAsync(source, cancellationToken).ConfigureAwait(false);

        // Any fault from here — encrypted, malformed, truncated — propagates to Core, which converts
        // it into a structured failure that still writes the layout
        using var stream = new MemoryStream(bytes, writable: false);
        var model = ExcelOpenXmlReader.Read(stream);

        await ExcelContentEmitter.EmitAsync(sink, context.Options, model, cancellationToken)
            .ConfigureAwait(false);
        return ExtractionOutcome.Produced;
    }

    /// <inheritdoc />
    /// <remarks>
    ///     The cases describe this backend's own behavior in its deployed environment: that it can
    ///     read a workbook it builds itself, and that page rendering is genuinely not offered rather
    ///     than merely untested. The rendering case reports as skipped with a reason, because a
    ///     capability this package does not claim must not be reported as a failure.
    /// </remarks>
    public IEnumerable<SelfTestCase> GetSelfTestCases() =>
    [
        new SelfTestCase("excel.openxml.parseRoundTrip", Id, RunParseRoundTrip),
        new SelfTestCase("excel.pageRendering", Id, static _ => SelfTestResult.Skipped(
            "This extractor does not render pages; a workbook is non-paginated, so page rendering does not apply."))
    ];

    /// <summary>
    ///     Reads the source document fully into memory.
    /// </summary>
    /// <param name="source">The source to read.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The document bytes.</returns>
    /// <remarks>Buffering gives the reader a seekable stream to open the package. Read-only I/O.</remarks>
    private static async ValueTask<byte[]> ReadSourceAsync(DocumentSource source, CancellationToken cancellationToken)
    {
        using var stream = source.OpenRead();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        return buffer.ToArray();
    }

    /// <summary>
    ///     Runs the parse round-trip self-test case.
    /// </summary>
    /// <param name="context">The self-test context supplying cancellation.</param>
    /// <returns>The result of the case.</returns>
    /// <remarks>
    ///     Reads the embedded workbook authored in Microsoft Excel, which proves the SDK is genuinely
    ///     functional in this deployment rather than merely present. Uses no filesystem location of
    ///     its own because the whole round trip fits in memory.
    /// </remarks>
    private static SelfTestResult RunParseRoundTrip(SelfTestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var started = DateTimeOffset.UtcNow;

        try
        {
            using var stream = new MemoryStream(
                SelfTestProbe.Load(typeof(ExcelOpenXmlExtractor).Assembly, ProbeResourceName), writable: false);
            var model = ExcelOpenXmlReader.Read(stream);
            return model.Sheets.Count > 0
                ? SelfTestResult.Passed(DateTimeOffset.UtcNow - started)
                : SelfTestResult.Failed(
                    "The Open XML reader read the embedded Excel workbook but found no worksheets.",
                    DateTimeOffset.UtcNow - started);
        }
#pragma warning disable CA1031 // A self-test reports every fault as data rather than throwing at its caller
        catch (Exception exception)
#pragma warning restore CA1031
        {
            return SelfTestResult.Failed(
                $"The Open XML reader could not complete a round trip in this environment: {exception.Message}",
                DateTimeOffset.UtcNow - started);
        }
    }

}
