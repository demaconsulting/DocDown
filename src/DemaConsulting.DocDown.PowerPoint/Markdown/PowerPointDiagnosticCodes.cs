namespace DocDown.PowerPoint.Markdown;

/// <summary>
///     The diagnostic codes this package owns.
/// </summary>
/// <remarks>
///     Core's <c>DD</c> range is internal to Core and each backend owns its own prefix, so a
///     distinct <c>PPTX</c> prefix makes the ownership boundary self-evident in any manifest and
///     cannot collide with another package's range. Internal because the codes are a published
///     output value, not an API consumers program against. A code number is never reused once
///     published, so the set is stable rather than contiguous: <c>PPTX0002</c> is retired and
///     permanently reserved — it announced that a deck carries no speaker notes, which is a fact
///     about the document rather than about the extraction, and is now reported as a counted zero in
///     the content outline. A table-pinning test treats the surviving set as a contract. All members
///     are constants and thread-safe.
/// </remarks>
internal static class PowerPointDiagnosticCodes
{
    /// <summary>The presentation contains no slides, so no content could be produced.</summary>
    /// <remarks>Accompanies the empty-deck gap so the absence is machine-detectable.</remarks>
    internal const string NoSlides = "PPTX0001";

    // PPTX0002 is retired and must never be reused: it reported that a deck carries no speaker
    // notes, a judgement about the deck's content rather than a statement about what DocDown could
    // do. The content outline now carries "0 sets of speaker notes" instead.

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

    /// <summary>The deck embeds charts whose plotted data this backend does not read.</summary>
    /// <remarks>
    ///     Accompanies the counted gap naming the charts. A chart on a slide stores its plotted values
    ///     in a DrawingML chart part that carries neither text body nor image blip, so without this the
    ///     chart would leave no trace whatever in the output while the summary still claimed a complete
    ///     extraction — the exact silent-loss failure the output contract exists to prevent.
    /// </remarks>
    internal const string ChartsNotExtracted = "PPTX0005";
}
