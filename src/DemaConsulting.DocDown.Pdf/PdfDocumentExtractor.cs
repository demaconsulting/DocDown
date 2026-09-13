using System.Globalization;
using DocDown.Core;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace DocDown.Pdf;

/// <summary>
///     The PDF backend: extracts text, embedded images, and document metadata from a PDF into the
///     DocDown output contract.
/// </summary>
/// <remarks>
///     <para>
///         This extractor is 100% managed and carries no native assets, which is both its strength
///         and the precise boundary of what it can promise. It reads the structure a PDF already
///         contains — glyphs, embedded image XObjects, and the document information dictionary — and
///         it cannot rasterize a page, because rasterization needs a renderer this package
///         deliberately does not ship. Extraction therefore focuses on the document content this
///         managed parser can honestly produce: markdown text, embedded-image files, and the PDF's
///         own document metadata.
///     </para>
///     <para>
///         Because nothing about this backend is environment-dependent — no native binary to locate,
///         no external application to launch — <see cref="ProbeAvailability"/> is unconditional and
///         performs no I/O at all. That is what makes the probe's obligations (well under 50 ms,
///         side-effect free, never opens the document, never throws) trivially satisfied rather than
///         carefully maintained.
///     </para>
///     <para>
///         Adverse documents are not translated into results here. Core catches any exception a
///         backend throws and converts it into an
///         <see cref="ExtractionOutcome.Unreadable"/> result carrying the parser's explanation, so
///         an encrypted or malformed PDF surfaces as extraction data rather than as an exception
///         reaching the caller. Instances hold no per-extraction state and are safe to register once
///         and reuse.
///     </para>
/// </remarks>
public sealed class PdfDocumentExtractor : IDocumentExtractor, ISelfValidating
{
    /// <summary>
    ///     Creates a PDF extractor ready to register with a <see cref="DocDownBuilder"/>.
    /// </summary>
    /// <remarks>
    ///     Construction is deliberately trivial: the extractor holds no per-extraction state, opens
    ///     no file, and probes no environment here, so a single instance can be registered once and
    ///     reused across concurrent extractions. Most callers never call this directly and instead
    ///     use <c>AddPdf</c>, which registers a factory with the builder.
    /// </remarks>
    public PdfDocumentExtractor()
    {
    }

    /// <inheritdoc />
    public string Id => "pdf";

    /// <inheritdoc />
    public string DisplayName => "PDF (PdfPig)";

    /// <inheritdoc />
    public IReadOnlyCollection<DocumentFormat> SupportedFormats => [DocumentFormat.Pdf];

    /// <inheritdoc />
    public int Priority => 0;

    /// <inheritdoc />
    /// <remarks>
    ///     Always available. There is nothing to probe: the parser is a managed assembly that ships
    ///     inside this package, so if this type could be constructed the extractor can run. Performs
    ///     no I/O and cannot throw.
    /// </remarks>
    public ExtractorAvailability ProbeAvailability() => ExtractorAvailability.Available();

    /// <inheritdoc />
    public async ValueTask<ExtractionOutcome> ExtractAsync(DocumentSource source, IExtractionContext context)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(context);

        var sink = context.Sink;
        var options = context.Options;
        var cancellationToken = context.CancellationToken;

        // Record what parsed this document, and state plainly that page rendering is not on offer
        // here, so the environment block explains the capability shortfall rather than omitting it
        sink.ReportEnvironmentFact(new EnvironmentFact("DocDown.Pdf", "pdf.parser", "PdfPig (managed)", Available: true));
        sink.ReportEnvironmentFact(new EnvironmentFact(
            "DocDown.Pdf", "pdf.pageRendering", "not provided by this extractor", Available: false));

        // Buffer the source: a stream source is not guaranteed seekable, and the parser needs to seek
        var bytes = await ReadSourceAsync(source, cancellationToken).ConfigureAwait(false);

        // Any parser fault from here on — encrypted, malformed, truncated — propagates to Core, which
        // converts it into a structured failure that still writes the full layout
        using var document = PdfDocument.Open(bytes, new ParsingOptions { UseLenientParsing = true });

        var pages = SelectPages(document, options.Pages);
        ReportDocumentInfo(sink, document, pages.Count);
        sink.ReportDocumentMetadata(BuildDocumentMetadata(document.Information));

