using System.Globalization;
using System.Text;
using DocDown.Core;
using DocDown.Excel.OpenXml;

namespace DocDown.Excel.Markdown;

/// <summary>
///     Emits a read <see cref="ExcelWorkbookModel"/> through the extraction sink: one content part
///     per worksheet, each cell's address, value, and formula preserved, plus the honest gaps the
///     model and options imply.
/// </summary>
/// <remarks>
///     Every worksheet becomes a <see cref="ContentPartKind.Sheet"/> part so a workbook stays
///     navigable and a cited fact keeps its sheet identity. Nothing is truncated and nothing is
///     omitted silently: an empty sheet is noted informationally, an empty workbook is a counted
///     gap, and the workbook's embedded images are extracted through the sink and counted. A
///     spreadsheet has no page grid, so a page-rendering request applies to nothing and the engine
///     honors it with silence rather than a false shortfall. Performs no filesystem I/O of its own;
///     every byte goes through the sink. Stateless and thread-safe.
/// </remarks>
internal static class ExcelContentEmitter
{
    /// <summary>The ledger path the empty-workbook gap names.</summary>
    private const string ContentTarget = "content.md";

    /// <summary>The ledger path every image gap names.</summary>
    private const string ImagesTarget = "images/";

    /// <summary>The minimum number of populated rows a sheet's bounding rectangle needs to warrant a grid table.</summary>
    /// <remarks>Fewer than three rows is a strip or a pair of headings, better read as the list; measured real grids all exceed this.</remarks>
    private const int MinTableRowSpan = 3;

    /// <summary>The minimum number of populated columns a sheet's bounding rectangle needs to warrant a grid table.</summary>
    /// <remarks>A single column is a one-dimensional list, for which a table adds nothing over the address listing.</remarks>
    private const int MinTableColumnSpan = 2;

    /// <summary>The maximum number of populated columns beyond which a markdown grid table is unreadable.</summary>
    /// <remarks>
    ///     The widest real sheet measured across the sample workbooks was 21 columns; a 30-column
    ///     guard therefore only ever fires on a pathologically wide sheet, which keeps only the
    ///     address listing rather than emitting an unreadable table.
    /// </remarks>
    private const int MaxTableColumnSpan = 30;

    /// <summary>The minimum fill density (populated cells over bounding-rectangle area) a sheet needs to warrant a grid table.</summary>
    /// <remarks>
    ///     Measured grid-shaped sheets clustered at 0.55 and above while the one genuinely sparse
    ///     sheet sat at 0.42, so a 0.50 threshold separates a real grid from a scatter of cells that
    ///     a table would pad mostly with blanks.
    /// </remarks>
    private const double MinTableDensity = 0.50;

    /// <summary>The maximum characters a value may occupy in a table cell before it is elided there.</summary>
    /// <remarks>
    ///     Real data cells measured at roughly 50 characters or fewer, while prose and note cells ran
    ///     to 240, 345, and 1087 characters; an 80-character cut cleanly isolates prose so the table
    ///     stays readable. The elision is table-only: the full verbatim value is always present in the
    ///     address listing below, so the verbatim-full-length guarantee is never weakened.
    /// </remarks>
    private const int MaxTableCellLength = 80;

