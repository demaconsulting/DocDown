namespace DocDown.Excel.Markdown;

/// <summary>
///     The diagnostic codes this package owns.
/// </summary>
/// <remarks>
///     Core's <c>DD</c> range is internal to Core and each backend owns its own prefix, so a
///     distinct <c>XLSX</c> prefix makes the ownership boundary self-evident in any manifest and
///     cannot collide with another package's range. Internal because the codes are a published
///     output value, not an API consumers program against. The numbering is contiguous and stable —
///     a table-pinning test treats it as a contract. All members are constants and thread-safe.
/// </remarks>
internal static class ExcelDiagnosticCodes
{
    /// <summary>The workbook contains no worksheets, so no content could be produced.</summary>
    /// <remarks>Accompanies the empty-workbook gap so the absence is machine-detectable.</remarks>
    internal const string NoWorksheets = "XLSX0001";

    /// <summary>A worksheet carried no non-empty cells and produced an empty part.</summary>
    /// <remarks>Informational: an empty sheet is an expected authoring artifact, not a loss of content.</remarks>
    internal const string EmptySheet = "XLSX0002";

    /// <summary>Embedded images include EMF or WMF vector metafiles, written unchanged with a readability caveat.</summary>
    /// <remarks>
    ///     Informational, never a gap: the bytes are a complete image file and are written and counted
    ///     as extracted, and no better environment or configuration would yield more because this
    ///     backend ships no metafile rasterizer. The caveat that many viewers and image libraries
    ///     cannot render a Windows metafile is stated for the reader, but it must not degrade the run.
    /// </remarks>
    internal const string VectorImageWrittenAsIs = "XLSX0003";

    /// <summary>A chart was found whose cached data could not be read, so only the chart's existence could be reported.</summary>
    /// <remarks>
    ///     Accompanies the unreadable-chart gap. A chart part this backend cannot parse — a malformed
    ///     part, or the extended chart grammar — must still be named, because a chart that is dropped
    ///     without a word is exactly the failure the output contract forbids.
    /// </remarks>
    internal const string ChartUnreadable = "XLSX0004";

    /// <summary>A chart was read but carries no cached data points, so no data table could be produced.</summary>
    /// <remarks>
    ///     Accompanies the uncached-chart gap. A chart stores the values as last plotted; a chart saved
    ///     without that cache plots from formulas alone, which a consumer that cannot recalculate the
    ///     workbook cannot use, so the absence is a genuine loss and is reported as one.
    /// </remarks>
    internal const string ChartWithoutCachedData = "XLSX0005";

    /// <summary>A chart's cached data exceeded the rendering bound and was truncated.</summary>
    /// <remarks>
    ///     Accompanies the truncated-chart gap. The bound exists so one logged sweep cannot crowd out a
    ///     whole workbook; saying how many points were dropped keeps the truncation a stated choice
    ///     rather than a silent loss.
    /// </remarks>
    internal const string ChartPointsTruncated = "XLSX0006";
}