        // Images first: the text renderer places the links the sink allocates for them
        var imageResult = await PdfImageExtractor
            .ExtractAsync(pages, sink, options, cancellationToken).ConfigureAwait(false);

        var textResult = PdfTextExtractor.Extract(
            pages, document.Information?.Title, imageResult.Images, cancellationToken);
        await sink.WriteContentAsync(textResult.Markdown, cancellationToken).ConfigureAwait(false);

        // Report the content inventory from the same facts used to render the output, so an empty
        // or text-free PDF is described by counts rather than by judgment-oriented machinery
        ReportContentFeatures(sink, options, pages, textResult, imageResult);
        return ExtractionOutcome.Produced;
    }

    /// <inheritdoc />
    /// <remarks>
    ///     The cases describe this backend's own behavior in the environment it is installed in: that
    ///     it can parse a document it builds itself, and that page rendering is genuinely unavailable
    ///     rather than merely untested. The rendering case reports as skipped with a reason, because
    ///     a capability this package does not claim must not be reported as a failure.
    /// </remarks>
    public IEnumerable<SelfTestCase> GetSelfTestCases() =>
    [
        new SelfTestCase("pdf.parseRoundTrip", Id, RunParseRoundTrip),
        new SelfTestCase("pdf.pageRendering", Id, static _ => SelfTestResult.Skipped(
            "This extractor does not provide the renderedPages capability; page rendering is not attempted."))
    ];

    /// <summary>
    ///     Reads the source document fully into memory.
    /// </summary>
    /// <param name="source">The source to read.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The document bytes.</returns>
    /// <remarks>
    ///     A stream-backed <see cref="DocumentSource"/> is not guaranteed seekable, and a PDF parser
    ///     must seek to the cross-reference table at the end of the file before it can read anything.
    ///     Buffering makes both source kinds behave identically rather than making stream sources a
    ///     special case that fails late. Performs read-only I/O.
    /// </remarks>
    private static async ValueTask<byte[]> ReadSourceAsync(DocumentSource source, CancellationToken cancellationToken)
    {
        using var stream = source.OpenRead();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        return buffer.ToArray();
    }

    /// <summary>
    ///     Selects the pages to extract, honoring a requested page range.
    /// </summary>
    /// <param name="document">The opened document.</param>
    /// <param name="range">The requested inclusive page range, or <see langword="null"/> for the whole document.</param>
    /// <returns>The selected pages in document order; empty when the document has no pages.</returns>
    /// <remarks>
    ///     Pages outside the document are silently absent from the selection rather than an error,
    ///     because an over-wide range is a reasonable caller request that should yield the pages that
    ///     do exist. Pure apart from reading the document.
    /// </remarks>
    private static IReadOnlyList<Page> SelectPages(PdfDocument document, PageRange? range)
    {
        var pages = new List<Page>(document.NumberOfPages);
        for (var number = 1; number <= document.NumberOfPages; number++)
        {
            if (range is { } selected && !selected.Contains(number))
            {
                continue;
            }

            pages.Add(document.GetPage(number));
        }

        return pages;
    }

    /// <summary>
    ///     Reports the document-level metadata this backend can read.
    /// </summary>
    /// <param name="sink">The sink to report through.</param>
    /// <param name="document">The opened document.</param>
    /// <param name="selectedPages">The number of pages actually selected for extraction.</param>
    /// <remarks>
    ///     Reports the document's own page count rather than the selected count, because the metadata
    ///     describes the document and not this run's slice of it; the selected count is reported as
    ///     the part count so a page-range run is still self-describing. Blank metadata values are
    ///     reported as absent rather than as empty strings, because unknown and empty are different
    ///     facts. Side effect: records on the sink.
    /// </remarks>
    private static void ReportDocumentInfo(IExtractionSink sink, PdfDocument document, int selectedPages)
    {
        var information = document.Information;
        sink.ReportDocumentInfo(new DocumentInfo(
            Title: NullIfBlank(information?.Title),
            Author: NullIfBlank(information?.Author),
            PageCount: document.NumberOfPages,
            PartCount: selectedPages));
    }

    /// <summary>
    ///     Reports the content inventory this backend can derive exactly from the extraction walk.
    /// </summary>
    /// <param name="sink">The sink to report through.</param>
    /// <param name="options">The effective options, consulted for whether embedded images were requested.</param>
    /// <param name="pages">The selected pages.</param>
    /// <param name="text">The text-extraction outcome.</param>
    /// <param name="images">The image-extraction outcome.</param>
    /// <remarks>
    ///     Counts are reported from the real PDF structures the extractor walked rather than from a
    ///     regex over the rendered markdown. Zero counts are kept only where the backend genuinely
    ///     looked and found none, which is how an empty or scanned PDF is conveyed without treating
    ///     ordinary document absences as failures. When embedded images were found but none were
    ///     written, the extraction notes explain why and the image inventory stays silent rather than
    ///     falsely claiming the document had no images. Side effect: records on the sink.
    /// </remarks>
    private static void ReportContentFeatures(
        IExtractionSink sink, ExtractionOptions options, IReadOnlyList<Page> pages,
        PdfTextResult text, PdfImageResult images)
    {
        sink.ReportContentFeature(new ContentFeature("pages", pages.Count, "page", LookedFor: true));
        sink.ReportContentFeature(new ContentFeature("headings", text.HeadingCount, LookedFor: true));
        sink.ReportContentFeature(new ContentFeature("paragraphs", text.ParagraphCount, "paragraph", LookedFor: true));

        if (options.IncludeEmbeddedImages && (images.Written > 0 || images.Found == 0))
        {
            sink.ReportContentFeature(new ContentFeature(
                "inline images", images.Written, "inline image", LookedFor: images.Found == 0));
        }
    }

    /// <summary>
    ///     Runs the parse round-trip self-test case.
    /// </summary>
    /// <param name="context">The self-test context supplying cancellation.</param>
    /// <returns>The result of the case.</returns>
    /// <remarks>
    ///     Builds a one-page PDF in memory and reads its glyphs back, which proves the parser is
    ///     genuinely functional in this deployment rather than merely present. Uses no filesystem
    ///     location of its own because the whole round trip fits in memory.
    /// </remarks>
    private static SelfTestResult RunParseRoundTrip(SelfTestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var started = DateTimeOffset.UtcNow;

        try
        {
            var bytes = BuildProbeDocument();
            using var document = PdfDocument.Open(bytes, new ParsingOptions { UseLenientParsing = true });
            var page = document.GetPage(1);
            return page.Letters.Count > 0
                ? SelfTestResult.Passed(DateTimeOffset.UtcNow - started)
                : SelfTestResult.Failed(
                    "The PDF parser read a document it built itself but found no glyphs on its only page.",
                    DateTimeOffset.UtcNow - started);
        }
#pragma warning disable CA1031 // A self-test reports every fault as data rather than throwing at its caller
        catch (Exception exception)
#pragma warning restore CA1031
        {
            return SelfTestResult.Failed(
                $"The PDF parser could not complete a round trip in this environment: {exception.Message}",
                DateTimeOffset.UtcNow - started);
        }
    }

    /// <summary>
    ///     Builds the one-page document the parse round-trip case reads back.
    /// </summary>
    /// <returns>The bytes of a minimal single-page PDF carrying a short line of text.</returns>
    /// <remarks>
    ///     Built rather than embedded so the case ships no binary payload and exercises the writer and
    ///     the reader together. Pure apart from the allocation.
    /// </remarks>
    private static byte[] BuildProbeDocument()
    {
        using var builder = new UglyToad.PdfPig.Writer.PdfDocumentBuilder();
        var font = builder.AddStandard14Font(UglyToad.PdfPig.Fonts.Standard14Fonts.Standard14Font.Helvetica);
        var page = builder.AddPage(PageSize.A4);
        page.AddText("DocDown self test", 12, new UglyToad.PdfPig.Core.PdfPoint(30, 600), font);
        return builder.Build();
    }

    /// <summary>
    ///     Normalizes a blank metadata value to <see langword="null"/>.
    /// </summary>
    /// <param name="value">The metadata value read from the document, which may be blank.</param>
    /// <returns>The trimmed value, or <see langword="null"/> when it carries no information.</returns>
    /// <remarks>
    ///     A PDF routinely stores an empty title rather than omitting the key. Reporting that as an
    ///     empty string would claim the document declares a blank title, when the honest statement is
    ///     that it declares nothing. Pure.
    /// </remarks>
    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    ///     The interesting document-information fields recorded as absent when blank, in report order.
    /// </summary>
    /// <remarks>
    ///     A PDF commonly leaves these blank; naming them in <c>metadata.json</c> turns silence into
    ///     an explicit "the document supplied no value" rather than an omission a reader cannot
    ///     distinguish from a field the format does not carry.
    /// </remarks>
    private static readonly string[] InterestingPdfFields = ["title", "author", "subject", "keywords"];

    /// <summary>
    ///     Maps a PDF's document-information dictionary to a <see cref="DocumentMetadata"/> for
    ///     <c>metadata.json</c>, with provenance <see cref="MetadataProvenance.PdfDocumentInformation"/>.
    /// </summary>
    /// <param name="information">The document information, or <see langword="null"/> when the PDF carries none.</param>
    /// <returns>
    ///     The self-reported metadata: only populated fields (verbatim, dates normalized to ISO-8601
    ///     UTC) and the interesting fields that were blank. A date that cannot be parsed is omitted,
    ///     never guessed.
    /// </returns>
    /// <remarks>
    ///     PDF has its own mapper because its property bag and date encoding differ from the OPC core
    ///     properties: dates arrive as PDF <c>D:</c> date strings that must be parsed carefully,
    ///     and a value that cannot be parsed is dropped rather than surfaced as a wrong or blank date.
    ///     Pure and side-effect free. Exposed as <see langword="internal"/> so the mapping is unit-tested directly.
    /// </remarks>
    internal static DocumentMetadata BuildDocumentMetadata(DocumentInformation? information)
    {
        var fields = new List<DocumentMetadataField>();

        AddText(fields, "title", information?.Title);
        AddText(fields, "author", information?.Author);
        AddText(fields, "subject", information?.Subject);
        AddText(fields, "keywords", information?.Keywords);
        AddText(fields, "creator", information?.Creator);
        AddText(fields, "producer", information?.Producer);
        AddDate(fields, "created", information?.CreationDate);
        AddDate(fields, "modified", information?.ModifiedDate);

        // An interesting field is absent only when it produced no populated field above
        var present = new HashSet<string>(fields.Select(field => field.Name), StringComparer.Ordinal);
        var absent = InterestingPdfFields.Where(name => !present.Contains(name)).ToList();
        return new DocumentMetadata(fields, absent);
    }

    /// <summary>
    ///     Adds a text field when its value carries information, dropping blanks.
    /// </summary>
    /// <param name="fields">The field list to append to.</param>
    /// <param name="name">The field's stable camelCase name.</param>
    /// <param name="value">The raw property value, which may be null or blank.</param>
    /// <remarks>A blank value is an absence, not a field, so it is never emitted. Side effect: appends.</remarks>
    private static void AddText(List<DocumentMetadataField> fields, string name, string? value)
    {
        if (NullIfBlank(value) is { } trimmed)
        {
            fields.Add(new DocumentMetadataField(name, trimmed, MetadataProvenance.PdfDocumentInformation));
        }
    }

    /// <summary>
    ///     Adds a date field, normalized to ISO-8601 UTC, when the raw value parses as a PDF date.
    /// </summary>
    /// <param name="fields">The field list to append to.</param>
    /// <param name="name">The field's stable camelCase name.</param>
    /// <param name="raw">The raw PDF date string (for example <c>D:20260707120000Z</c>).</param>
    /// <remarks>
    ///     A value that cannot be parsed is omitted, never guessed, honoring the never-invent rule.
    ///     Side effect: appends only on a successful parse.
    /// </remarks>
    private static void AddDate(List<DocumentMetadataField> fields, string name, string? raw)
    {
        if (TryParsePdfDate(raw, out var iso))
        {
            fields.Add(new DocumentMetadataField(name, iso, MetadataProvenance.PdfDocumentInformation));
        }
    }

    /// <summary>
    ///     Parses a PDF <c>D:</c> date string (year, month, day, hour, minute, second, then an
    ///     optional UTC offset) to an ISO-8601 UTC string.
    /// </summary>
    /// <param name="raw">The raw date string, with or without the <c>D:</c> prefix.</param>
    /// <param name="iso">The parsed <c>yyyy-MM-ddTHH:mm:ssZ</c> string when the parse succeeds.</param>
    /// <returns><see langword="true"/> when the value parses to a real instant; otherwise <see langword="false"/>.</returns>
    /// <remarks>
    ///     The PDF date grammar makes every field after the year optional and each defaults (month and
    ///     day to 1, the rest to 0), with an optional trailing <c>Z</c>, <c>+</c>, or <c>-</c> UTC
    ///     offset. A field region that is present but non-numeric, a dangling half field, an offset
    ///     out of range, or an impossible date all fail the parse so the caller omits the value rather
    ///     than guessing one. Pure. Exposed as <see langword="internal"/> so the parse is unit-tested directly.
    /// </remarks>
    internal static bool TryParsePdfDate(string? raw, out string iso)
    {
        iso = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var text = raw.Trim();
        if (text.StartsWith("D:", StringComparison.Ordinal))
        {
            text = text[2..];
        }

        // A year is mandatory; every later field is optional and defaults
        if (text.Length < 4 || !int.TryParse(text.AsSpan(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out var year))
        {
            return false;
        }

        if (!TryReadPart(text, 4, 1, out var month) || !TryReadPart(text, 6, 1, out var day)
            || !TryReadPart(text, 8, 0, out var hour) || !TryReadPart(text, 10, 0, out var minute)
            || !TryReadPart(text, 12, 0, out var second) || !TryReadOffset(text, out var offset))
        {
            return false;
        }

        try
        {
            var instant = new DateTimeOffset(year, month, day, hour, minute, second, offset);
            iso = instant.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            // An impossible date (month 13, day 32, offset beyond +/-14h) is a malformed value, not a guessable one
            return false;
        }
    }

    /// <summary>
    ///     Reads a two-digit PDF date field at a fixed offset, defaulting when the field is absent.
    /// </summary>
    /// <param name="text">The date text (already stripped of the <c>D:</c> prefix).</param>
    /// <param name="start">The 0-based start of the two-digit field.</param>
    /// <param name="fallback">The value to use when the field is absent (the grammar's default).</param>
    /// <param name="value">The parsed or defaulted value.</param>
    /// <returns><see langword="true"/> when the field is absent or a valid pair of digits; <see langword="false"/> when malformed.</returns>
    /// <remarks>An absent field defaults per the grammar; a present but non-numeric or half field is malformed and fails. Pure.</remarks>
    private static bool TryReadPart(string text, int start, int fallback, out int value)
    {
        value = fallback;
        if (start >= text.Length)
        {
            return true;
        }

        if (start + 2 > text.Length)
        {
            return false;
        }

        return int.TryParse(text.AsSpan(start, 2), NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>
    ///     Reads the optional trailing UTC offset of a PDF date.
    /// </summary>
    /// <param name="text">The date text (already stripped of the <c>D:</c> prefix).</param>
    /// <param name="offset">The parsed offset, or <see cref="TimeSpan.Zero"/> when none is present.</param>
    /// <returns><see langword="true"/> when the offset is absent, <c>Z</c>, or a well-formed signed offset; otherwise <see langword="false"/>.</returns>
    /// <remarks>
    ///     Accepts <c>Z</c> (UTC), or a <c>+</c>/<c>-</c> sign followed by two-digit hours and optional
    ///     two-digit minutes with the apostrophe separators the grammar allows. An unexpected leading
    ///     character fails the parse. Pure.
    /// </remarks>
    private static bool TryReadOffset(string text, out TimeSpan offset)
    {
        offset = TimeSpan.Zero;
        if (text.Length <= 14)
        {
            return true;
        }

        var sign = text[14];
        if (sign is 'Z' or 'z')
        {
            return true;
        }

        if (sign is not ('+' or '-'))
        {
            return false;
        }

        // Drop the apostrophe separators so hours and optional minutes read as plain digit pairs
        var rest = text[15..].Replace("'", string.Empty, StringComparison.Ordinal);
        var hours = 0;
        var minutes = 0;
        if (rest.Length >= 2 && !int.TryParse(rest.AsSpan(0, 2), NumberStyles.None, CultureInfo.InvariantCulture, out hours))
        {
            return false;
        }

        if (rest.Length >= 4 && !int.TryParse(rest.AsSpan(2, 2), NumberStyles.None, CultureInfo.InvariantCulture, out minutes))
        {
            return false;
        }

        var magnitude = new TimeSpan(hours, minutes, 0);
        offset = sign == '-' ? -magnitude : magnitude;
        return true;
    }
}
