using DocDown.Pdf;

namespace DemaConsulting.DocDown.Pdf.Tests;

/// <summary>
///     Unit tests for the PDF <c>D:</c> date parsing in <see cref="PdfDocumentExtractor"/>, proving
///     well-formed dates (with and without offsets) normalize to ISO-8601 UTC and that malformed or
///     impossible values are omitted rather than guessed.
/// </summary>
public class PdfDateParsingTests
{
    /// <summary>
    ///     Proves well-formed PDF dates parse to the expected ISO-8601 UTC instant, covering the
    ///     <c>D:</c> prefix, defaulted fields, and <c>Z</c>, <c>+</c>, and <c>-</c> offsets.
    /// </summary>
    /// <param name="raw">The raw PDF date string.</param>
    /// <param name="expected">The expected ISO-8601 UTC result.</param>
    [Theory]
    [InlineData("D:20260707120000+00'00'", "2026-07-07T12:00:00Z")]
    [InlineData("D:20260707120000Z", "2026-07-07T12:00:00Z")]
    [InlineData("D:20260707120000-05'00'", "2026-07-07T17:00:00Z")]
    [InlineData("D:20260707120000+0530", "2026-07-07T06:30:00Z")]
    [InlineData("D:20260707", "2026-07-07T00:00:00Z")]
    [InlineData("D:2026", "2026-01-01T00:00:00Z")]
    [InlineData("20260707120000", "2026-07-07T12:00:00Z")]
    public void PdfDocumentExtractor_TryParsePdfDate_WellFormed_ParsesToIsoUtc(string raw, string expected)
    {
        Assert.True(PdfDocumentExtractor.TryParsePdfDate(raw, out var iso));
        Assert.Equal(expected, iso);
    }

    /// <summary>
    ///     Proves malformed, impossible, or empty values are rejected so the caller omits the field
    ///     rather than guessing a date.
    /// </summary>
    /// <param name="raw">The raw value that must not parse.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("D:garbage")]
    [InlineData("D:2026XX07")]
    [InlineData("D:202613")]
    [InlineData("D:20260732")]
    [InlineData("D:2026070712000")]
    [InlineData("D:20260707120000+99'00'")]
    public void PdfDocumentExtractor_TryParsePdfDate_Malformed_ReturnsFalse(string? raw)
    {
        Assert.False(PdfDocumentExtractor.TryParsePdfDate(raw, out var iso));
        Assert.Equal(string.Empty, iso);
    }
}
