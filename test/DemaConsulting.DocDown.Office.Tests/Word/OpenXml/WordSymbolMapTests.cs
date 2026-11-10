using DemaConsulting.DocDown.Word.OpenXml;

namespace DemaConsulting.DocDown.Office.Tests.Word.OpenXml;

/// <summary>
///     Unit tests for <see cref="WordSymbolMap"/>, which resolves symbol-font codes to Unicode text.
/// </summary>
public class WordSymbolMapTests
{
    /// <summary>
    ///     Proves the Symbol bullet code resolves to U+2022.
    /// </summary>
    [Fact]
    public void WordSymbolMap_Resolve_SymbolBullet_MapsToBullet()
    {
        var result = WordSymbolMap.Resolve("Symbol", "F0B7");

        Assert.True(result.Mapped);
        Assert.Equal("\u2022", result.Text);
    }

    /// <summary>
    ///     Proves verified Wingdings check and box codes resolve to their Unicode mappings.
    /// </summary>
    /// <param name="code">The Wingdings code.</param>
    /// <param name="expected">The expected Unicode text.</param>
    [Theory]
    [InlineData("F0FC", "\u2713")]
    [InlineData("F0FE", "\U0001F5F9")]
    [InlineData("F0FD", "\U0001F5F7")]
    [InlineData("F0FB", "\U0001F5F6")]
    [InlineData("F0A7", "\u25AA")]
    public void WordSymbolMap_Resolve_WingdingsCodes_MapToUnicode(string code, string expected)
    {
        var result = WordSymbolMap.Resolve("Wingdings", code);

        Assert.True(result.Mapped);
        Assert.Equal(expected, result.Text);
    }

    /// <summary>
    ///     Proves the font name is matched without regard to case or surrounding whitespace.
    /// </summary>
    [Fact]
    public void WordSymbolMap_Resolve_FontNameCaseAndWhitespace_Ignored()
    {
        var result = WordSymbolMap.Resolve("  sYmBoL ", "F0B7");

        Assert.True(result.Mapped);
        Assert.Equal("\u2022", result.Text);
    }

    /// <summary>
    ///     Proves a Private Use Area code and the bare code address the same glyph, in either hex case.
    /// </summary>
    [Fact]
    public void WordSymbolMap_Resolve_PrivateUseAndBareCodes_AreEquivalent()
    {
        Assert.Equal(WordSymbolMap.Resolve("Symbol", "F0B7"), WordSymbolMap.Resolve("Symbol", "00B7"));
        Assert.Equal(WordSymbolMap.Resolve("Wingdings", "F0fc"), WordSymbolMap.Resolve("Wingdings", "00FC"));
    }

    /// <summary>
    ///     Proves unrecognized fonts, malformed or out-of-range codes, and unlisted codes resolve to
    ///     the placeholder and are flagged as unmapped.
    /// </summary>
    /// <param name="font">The font name.</param>
    /// <param name="code">The character code.</param>
    [Theory]
    [InlineData("Invented Glyphs", "F0B7")]
    [InlineData("Symbol", "ZZZZ")]
    [InlineData("Symbol", "")]
    [InlineData("Symbol", "F1B7")]
    [InlineData("Symbol", "F010")]
    [InlineData("Symbol", "0010")]
    [InlineData("Symbol", "F0FF")]
    [InlineData("Wingdings", "F0FF")]
    [InlineData("Wingdings 2", "F0B7")]
    [InlineData("Webdings", "F0B7")]
    [InlineData(null, "F0B7")]
    [InlineData("Symbol", null)]
    public void WordSymbolMap_Resolve_UnmappableInput_ReturnsPlaceholder(string? font, string? code)
    {
        var result = WordSymbolMap.Resolve(font, code);

        Assert.False(result.Mapped);
        Assert.Equal(WordSymbolMap.Placeholder, result.Text);
        Assert.Equal("\uFFFD", result.Text);
    }
}