    /// <summary>
    ///     Emits a workbook model through the sink and returns whether the run degraded.
    /// </summary>
    /// <param name="sink">The sink every artifact is written through. Must not be null.</param>
    /// <param name="options">The effective extraction options. Must not be null.</param>
    /// <param name="model">The workbook model to emit. Must not be null.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns><see langword="true"/> when any gap was reported; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any argument is <see langword="null"/>.</exception>
    /// <remarks>Side effect: writes parts and records reports on the sink.</remarks>
    public static async ValueTask<bool> EmitAsync(
        IExtractionSink sink, ExtractionOptions options, ExcelWorkbookModel model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(model);

        var degraded = false;

        sink.ReportFound(GapKind.Parts, model.Sheets.Count);

        // An empty workbook has nothing to write; say so as a counted gap rather than an empty file
        if (model.Sheets.Count == 0)
        {
            sink.ReportDiagnostic(new ExtractionDiagnostic(
                ExcelDiagnosticCodes.NoWorksheets, DiagnosticSeverity.Warning,
                "The workbook contains no worksheets, so no content could be produced."));
            sink.ReportGap(new ExtractionGap(
                string.Empty, GapKind.Parts, ContentTarget, GapScope.Failed,
                "The workbook contains no worksheets.",
                Impact: "No sheet content is present in the extracted output."));
            degraded = true;
        }

        // Write the workbook's embedded image files first so each worksheet can link its pictures
        // inline at their point of occurrence — Word's convention. Gap reporting is deferred below so
        // the diagnostic order is unchanged; only the file writes move up.
        var imageResult = options.IncludeEmbeddedImages
            ? await EmbeddedImageWriter.WriteAsync(sink, options, model.Images, cancellationToken).ConfigureAwait(false)
            : null;
        var imagePaths = imageResult?.PathsBySourceRef ?? EmptyImagePaths;

        var ordinal = 1;
        foreach (var sheet in model.Sheets)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var markdown = RenderSheet(sheet, imagePaths);
            await sink.AddContentPartAsync(
                new ContentPart(ContentPartKind.Sheet, ordinal, sheet.Name), markdown, cancellationToken)
                .ConfigureAwait(false);
            ordinal++;

            if (sheet.Cells.Count == 0)
            {
                sink.ReportDiagnostic(new ExtractionDiagnostic(
                    ExcelDiagnosticCodes.EmptySheet, DiagnosticSeverity.Info,
                    $"Worksheet '{sheet.Name}' carried no non-empty cells."));
            }
        }

        sink.ReportDocumentInfo(new DocumentInfo(Title: null, Author: null, PageCount: null, PartCount: model.Sheets.Count));

        // Report the workbook's self-reported metadata for metadata.json when the reader captured it
        if (model.Metadata is { } metadata)
        {
            sink.ReportDocumentMetadata(metadata);
        }

        // Report the honest gaps and caveats for the images written above
        degraded |= ReportImages(sink, options, imageResult);

        ReportContentFeatures(sink, model);

