namespace DocDown.Core;

/// <summary>
///     The kind of logical content part a document is split into.
/// </summary>
/// <remarks>
///     Different document families divide naturally into different units — a workbook into
///     sheets, a deck into slides — and recording the specific kind lets output name and present
///     each part meaningfully rather than as an anonymous section.
/// </remarks>
public enum ContentPartKind
{
    /// <summary>A page of a paged document.</summary>
    Page,

    /// <summary>A worksheet of a spreadsheet.</summary>
    Sheet,

    /// <summary>A slide of a presentation.</summary>
    Slide,

    /// <summary>A section of a structured document.</summary>
    Section,

    /// <summary>An attachment embedded in the document.</summary>
    Attachment
}
