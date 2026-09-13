using System.Globalization;
using DocDown.Core;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using S = DocumentFormat.OpenXml.Spreadsheet;

namespace DocDown.Excel.OpenXml;

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
                var images = imageCollection.SheetImageRefs.TryGetValue(sheetIndex, out var refs) ? refs : [];
                sheets.Add(new ExcelSheetModel(name, cells, merges, images));
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
