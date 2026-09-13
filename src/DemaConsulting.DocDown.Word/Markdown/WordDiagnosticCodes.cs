namespace DocDown.Word.Markdown;

/// <summary>
///     The diagnostic codes this package owns.
/// </summary>
/// <remarks>
///     Core's <c>DD</c> range is internal to Core and the <c>PDF</c> range belongs to the PDF
///     package, so a distinct <c>WORD</c> prefix makes the ownership boundary self-evident in any
///     manifest and cannot collide with another package's range whatever it adds later. Internal
///     because the codes are a published output value, not an API consumers program against. All
///     members are constants and thread-safe.
///     <para>
///         The numbering is contiguous and stable — a table-pinning test treats it as a contract —
///         so a consumer branching on a code is never surprised by a renumbering.
///     </para>
/// </remarks>
internal static class WordDiagnosticCodes
{
    /// <summary>The document carries no extractable text.</summary>
    /// <remarks>Accompanies the empty-content gap so the absence is machine-detectable.</remarks>
    internal const string NoTextContent = "WORD0001";

    /// <summary>The document is encrypted or password-protected and cannot be opened.</summary>
    /// <remarks>Accompanies the structured failure raised when the OLE compound-file signature is detected.</remarks>
#pragma warning disable S2068 // This is a diagnostic-code constant, not a credential
    internal const string PasswordProtected = "WORD0002";
#pragma warning restore S2068

    /// <summary>A table with no cell content was skipped.</summary>
    /// <remarks>Accompanies the note that an empty table contributed nothing to the content.</remarks>
    internal const string EmptyTableSkipped = "WORD0003";

    /// <summary>A table's first row was used as the GFM header though Word did not mark it a header row.</summary>
    /// <remarks>
    ///     GFM requires a delimiter row, so row one is always the header; this states the assumption
    ///     rather than hiding it when <c>w:tblHeader</c> was absent.
    /// </remarks>
    internal const string TableHeaderAssumed = "WORD0004";

    /// <summary>Merged (<c>w:gridSpan</c>/<c>w:vMerge</c>) or nested table cells were flattened.</summary>
    /// <remarks>
    ///     Accompanies the counted structural gap. GFM cannot express a merge or a nested table, so
    ///     the flattening is stated with a count rather than performed silently.
    /// </remarks>
    internal const string MergedCellsFlattened = "WORD0005";

    /// <summary>An EMF or WMF vector image was written unchanged.</summary>
    /// <remarks>
    ///     Informational, never a gap: the bytes are a complete image file, so they are written and
    ///     counted as extracted, and no better environment would yield more because this package
    ///     ships no metafile rasterizer. The caveat that many viewers cannot render vector metafiles
    ///     is stated for the reader, but it must not degrade the run.
    /// </remarks>
    internal const string VectorImageWrittenAsIs = "WORD0006";

    /// <summary>PNG output was requested but this package ships no imaging stack.</summary>
    /// <remarks>
    ///     Accompanies the gap explaining that source bytes were written as-is instead, because the
    ///     package cannot decode and re-encode. Mirrors the PDF package's unhonored force-PNG note.
    /// </remarks>
    internal const string ForcePngNotHonored = "WORD0007";

    /// <summary>Tracked changes were rendered in the accepted-revisions view.</summary>
    /// <remarks>Accompanies the note (with the revision count) that a view choice was made.</remarks>
    internal const string TrackedChangesAccepted = "WORD0008";

    /// <summary>A header or footer carrying only page-numbering fields (or empty) was omitted from Document Control.</summary>
    /// <remarks>Accompanies the counted, reasoned gap recording the omission.</remarks>
    internal const string HeaderFooterPageNumberingOnly = "WORD0009";

    /// <summary>The document embeds charts whose plotted data this backend does not read.</summary>
    /// <remarks>
    ///     Accompanies the counted gap naming the charts. A Word chart stores its plotted values in a
    ///     DrawingML chart part that carries no image blip, so without this the chart would leave no
    ///     trace whatever in the output while the summary still claimed a complete extraction — the
    ///     exact silent-loss failure the output contract exists to prevent.
    /// </remarks>
    internal const string ChartsNotExtracted = "WORD0010";
}
