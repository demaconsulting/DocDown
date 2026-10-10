using System.Globalization;
using DemaConsulting.DocDown.Core;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using S = DocumentFormat.OpenXml.Spreadsheet;
using Tc = DocumentFormat.OpenXml.Office2019.Excel.ThreadedComments;

namespace DemaConsulting.DocDown.Excel.OpenXml;

/// <summary>
///     Reads a spreadsheet package into the backend-neutral <see cref="ExcelWorkbookModel"/>,
///     resolving shared strings and preserving every cell's value and formula verbatim.
/// </summary>
/// <remarks>
///     The reader is the one place that touches the Open XML object model. It walks each worksheet
///     in workbook order, keeps only cells that carry a value or a formula, and never truncates a
///     value — a cell holding a page of pasted prose survives intact, because losing length is
///     exactly the failure the Excel intent forbids. It performs no filesystem I/O; the caller hands
///     it a seekable stream. Stateless and safe to reuse.
/// </remarks>
internal static class ExcelOpenXmlReader
{
    /// <summary>
    ///     Reads a workbook from a seekable stream into the model.
    /// </summary>
    /// <param name="stream">A readable, seekable stream over the <c>.xlsx</c> bytes.</param>
    /// <returns>The workbook model with its worksheets and cells in document order.</returns>
    /// <exception cref="ExcelExtractionException">Thrown when the package cannot be opened or carries no workbook part.</exception>
    /// <remarks>Read-only over the stream. Any Open XML fault is wrapped so Core sees a structured failure.</remarks>
    public static ExcelWorkbookModel Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        SpreadsheetDocument document;
        try
        {
            document = SpreadsheetDocument.Open(stream, isEditable: false);
        }
        catch (OpenXmlPackageException exception)
        {
            throw new ExcelExtractionException(
                "The workbook could not be opened; it may be encrypted, malformed, or not a valid Open XML spreadsheet.",
                exception);
        }
        catch (FileFormatException exception)
        {
            throw new ExcelExtractionException(
                "The workbook could not be opened; it may be encrypted, malformed, or not a valid Open XML spreadsheet.",
                exception);
        }

