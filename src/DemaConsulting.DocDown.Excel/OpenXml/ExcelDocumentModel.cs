using DocDown.Core;

namespace DocDown.Excel.OpenXml;

/// <summary>
///     The backend-neutral model of a whole workbook: its worksheets in workbook order, each with
///     the non-empty cells it carries.
/// </summary>
/// <param name="Sheets">The worksheets, in the order the workbook declares them.</param>
/// <param name="Images">
///     The embedded images resolved from the worksheet drawings, deduplicated by package part, in
///     worksheet order. Empty when the workbook embeds no images. Written through the sink, which
///     deduplicates again by content.
/// </param>
/// <param name="Metadata">
///     What the workbook asserts about itself, mapped from the OPC core properties, or
///     <see langword="null"/> when not captured (for example a hand-built test model).
/// </param>
/// <remarks>
///     The reader populates this model from the Open XML package and hands it to the emitter, so
///     every decision about what reaches the output is made once against a model that can be built
///     by hand with no workbook behind it. Immutable and thread-safe.
/// </remarks>
internal sealed record ExcelWorkbookModel(
    IReadOnlyList<ExcelSheetModel> Sheets,
    IReadOnlyList<EmbeddedImage> Images,
    DocumentMetadata? Metadata = null)
{
    /// <summary>
    ///     Initializes a workbook model that embeds no images, for a hand-built model with no workbook behind it.
    /// </summary>
    /// <param name="sheets">The worksheets, in workbook order.</param>
    /// <param name="metadata">The self-reported metadata, or <see langword="null"/>.</param>
    /// <remarks>A convenience for tests and callers that do not exercise embedded images; images default to empty.</remarks>
    public ExcelWorkbookModel(IReadOnlyList<ExcelSheetModel> sheets, DocumentMetadata? metadata = null)
        : this(sheets, [], metadata)
    {
    }
}

/// <summary>
///     One worksheet: its name, the non-empty cells it carries, in row-major order, the merged
///     cell ranges declared on it, and the images it references.
/// </summary>
/// <param name="Name">The worksheet name as shown on its tab; the citable identity of a sheet.</param>
/// <param name="Cells">The worksheet's non-empty cells, in row-then-column order.</param>
/// <param name="MergedRanges">
///     The A1-style merged ranges declared on the sheet (for example <c>A1:C1</c>), in document
///     order. Empty when the sheet declares no merges. A merge stores its value only in the anchor
///     cell, so the range is recorded as a note beside the grid table rather than visually spanned,
///     which a GFM table cannot do.
/// </param>
/// <param name="Images">
///     The images this worksheet references, in drawing order and distinct by package part, so the
///     emitter can link each one inline under the worksheet section — following Word's convention. A
///     picture shared across sheets appears in each sheet's list, recording every reference. Empty
///     when the worksheet carries no picture.
/// </param>
/// <remarks>Immutable and thread-safe.</remarks>
internal sealed record ExcelSheetModel(
    string Name, IReadOnlyList<ExcelCellModel> Cells, IReadOnlyList<string> MergedRanges,
    IReadOnlyList<ExcelSheetImageRef> Images)
{
    /// <summary>
    ///     Initializes a worksheet model that references no images inline, for a hand-built model.
    /// </summary>
    /// <param name="name">The worksheet name.</param>
    /// <param name="cells">The worksheet's non-empty cells.</param>
    /// <param name="mergedRanges">The A1-style merged ranges.</param>
    /// <remarks>A convenience for tests that do not exercise inline image links; images default to empty.</remarks>
    public ExcelSheetModel(string name, IReadOnlyList<ExcelCellModel> cells, IReadOnlyList<string> mergedRanges)
        : this(name, cells, mergedRanges, [])
    {
    }
}

/// <summary>
///     One image occurrence on a worksheet: the package-part reference that keys the written-path
///     map, and the alt text to show, if any.
/// </summary>
/// <param name="SourceRef">The image part URI within the package; the key into the sink's written-path map.</param>
/// <param name="AltText">
///     The image's descriptive alt text when a genuinely descriptive source was chosen; otherwise
///     <see langword="null"/> so a neutral placeholder is used rather than implying a description.
/// </param>
/// <remarks>Immutable and thread-safe.</remarks>
internal sealed record ExcelSheetImageRef(string SourceRef, string? AltText);

/// <summary>
///     One cell: its address, its value verbatim, and the formula that produced it when present.
/// </summary>
/// <param name="Reference">The A1-style cell address (for example <c>B7</c>), so a fact can be cited back to its origin.</param>
/// <param name="Value">
///     The cell's computed value, preserved verbatim at full precision and full length, or
///     <see langword="null"/> when the cell holds only a formula with no cached value.
/// </param>
/// <param name="Formula">
///     The formula behind a computed cell (without a leading <c>=</c>), or <see langword="null"/>
///     when the cell carries no formula. Retained alongside the value because the formula states the
///     engineering relationship and the value states only one evaluation of it.
/// </param>
/// <remarks>Immutable and thread-safe.</remarks>
internal sealed record ExcelCellModel(string Reference, string? Value, string? Formula);
