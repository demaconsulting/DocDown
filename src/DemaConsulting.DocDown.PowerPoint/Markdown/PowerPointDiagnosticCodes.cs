namespace DocDown.PowerPoint.Markdown;

/// <summary>
///     The diagnostic codes this package owns.
/// </summary>
/// <remarks>
///     Core's <c>DD</c> range is internal to Core and each backend owns its own prefix, so a
///     distinct <c>PPTX</c> prefix makes the ownership boundary self-evident in any manifest and
///     cannot collide with another package's range. Internal because the codes are a published
///     output value, not an API consumers program against. The numbering is contiguous and stable —
///     a table-pinning test treats it as a contract. All members are constants and thread-safe.
/// </remarks>
internal static class PowerPointDiagnosticCodes
{
    /// <summary>The presentation contains no slides, so no content could be produced.</summary>
    /// <remarks>Accompanies the empty-deck gap so the absence is machine-detectable.</remarks>
    internal const string NoSlides = "PPTX0001";

    /// <summary>The deck's slides carry no speaker notes, though notes were read from every slide.</summary>
    /// <remarks>
    ///     Informational, never a gap: a notes-less deck is well-formed and no better environment
    ///     would yield notes that do not exist, so the whole-deck absence is stated as an
    ///     informational diagnostic rather than a shortfall. Distinguishes "this deck has no speaker
    ///     notes" (notes were looked for and none exist) from "notes were not looked for", which
    ///     never happens because this backend always reads the notes slide.
    /// </remarks>
    internal const string NoSpeakerNotes = "PPTX0002";

    /// <summary>Embedded images include EMF or WMF vector metafiles, written unchanged with a readability caveat.</summary>
    /// <remarks>
    ///     Informational, never a gap: the bytes are a complete image file and are written and counted
    ///     as extracted, and no better environment or configuration would yield more because this
    ///     backend ships no metafile rasterizer. The caveat that many viewers and image libraries
    ///     cannot render a Windows metafile is stated for the reader, but it must not degrade the run.
    /// </remarks>
    internal const string VectorImageWrittenAsIs = "PPTX0003";

    /// <summary>A slide could not be rendered by the COM backend and was omitted.</summary>
    /// <remarks>Accompanies the counted pages gap naming the slide; the run continued with the remaining slides.</remarks>
    internal const string SlideRenderFailed = "PPTX0004";
}