        // A page-rendering request is deliberately not answered with a gap: a workbook has no page
        // grid, so rendering applies to nothing. The engine records that non-applicability itself, so
        // this backend stays silent rather than reporting a shortfall for a request it cannot lose.
        return degraded;
    }

    /// <summary>
    ///     Reports an outline of what the rendered content contains, counted from the model.
    /// </summary>
    /// <param name="sink">The sink to report through.</param>
    /// <param name="model">The workbook model whose structure is counted.</param>
    /// <remarks>
    ///     A character count says nothing about whether a workbook is one small table or tens of
    ///     thousands of cells across a dozen sheets, which is exactly what a reader deciding whether
    ///     to open the parts needs to know. The counts come from the model the reader built, and Core
    ///     drops any zero count. Side effect: records reports on the sink.
    /// </remarks>
    private static void ReportContentFeatures(IExtractionSink sink, ExcelWorkbookModel model)
    {
        sink.ReportContentFeature(new ContentFeature("worksheets", model.Sheets.Count));
        sink.ReportContentFeature(new ContentFeature(
            "populated cells", model.Sheets.Sum(sheet => sheet.Cells.Count)));
        sink.ReportContentFeature(new ContentFeature(
            "cells carrying a formula",
            model.Sheets.Sum(sheet => sheet.Cells.Count(cell => cell.Formula is not null)),
            "cell carrying a formula"));
        sink.ReportContentFeature(new ContentFeature(
            "inline images", model.Sheets.Sum(sheet => sheet.Images.Count)));
    }

    /// <summary>The empty path map used when images are suppressed, so content rendering emits no links.</summary>
    private static readonly IReadOnlyDictionary<string, string> EmptyImagePaths =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    ///     Reports the honest gaps and caveats for the workbook's embedded images written earlier.
    /// </summary>
    /// <param name="sink">The sink to report through.</param>
    /// <param name="options">The effective options, consulted for suppression and force-PNG.</param>
    /// <param name="result">The image write accounting from the earlier write, or <see langword="null"/> when images were suppressed.</param>
    /// <returns><see langword="true"/> when any image gap was reported; otherwise <see langword="false"/>.</returns>
    /// <remarks>
    ///     When images are suppressed nothing was written and Core records the suppression itself, so
    ///     this reports nothing. A workbook that embeds no images reports nothing here, so an image-free
    ///     workbook stays a clean success. Vector metafiles carry a readability caveat, an image beyond a
    ///     caller limit is a counted size-skip, and a force-PNG request is explained rather than honored
    ///     because this backend ships no imaging stack. Side effect: records reports on the sink.
    /// </remarks>
    private static bool ReportImages(IExtractionSink sink, ExtractionOptions options, EmbeddedImageWriteResult? result)
    {
        // A caller who disabled embedded images asked for none to be attempted; Core records that
        // deliberate absence itself, so this unit reports nothing here
        if (result is null)
        {
            return false;
        }

        // A workbook that embeds no images has no images ledger to open; stay silent so it is a clean success
        if (result.Found == 0)
        {
            return false;
        }

        sink.ReportFound(GapKind.Images, result.Found);

        var degraded = false;
        if (result.VectorWrittenCount > 0)
        {
            ReportVectorImageDiagnostic(sink, result);
        }

        if (result.SizeSkippedCount > 0)
        {
            degraded |= ReportSizeSkipGap(sink, result);
        }

        if (options.ImageOutput == ImageOutputMode.ForcePng && result.ForcePngUnhonoredCount > 0)
        {
            degraded |= ReportForcePngGap(sink, result);
        }

        return degraded;
    }

    /// <summary>
    ///     Reports the informational readability caveat for EMF or WMF vector metafiles written unchanged.
    /// </summary>
    /// <param name="sink">The sink to report through.</param>
    /// <param name="result">The image write accounting carrying the vector and found counts.</param>
    /// <remarks>Informational, never a gap: the bytes are a complete image file, written and counted as extracted, and no better environment would yield more; the caveat is that many viewers cannot render a Windows metafile. Side effect: records a diagnostic.</remarks>
    private static void ReportVectorImageDiagnostic(IExtractionSink sink, EmbeddedImageWriteResult result)
    {
        var counted = Counted(result.VectorWrittenCount, result.Found);
        sink.ReportDiagnostic(new ExtractionDiagnostic(
            ExcelDiagnosticCodes.VectorImageWrittenAsIs, DiagnosticSeverity.Info,
            $"{counted} embedded images are EMF or WMF vector metafiles, which many viewers cannot render. "
            + "Their bytes were written unchanged and counted as extracted."));
    }

    /// <summary>
    ///     Reports the counted gap for images skipped because they exceed a caller-supplied limit.
    /// </summary>
    /// <param name="sink">The sink to report through.</param>
    /// <param name="result">The image write accounting carrying the size-skip count, items, and found total.</param>
    /// <returns>Always <see langword="true"/>, so the caller can accumulate the degraded signal.</returns>
    /// <remarks>A size skip is a deliberate, reversible choice, named as such and pointing at the option to relax. Side effect: records reports.</remarks>
    private static bool ReportSizeSkipGap(IExtractionSink sink, EmbeddedImageWriteResult result)
    {
        var counted = Counted(result.SizeSkippedCount, result.Found);
        sink.ReportGap(new ExtractionGap(
            string.Empty, GapKind.Images, ImagesTarget, GapScope.PartiallyExtracted,
            $"{counted} embedded images exceeded a caller-supplied size limit and were skipped rather than written.",
            Impact: "Those images are not present in the extracted output.",
            Remedy: "Raise or clear MaxImageBytes and MaxImageDimensionPx to extract images of any size.",
            AffectedCount: result.SizeSkippedCount,
            AffectedItems: result.SizeSkippedItems));
        return true;
    }

    /// <summary>
    ///     Reports that PNG output could not be honored, explaining that source bytes were written instead.
    /// </summary>
    /// <param name="sink">The sink to report through.</param>
    /// <param name="result">The image write accounting carrying the unhonored-force-PNG count and found total.</param>
    /// <returns>Always <see langword="true"/>, so the caller can accumulate the degraded signal.</returns>
    /// <remarks>The file extension follows the bytes actually written; this backend ships no imaging stack, so it cannot re-encode. Side effect: records reports.</remarks>
    private static bool ReportForcePngGap(IExtractionSink sink, EmbeddedImageWriteResult result)
    {
        var counted = Counted(result.ForcePngUnhonoredCount, result.Found);
        sink.ReportGap(new ExtractionGap(
            string.Empty, GapKind.Images, ImagesTarget, GapScope.PartiallyExtracted,
            $"PNG output was requested, but this backend ships no imaging stack and cannot re-encode; "
            + $"{counted} embedded images were written in their source encoding with a matching file "
            + "extension rather than converted.",
            Impact: "Those images are not in the requested PNG format; their file extensions and the "
            + "manifest media types describe what was actually written.",
            AffectedCount: result.ForcePngUnhonoredCount));
        return true;
    }

    /// <summary>
    ///     Renders a "{count} of {found}" or bare-count phrase for gap prose.
    /// </summary>
    /// <param name="count">The affected count.</param>
    /// <param name="found">The total found.</param>
    /// <returns>The phrase.</returns>
    /// <remarks>Reads naturally whether or not the affected set is the whole set. Pure.</remarks>
    private static string Counted(int count, int found)
    {
        var countText = count.ToString(CultureInfo.InvariantCulture);
        return count == found
            ? countText
            : $"{countText} of {found.ToString(CultureInfo.InvariantCulture)}";
    }

    /// <summary>The marker shown in the grid table for a value the table cannot carry.</summary>
    /// <remarks>
    ///     Named so the renderer and the note that explains it cannot drift: the note quotes this
    ///     exact character, and the elision detection compares against it rather than re-deriving the
    ///     condition.
    /// </remarks>
    private const string Elision = "…";

    /// <summary>The note explaining the table's elision marker, emitted only when a cell was elided.</summary>
    /// <remarks>
    ///     Without it a reader who sees <c>| 1 | … |</c> can reasonably conclude the content was not
    ///     recovered, when in fact the full value is a few lines further down: one real workbook's
    ///     <c>A1</c> renders as <c>…</c> purely because its value <c>Assumptions:</c> contains a
    ///     newline, which a GFM table cannot hold. The note names both the cause and where the value
    ///     actually is, so the elision reads as a formatting limit rather than a loss.
    /// </remarks>
    private const string ElisionNote =
        "_Cells shown as `…` are too long or contain line breaks, which a Markdown table cannot carry. "
        + "Their full values appear verbatim in the cell listing below._";

    /// <summary>
    ///     Renders one worksheet to markdown: a heading, an optional grid table for a dense
    ///     rectangular region, then the address/value/formula listing that is always present.
    /// </summary>
    /// <param name="sheet">The worksheet to render.</param>
    /// <param name="imagePaths">The map from an image part reference to its written relative path.</param>
    /// <returns>The worksheet's markdown.</returns>
    /// <remarks>
    ///     The listing is the lossless ledger: every cell's value is written verbatim and at full
    ///     length, and its formula (when present) is written alongside the value, because the formula
    ///     states the relationship and the value only one evaluation of it. The grid table is emitted
    ///     <em>in addition</em>, only when the populated region is table-shaped, to restore the
    ///     row/column relationships a reader would otherwise rebuild mentally; it never replaces the
    ///     listing and never carries the verbatim guarantee, so long or multi-line cells are elided in
    ///     the table with their full value preserved in the listing below — a fact the table now states
    ///     in a note whenever an elision actually happened. Any images the worksheet
    ///     references are linked inline after the listing. Pure.
    /// </remarks>
    private static string RenderSheet(ExcelSheetModel sheet, IReadOnlyDictionary<string, string> imagePaths)
    {
        var builder = new StringBuilder();
        builder.Append("# ").Append(sheet.Name).Append('\n').Append('\n');

        if (sheet.Cells.Count == 0)
        {
            builder.Append("_This worksheet has no cell content._").Append('\n');
            AppendImageLinks(builder, sheet, imagePaths);
            return builder.ToString();
        }

        builder.Append(sheet.Cells.Count.ToString(CultureInfo.InvariantCulture))
            .Append(sheet.Cells.Count == 1 ? " cell" : " cells").Append('\n').Append('\n');

        // A GFM table cannot span cells, so merged ranges are stated as a note rather than spanned
        if (sheet.MergedRanges.Count > 0)
        {
            builder.Append("Merged ranges: ").Append(string.Join(", ", sheet.MergedRanges)).Append('\n').Append('\n');
        }

        // The grid table is additive: emitted only for a dense rectangular region, never instead of the listing
        AppendGridTable(builder, sheet);

        foreach (var cell in sheet.Cells)
        {
            builder.Append("- `").Append(cell.Reference).Append('`');
            if (cell.Value is { } value)
            {
                builder.Append(": ").Append(value);
            }

            if (cell.Formula is { } formula)
            {
                builder.Append(" (formula: `=").Append(formula).Append("`)");
            }

            builder.Append('\n');
        }

        AppendImageLinks(builder, sheet, imagePaths);
        return builder.ToString();
    }

    /// <summary>
    ///     Appends an inline markdown link for each image the worksheet references that was written.
    /// </summary>
    /// <param name="builder">The buffer to append to.</param>
    /// <param name="sheet">The worksheet whose images are linked.</param>
    /// <param name="imagePaths">The map from an image part reference to its written relative path.</param>
    /// <remarks>
    ///     A link is emitted only when the sink returned a path for the image, so a suppressed,
    ///     size-skipped, or deduplicated-away image never leaves a link pointing at nothing — mirroring
    ///     Word. A picture shared across sheets is linked under each sheet, recording every reference.
    ///     Side effect: appends to <paramref name="builder"/>.
    /// </remarks>
    private static void AppendImageLinks(
        StringBuilder builder, ExcelSheetModel sheet, IReadOnlyDictionary<string, string> imagePaths)
    {
        var any = false;
        foreach (var image in sheet.Images)
        {
            if (!imagePaths.TryGetValue(image.SourceRef, out var path))
            {
                continue;
            }

            if (!any)
            {
                builder.Append('\n');
                any = true;
            }

            builder.Append("![").Append(ImageLinkText.Alt(image.AltText)).Append("](").Append(path).Append(")")
                .Append('\n').Append('\n');
        }
    }

    /// <summary>
    ///     Appends a markdown grid table for a sheet's dense rectangular region, when one qualifies.
    /// </summary>
    /// <param name="builder">The buffer to append to.</param>
    /// <param name="sheet">The worksheet whose populated region is considered.</param>
    /// <remarks>
    ///     Qualification applies the measured heuristic: at least <see cref="MinTableRowSpan"/> rows
    ///     and <see cref="MinTableColumnSpan"/> columns, no wider than <see cref="MaxTableColumnSpan"/>
    ///     columns, and fill density at least <see cref="MinTableDensity"/>. Header cells are the
    ///     column letters and the leading cell of each body row is its row number, so every table cell
    ///     stays addressable (for example <c>J1</c>). A cell longer than
    ///     <see cref="MaxTableCellLength"/> or carrying a newline is elided in the table only; a
    ///     formula cell shows its cached value or <c>=…</c> when value-less. When any cell was
    ///     actually elided, <see cref="ElisionNote"/> follows the table so a reader is never left to
    ///     guess whether the content was lost. Appends nothing when the
    ///     region is not table-shaped. Side effect: appends to <paramref name="builder"/>.
    /// </remarks>
    private static void AppendGridTable(StringBuilder builder, ExcelSheetModel sheet)
    {
        // Compute the populated bounding rectangle and index cells by their (row, column) position
        var byPosition = new Dictionary<(int Row, int Column), ExcelCellModel>();
        var minRow = int.MaxValue;
        var maxRow = int.MinValue;
        var minColumn = int.MaxValue;
        var maxColumn = int.MinValue;
        foreach (var cell in sheet.Cells)
        {
            if (!TryParseReference(cell.Reference, out var row, out var column))
            {
                continue;
            }

            byPosition[(row, column)] = cell;
            minRow = Math.Min(minRow, row);
            maxRow = Math.Max(maxRow, row);
            minColumn = Math.Min(minColumn, column);
            maxColumn = Math.Max(maxColumn, column);
        }

        // A sheet whose cells carry no parseable A1 references cannot be gridded
        if (byPosition.Count == 0)
        {
            return;
        }

        var rowSpan = maxRow - minRow + 1;
        var columnSpan = maxColumn - minColumn + 1;
        var density = (double)byPosition.Count / (rowSpan * columnSpan);

        // Apply the qualification heuristic; a region that is not table-shaped keeps the listing alone
        if (rowSpan < MinTableRowSpan || columnSpan < MinTableColumnSpan
            || columnSpan > MaxTableColumnSpan || density < MinTableDensity)
        {
            return;
        }

        // Header row of column letters, with an empty corner above the row-number column
        builder.Append('|').Append(' ').Append(" |");
        for (var column = minColumn; column <= maxColumn; column++)
        {
            builder.Append(' ').Append(ColumnLetters(column)).Append(" |");
        }

        builder.Append('\n');

        // Separator row: one divider per column plus the leading row-number column
        builder.Append("| --- |");
        for (var column = minColumn; column <= maxColumn; column++)
        {
            builder.Append(" --- |");
        }

        builder.Append('\n');

        // Body rows: a leading row number then each cell, elided where it would break the table
        var elided = false;
        for (var row = minRow; row <= maxRow; row++)
        {
            builder.Append("| ").Append(row.ToString(CultureInfo.InvariantCulture)).Append(" |");
            for (var column = minColumn; column <= maxColumn; column++)
            {
                var text = byPosition.TryGetValue((row, column), out var cell) ? DisplayInTable(cell) : string.Empty;
                elided |= string.Equals(text, Elision, StringComparison.Ordinal);
                builder.Append(' ').Append(text).Append(" |");
            }

            builder.Append('\n');
        }

        builder.Append('\n');

        // Explain the marker only when one was actually used; an unconditional note would spend
        // tokens on a caveat that did not apply
        if (elided)
        {
            builder.Append(ElisionNote).Append('\n').Append('\n');
        }
    }

    /// <summary>
    ///     Produces a cell's table-safe display text, eliding what a table cannot carry.
    /// </summary>
    /// <param name="cell">The cell to display.</param>
    /// <returns>The value, an elision marker, or a formula placeholder.</returns>
    /// <remarks>
    ///     A value longer than <see cref="MaxTableCellLength"/> or containing a newline is shown as
    ///     the elision marker <c>…</c> because its full verbatim form lives in the listing; a
    ///     value-less formula cell is shown as <c>=…</c>. Pipe characters are escaped so a value can
    ///     never break the table's column structure. Pure.
    /// </remarks>
    private static string DisplayInTable(ExcelCellModel cell)
    {
        if (cell.Value is { } value)
        {
            if (value.Length > MaxTableCellLength || value.Contains('\n', StringComparison.Ordinal))
            {
                return Elision;
            }

            return value.Replace("|", "\\|", StringComparison.Ordinal);
        }

        // A cell with only a formula has no cached value to show; mark it so the listing carries the formula
        return cell.Formula is not null ? "=" + Elision : string.Empty;
    }

    /// <summary>
    ///     Parses an A1-style cell reference into its 1-based row and column numbers.
    /// </summary>
    /// <param name="reference">The A1-style reference (for example <c>J1</c> or <c>AB12</c>).</param>
    /// <param name="row">The parsed 1-based row number when the parse succeeds.</param>
    /// <param name="column">The parsed 1-based column number when the parse succeeds.</param>
    /// <returns><see langword="true"/> when the reference parses to a row and column; otherwise <see langword="false"/>.</returns>
    /// <remarks>
    ///     A reference is a run of column letters followed by a run of row digits; anything else (an
    ///     absolute <c>$</c> marker, a range, or a malformed value) fails the parse and is left out of
    ///     the grid rather than guessed. Pure.
    /// </remarks>
    private static bool TryParseReference(string reference, out int row, out int column)
    {
        row = 0;
        column = 0;

        var index = 0;
        while (index < reference.Length && reference[index] is >= 'A' and <= 'Z')
        {
            column = (column * 26) + (reference[index] - 'A' + 1);
            index++;
        }

        // A reference needs at least one letter and then only digits to be a single-cell address
        if (index == 0 || index == reference.Length)
        {
            return false;
        }

        for (var digit = index; digit < reference.Length; digit++)
        {
            if (reference[digit] is < '0' or > '9')
            {
                return false;
            }

            row = (row * 10) + (reference[digit] - '0');
        }

        return true;
    }

    /// <summary>
    ///     Converts a 1-based column number to its spreadsheet letters.
    /// </summary>
    /// <param name="column">The 1-based column number (1 maps to <c>A</c>, 27 to <c>AA</c>).</param>
    /// <returns>The column's letter label.</returns>
    /// <remarks>Used for the grid table's header row so each column stays addressable by its real letter. Pure.</remarks>
    private static string ColumnLetters(int column)
    {
        var letters = new StringBuilder();
        var remaining = column;
        while (remaining > 0)
        {
            var zeroBased = (remaining - 1) % 26;
            letters.Insert(0, (char)('A' + zeroBased));
            remaining = (remaining - 1) / 26;
        }

        return letters.ToString();
    }
}