        using (document)
        {
            var workbookPart = document.WorkbookPart
                ?? throw new ExcelExtractionException("The workbook contains no workbook part and cannot be read.");

            var sharedStrings = LoadSharedStrings(workbookPart);
            var personNames = LoadPersonNames(workbookPart);
            var imageCollection = ExcelOpenXmlImageReader.Collect(workbookPart);
            var sheets = new List<ExcelSheetModel>();

            var sheetElements = workbookPart.Workbook?.Sheets?.Elements<S.Sheet>() ?? [];
            var sheetIndex = 1;
            foreach (var sheet in sheetElements)
            {
                var name = sheet.Name?.Value ?? "Sheet";
                var worksheetPart = sheet.Id?.Value is { } relationshipId
                    && workbookPart.GetPartById(relationshipId) is WorksheetPart part
                    ? part
                    : null;
                var cells = worksheetPart is not null ? ReadCells(worksheetPart, sharedStrings) : [];
                var merges = worksheetPart is not null ? ReadMergedRanges(worksheetPart) : [];
                var comments = worksheetPart is not null ? ReadComments(worksheetPart, personNames) : [];
                var images = imageCollection.SheetImageRefs.TryGetValue(sheetIndex, out var refs) ? refs : [];
                var charts = ExcelChartReader.Collect(worksheetPart, name);
                var shapeTexts = ExcelDrawingTextReader.Collect(worksheetPart);
                sheets.Add(new ExcelSheetModel(name, cells, merges, images, charts, shapeTexts, comments));
                sheetIndex++;
            }

            var allImages = imageCollection.Images;

            return new ExcelWorkbookModel(sheets, allImages, OpcMetadataMapper.From(new OpcCoreProperties(
                document.PackageProperties.Creator,
                document.PackageProperties.LastModifiedBy,
                document.PackageProperties.Created,
                document.PackageProperties.Modified,
                document.PackageProperties.Revision,
                document.PackageProperties.Title,
                document.PackageProperties.Subject,
                document.PackageProperties.Keywords,
                document.PackageProperties.Category,
                document.PackageProperties.ContentStatus,
                document.PackageProperties.Description,
                document.PackageProperties.LastPrinted,
                document.PackageProperties.Version,
                document.PackageProperties.Language,
                document.PackageProperties.Identifier)));
        }
    }

    /// <summary>
    ///     Loads the shared-string table, whose entries string-typed cells reference by index.
    /// </summary>
    /// <param name="workbookPart">The workbook part.</param>
    /// <returns>The shared strings in index order, or an empty list when the workbook has none.</returns>
    /// <remarks>
    ///     Excel stores most text once in a shared table and has cells reference it by index; reading
    ///     the table up front lets every cell resolve its text without re-walking the part. Whole
    ///     inner text is taken so rich-text runs concatenate into the complete value.
    /// </remarks>
    private static IReadOnlyList<string> LoadSharedStrings(WorkbookPart workbookPart)
    {
        var table = workbookPart.SharedStringTablePart?.SharedStringTable;
        if (table is null)
        {
            return [];
        }

        var strings = new List<string>();
        foreach (var item in table.Elements<S.SharedStringItem>())
        {
            strings.Add(item.InnerText);
        }

        return strings;
    }

    /// <summary>
    ///     Reads a worksheet's non-empty cells in row-major order.
    /// </summary>
    /// <param name="worksheetPart">The worksheet part.</param>
    /// <param name="sharedStrings">The resolved shared-string table.</param>
    /// <returns>The worksheet's cells that carry a value or a formula.</returns>
    /// <remarks>
    ///     A cell with neither a value nor a formula contributes nothing and is dropped, so a sparse
    ///     sheet yields only its real content. Read-only over the part.
    /// </remarks>
    private static IReadOnlyList<ExcelCellModel> ReadCells(
        WorksheetPart worksheetPart, IReadOnlyList<string> sharedStrings)
    {
        var sheetData = worksheetPart.Worksheet?.GetFirstChild<S.SheetData>();
        if (sheetData is null)
        {
            return [];
        }

        var cells = new List<ExcelCellModel>();
        foreach (var row in sheetData.Elements<S.Row>())
        {
            foreach (var cell in row.Elements<S.Cell>())
            {
                var reference = cell.CellReference?.Value;
                if (string.IsNullOrEmpty(reference))
                {
                    continue;
                }

                var value = ResolveValue(cell, sharedStrings);
                var formula = cell.CellFormula?.Text;
                if (string.IsNullOrEmpty(formula))
                {
                    formula = null;
                }

                if (value is null && formula is null)
                {
                    continue;
                }

                cells.Add(new ExcelCellModel(reference, value, formula));
            }
        }

        return cells;
    }

    /// <summary>
    ///     Reads a worksheet's declared merged-cell ranges in document order.
    /// </summary>
    /// <param name="worksheetPart">The worksheet part.</param>
    /// <returns>The A1-style merge references (for example <c>A1:C1</c>), or an empty list when none.</returns>
    /// <remarks>
    ///     A merge stores its value only in the anchor cell, so the non-anchor cells are already
    ///     empty and dropped by the cell reader; the ranges are carried so the emitter can note them
    ///     beside a grid table, which a GFM table cannot visually span. A reference that is blank is
    ///     skipped rather than carried as an empty range. Read-only over the part.
    /// </remarks>
    private static IReadOnlyList<string> ReadMergedRanges(WorksheetPart worksheetPart)
    {
        var mergeCells = worksheetPart.Worksheet?.GetFirstChild<S.MergeCells>();
        if (mergeCells is null)
        {
            return [];
        }

        var ranges = new List<string>();
        foreach (var merge in mergeCells.Elements<S.MergeCell>())
        {
            var reference = merge.Reference?.Value;
            if (!string.IsNullOrWhiteSpace(reference))
            {
                ranges.Add(reference);
            }
        }

        return ranges;
    }

    /// <summary>
    ///     Loads the workbook's person list, which modern threaded comments reference by identifier
    ///     instead of naming their author inline.
    /// </summary>
    /// <param name="workbookPart">The workbook part.</param>
    /// <returns>The display name of each person, keyed by the identifier a threaded comment cites.</returns>
    /// <remarks>
    ///     A threaded comment carries only a <c>personId</c>; the names live once in the workbook's
    ///     person part, so reading them up front lets every threaded comment resolve its author
    ///     without re-walking the package. A workbook with no person part yields an empty map and
    ///     every threaded comment is then reported unattributed rather than attributed to a guess.
    /// </remarks>
    private static IReadOnlyDictionary<string, string> LoadPersonNames(WorkbookPart workbookPart)
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        var personLists = workbookPart.WorkbookPersonParts
            .Select(personPart => personPart.PersonList)
            .Where(personList => personList is not null);
        foreach (var personList in personLists)
        {
            foreach (var person in personList!.Elements<Tc.Person>())
            {
                if (person.Id?.Value is { } id && person.DisplayName?.Value is { Length: > 0 } displayName)
                {
                    names[id] = displayName;
                }
            }
        }

        return names;
    }

    /// <summary>
    ///     Reads a worksheet's reviewer comments, from both the legacy comments part and the modern
    ///     threaded comments parts, in cell order.
    /// </summary>
    /// <param name="worksheetPart">The worksheet part.</param>
    /// <param name="personNames">The workbook's person display names, keyed by identifier.</param>
    /// <returns>The worksheet's comments, or an empty list when it carries none.</returns>
    /// <remarks>
    ///     <para>
    ///         A spreadsheet carries comments in two grammars at once. The legacy part
    ///         (<c>xl/comments<em>n</em>.xml</c>) names its authors in a per-part author list that
    ///         each comment indexes into; the modern threaded parts carry a conversation per cell and
    ///         name their authors only by a person identifier resolved through the workbook's person
    ///         part. Both are read, because a workbook may use either or both.
    ///     </para>
    ///     <para>
    ///         <strong>Deduplication rule.</strong> A spreadsheet application that writes a threaded
    ///         comment normally also writes a legacy comment on the same cell for backward
    ///         compatibility, so reading both parts naively would report the same remark twice. The
    ///         rule applied here is the one that cannot double-count: <em>a cell's threaded comments
    ///         win, and its legacy comment is kept only when the cell carries no threaded comment at
    ///         all</em>. Matching the backward-compatibility placeholder by its text was deliberately
    ///         not attempted: the exact wording varies by application version and locale, so a text
    ///         match could not be confirmed against a genuine fixture and an unconfirmed match would
    ///         either leak a duplicate or drop a real legacy comment. Keying on the cell reference
    ///         alone loses nothing a reader would want, because the threaded conversation carries the
    ///         same remark with better attribution.
    ///     </para>
    ///     <para>
    ///         Order follows the legacy part's cell order where one exists, with any cell commented
    ///         only in a threaded part appended afterward, so the common workbook reads down the
    ///         sheet. Read-only over the parts.
    ///     </para>
    /// </remarks>
    private static IReadOnlyList<ExcelCommentModel> ReadComments(
        WorksheetPart worksheetPart, IReadOnlyDictionary<string, string> personNames)
    {
        var threaded = ReadThreadedComments(worksheetPart, personNames);
        var legacy = ReadLegacyComments(worksheetPart);
        if (threaded.Count == 0)
        {
            return legacy;
        }

        var threads = new Dictionary<string, List<ExcelCommentModel>>(StringComparer.OrdinalIgnoreCase);
        foreach (var comment in threaded)
        {
            if (!threads.TryGetValue(comment.Reference, out var thread))
            {
                thread = [];
                threads[comment.Reference] = thread;
            }

            thread.Add(comment);
        }

        var comments = new List<ExcelCommentModel>();
        var emitted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Walk the legacy part first: it lists every commented cell in sheet order, including the
        // backward-compatibility entries written beside the threaded conversations, so following it
        // keeps the reported order the order a reader scanning the sheet would meet
        foreach (var comment in legacy)
        {
            if (!threads.TryGetValue(comment.Reference, out var thread))
            {
                comments.Add(comment);
            }
            else if (emitted.Add(comment.Reference))
            {
                comments.AddRange(thread);
            }
        }

        // A cell commented only in a threaded part has no legacy entry to order it by, so it follows
        // in the order the threaded parts declare it
        comments.AddRange(threaded.Where(comment => !emitted.Contains(comment.Reference)));

        return comments;
    }

    /// <summary>
    ///     Reads the worksheet's legacy comments in document order.
    /// </summary>
    /// <param name="worksheetPart">The worksheet part.</param>
    /// <returns>The legacy comments, or an empty list when the worksheet has no legacy comments part.</returns>
    /// <remarks>
    ///     A legacy comment names its author by index into the part's own author list, so the list is
    ///     resolved first and an index naming no author yields no attribution rather than a guess. A
    ///     comment with neither text nor a cell reference is dropped, because it cites nothing.
    ///     Read-only over the part.
    /// </remarks>
    private static List<ExcelCommentModel> ReadLegacyComments(WorksheetPart worksheetPart)
    {
        var comments = new List<ExcelCommentModel>();
        var part = worksheetPart.WorksheetCommentsPart?.Comments;
        if (part is null)
        {
            return comments;
        }

        var authors = part.Authors?.Elements<S.Author>().Select(author => author.Text).ToList() ?? [];

        foreach (var comment in part.CommentList?.Elements<S.Comment>() ?? [])
        {
            var reference = comment.Reference?.Value;
            if (string.IsNullOrEmpty(reference))
            {
                continue;
            }

            var text = comment.CommentText?.InnerText;
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            string? author = null;
            if (comment.AuthorId?.Value is { } authorId && authorId < authors.Count
                && !string.IsNullOrWhiteSpace(authors[(int)authorId]))
            {
                author = authors[(int)authorId];
            }

            comments.Add(new ExcelCommentModel(reference, author, text));
        }

        return comments;
    }

    /// <summary>
    ///     Reads the worksheet's modern threaded comments in document order.
    /// </summary>
    /// <param name="worksheetPart">The worksheet part.</param>
    /// <param name="personNames">The workbook's person display names, keyed by identifier.</param>
    /// <returns>The threaded comments, in the order the threaded parts declare them.</returns>
    /// <remarks>
    ///     A thread's replies are kept in the order the part declares them, because a conversation
    ///     read out of order misstates who answered whom. A person identifier the workbook's person
    ///     part does not name yields no attribution rather than the raw identifier, which would read
    ///     as a name without being one. Read-only over the parts.
    /// </remarks>
    private static List<ExcelCommentModel> ReadThreadedComments(
        WorksheetPart worksheetPart, IReadOnlyDictionary<string, string> personNames)
    {
        var comments = new List<ExcelCommentModel>();
        foreach (var part in worksheetPart.WorksheetThreadedCommentsParts)
        {
            foreach (var comment in part.ThreadedComments?.Elements<Tc.ThreadedComment>() ?? [])
            {
                var reference = comment.Ref?.Value;
                if (string.IsNullOrEmpty(reference))
                {
                    continue;
                }

                var text = comment.ThreadedCommentText?.Text;
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                string? author = null;
                if (comment.PersonId?.Value is { } personId && personNames.TryGetValue(personId, out var name))
                {
                    author = name;
                }

                comments.Add(new ExcelCommentModel(reference, author, text));
            }
        }

        return comments;
    }

    /// <summary>
    ///     Resolves a cell's value to its verbatim text, following the cell's data type.
    /// </summary>
    /// <param name="cell">The cell to resolve.</param>
    /// <param name="sharedStrings">The resolved shared-string table.</param>
    /// <returns>The cell's value text, or <see langword="null"/> when it carries no value.</returns>
    /// <remarks>
    ///     Shared-string and inline-string cells resolve to their full text; a boolean resolves to
    ///     <c>TRUE</c>/<c>FALSE</c>; every other type (number, date serial, error) is taken exactly as
    ///     stored so no precision is lost to a reformat. The text is never truncated.
    /// </remarks>
    private static string? ResolveValue(S.Cell cell, IReadOnlyList<string> sharedStrings)
    {
        var dataType = cell.DataType?.Value;

        if (dataType == S.CellValues.InlineString)
        {
            return cell.InlineString?.InnerText;
        }

        var raw = cell.CellValue?.Text;
        if (dataType == S.CellValues.SharedString)
        {
            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index)
                && index >= 0 && index < sharedStrings.Count
                ? sharedStrings[index]
                : null;
        }

        if (dataType == S.CellValues.Boolean)
        {
            if (raw is null)
            {
                return null;
            }

            return raw == "0" ? "FALSE" : "TRUE";
        }

        return string.IsNullOrEmpty(raw) ? null : raw;
    }
}

/// <summary>
///     The exception the Excel reader raises for a workbook it cannot open or interpret, so Core can
///     convert it into a structured failure rather than letting a raw Open XML fault reach the caller.
/// </summary>
/// <remarks>
///     Raised for a missing workbook part or a package the Open XML SDK cannot open (an encrypted or
///     malformed <c>.xlsx</c>). Core catches it and writes a structured failure with the full output
///     layout still present, so an adverse workbook never surfaces as an unhandled exception.
/// </remarks>
public sealed class ExcelExtractionException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ExcelExtractionException"/> class.
    /// </summary>
    public ExcelExtractionException()
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ExcelExtractionException"/> class with a message.
    /// </summary>
    /// <param name="message">The message describing what could not be read.</param>
    public ExcelExtractionException(string message)
        : base(message)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ExcelExtractionException"/> class with a
    ///     message and an inner exception.
    /// </summary>
    /// <param name="message">The message describing what could not be read.</param>
    /// <param name="innerException">The underlying fault.</param>
    public ExcelExtractionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
