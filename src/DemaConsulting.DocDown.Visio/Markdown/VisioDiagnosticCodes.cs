namespace DocDown.Visio.Markdown;

/// <summary>
///     The diagnostic codes this package owns.
/// </summary>
/// <remarks>
///     Core's <c>DD</c> range is internal to Core and each backend owns its own prefix, so a
///     distinct <c>VISIO</c> prefix makes the ownership boundary self-evident in any manifest and
///     cannot collide with another package's range. Internal because the codes are a published
///     output value, not an API consumers program against. The numbering is contiguous and stable —
///     a table-pinning test treats it as a contract. All members are constants and thread-safe.
/// </remarks>
internal static class VisioDiagnosticCodes
{
    /// <summary>The drawing contains no pages, so no content could be produced.</summary>
    /// <remarks>Accompanies the empty-drawing gap so the absence is machine-detectable.</remarks>
    internal const string NoPages = "VISIO0001";

    /// <summary>A page carried no shapes with recoverable text and no connections.</summary>
    /// <remarks>Informational: an empty page is an expected authoring artifact, not a loss of content.</remarks>
    internal const string EmptyPage = "VISIO0002";

    /// <summary>Embedded images include EMF or WMF vector metafiles, written unchanged with a readability caveat.</summary>
    /// <remarks>
    ///     Informational, never a gap: the bytes are a complete image file and are written and counted
    ///     as extracted, and no better environment or configuration would yield more because this
    ///     backend ships no metafile rasterizer. The caveat that many viewers and image libraries
    ///     cannot render a Windows metafile is stated for the reader, but it must not degrade the run.
    /// </remarks>
    internal const string VectorImageWrittenAsIs = "VISIO0003";

    /// <summary>A page could not be rendered by the COM backend and was omitted.</summary>
    /// <remarks>Accompanies the counted pages gap naming the page; the run continued with the remaining pages.</remarks>
    internal const string PageRenderFailed = "VISIO0004";

    /// <summary>The convention by which a topology endpoint label is to be read.</summary>
    /// <remarks>
    ///     Published so the distinction between a shape's own text, its master type, and a bare
    ///     shape id survives machine reading of the manifest — a consumer that sees only the
    ///     diagnostics can still tell which endpoints anyone actually named.
    /// </remarks>
    internal const string TopologyLabelConvention = "VISIO0005";

    /// <summary>How many connector endpoints resolved by text, by master type, and not at all.</summary>
    /// <remarks>
    ///     Informational rather than a gap: an endpoint the drawing identifies only by id is a
    ///     property of the document, not a failure of extraction. The counts let a consumer judge
    ///     how readable the topology really is instead of taking the edge list on trust.
    /// </remarks>
    internal const string TopologyEndpointCoverage = "VISIO0006";
}
