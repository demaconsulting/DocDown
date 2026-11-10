using System.Collections.Frozen;
using System.Globalization;

namespace DemaConsulting.DocDown.Word.OpenXml;

/// <summary>
///     The outcome of resolving a Word symbol character to Unicode text.
/// </summary>
/// <param name="Text">
///     The Unicode text to emit: the mapped character, or <see cref="WordSymbolMap.Placeholder"/>
///     when the symbol could not be mapped.
/// </param>
/// <param name="Mapped">
///     <see langword="true"/> when <paramref name="Text"/> is a verified mapping;
///     <see langword="false"/> when it is the placeholder.
/// </param>
/// <remarks>Immutable value type; thread-safe.</remarks>
internal readonly record struct WordSymbolResult(string Text, bool Mapped);

/// <summary>
///     Maps a Word <c>w:sym</c> (symbol character) font and code to visible Unicode text.
/// </summary>
/// <remarks>
///     A <c>w:sym</c> names a glyph in a symbol-encoded font such as Symbol or Wingdings; the
///     character code is a font code point, not Unicode, so it cannot be emitted as-is. This map
///     translates only the entries confirmed against published sources: for Symbol the Unicode
///     Consortium's Adobe Symbol Encoding table (<c>symbol.txt</c>, excluding its Corporate Use
///     Subarea assignments), and for Wingdings the Unicode Wingdings/Webdings mapping
///     (UTC L2/11-052R2). Anything else — an unrecognized font, a malformed or unlisted code — is
///     never guessed at; it resolves to <see cref="Placeholder"/> and is reported as unmapped.
///     The Wingdings 2, Wingdings 3, and Webdings fonts are recognized but currently carry no
///     verified entries, so every code in them is unmapped. All state is static and read-only;
///     the class is thread-safe.
/// </remarks>
internal static class WordSymbolMap
{
    /// <summary>The text emitted for a symbol that could not be mapped (U+FFFD REPLACEMENT CHARACTER).</summary>
    public const string Placeholder = "\uFFFD";

    /// <summary>The offset Word adds to a symbol code when it stores it in the Private Use Area.</summary>
    private const int PrivateUseOffset = 0xF000;

    /// <summary>The Symbol font table: font code to Unicode scalar, from Adobe symbol.txt (non-CUS entries).</summary>
    private static readonly FrozenDictionary<byte, string> Symbol = Build(
    [
        (0x20, 0x0020), (0x21, 0x0021), (0x22, 0x2200), (0x23, 0x0023), (0x24, 0x2203), (0x25, 0x0025),
        (0x26, 0x0026), (0x27, 0x220B), (0x28, 0x0028), (0x29, 0x0029), (0x2A, 0x2217), (0x2B, 0x002B),
        (0x2C, 0x002C), (0x2D, 0x2212), (0x2E, 0x002E), (0x2F, 0x002F), (0x30, 0x0030), (0x31, 0x0031),
        (0x32, 0x0032), (0x33, 0x0033), (0x34, 0x0034), (0x35, 0x0035), (0x36, 0x0036), (0x37, 0x0037),
        (0x38, 0x0038), (0x39, 0x0039), (0x3A, 0x003A), (0x3B, 0x003B), (0x3C, 0x003C), (0x3D, 0x003D),
        (0x3E, 0x003E), (0x3F, 0x003F), (0x40, 0x2245), (0x41, 0x0391), (0x42, 0x0392), (0x43, 0x03A7),
        (0x44, 0x0394), (0x45, 0x0395), (0x46, 0x03A6), (0x47, 0x0393), (0x48, 0x0397), (0x49, 0x0399),
        (0x4A, 0x03D1), (0x4B, 0x039A), (0x4C, 0x039B), (0x4D, 0x039C), (0x4E, 0x039D), (0x4F, 0x039F),
        (0x50, 0x03A0), (0x51, 0x0398), (0x52, 0x03A1), (0x53, 0x03A3), (0x54, 0x03A4), (0x55, 0x03A5),
        (0x56, 0x03C2), (0x57, 0x03A9), (0x58, 0x039E), (0x59, 0x03A8), (0x5A, 0x0396), (0x5B, 0x005B),
        (0x5C, 0x2234), (0x5D, 0x005D), (0x5E, 0x22A5), (0x5F, 0x005F), (0x61, 0x03B1), (0x62, 0x03B2),
        (0x63, 0x03C7), (0x64, 0x03B4), (0x65, 0x03B5), (0x66, 0x03C6), (0x67, 0x03B3), (0x68, 0x03B7),
        (0x69, 0x03B9), (0x6A, 0x03D5), (0x6B, 0x03BA), (0x6C, 0x03BB), (0x6D, 0x03BC), (0x6E, 0x03BD),
        (0x6F, 0x03BF), (0x70, 0x03C0), (0x71, 0x03B8), (0x72, 0x03C1), (0x73, 0x03C3), (0x74, 0x03C4),
        (0x75, 0x03C5), (0x76, 0x03D6), (0x77, 0x03C9), (0x78, 0x03BE), (0x79, 0x03C8), (0x7A, 0x03B6),
        (0x7B, 0x007B), (0x7C, 0x007C), (0x7D, 0x007D), (0x7E, 0x223C), (0xA0, 0x20AC), (0xA1, 0x03D2),
        (0xA2, 0x2032), (0xA3, 0x2264), (0xA4, 0x2044), (0xA5, 0x221E), (0xA6, 0x0192), (0xA7, 0x2663),
        (0xA8, 0x2666), (0xA9, 0x2665), (0xAA, 0x2660), (0xAB, 0x2194), (0xAC, 0x2190), (0xAD, 0x2191),
        (0xAE, 0x2192), (0xAF, 0x2193), (0xB0, 0x00B0), (0xB1, 0x00B1), (0xB2, 0x2033), (0xB3, 0x2265),
        (0xB4, 0x00D7), (0xB5, 0x221D), (0xB6, 0x2202), (0xB7, 0x2022), (0xB8, 0x00F7), (0xB9, 0x2260),
        (0xBA, 0x2261), (0xBB, 0x2248), (0xBC, 0x2026), (0xBF, 0x21B5), (0xC0, 0x2135), (0xC1, 0x2111),
        (0xC2, 0x211C), (0xC3, 0x2118), (0xC4, 0x2297), (0xC5, 0x2295), (0xC6, 0x2205), (0xC7, 0x2229),
        (0xC8, 0x222A), (0xC9, 0x2283), (0xCA, 0x2287), (0xCB, 0x2284), (0xCC, 0x2282), (0xCD, 0x2286),
        (0xCE, 0x2208), (0xCF, 0x2209), (0xD0, 0x2220), (0xD1, 0x2207), (0xD5, 0x220F), (0xD6, 0x221A),
        (0xD7, 0x22C5), (0xD8, 0x00AC), (0xD9, 0x2227), (0xDA, 0x2228), (0xDB, 0x21D4), (0xDC, 0x21D0),
        (0xDD, 0x21D1), (0xDE, 0x21D2), (0xDF, 0x21D3), (0xE0, 0x25CA), (0xE1, 0x2329), (0xE5, 0x2211),
        (0xF1, 0x232A), (0xF2, 0x222B), (0xF3, 0x2320), (0xF5, 0x2321)
    ]);

    /// <summary>
    ///     The Wingdings table: font code to Unicode scalar, from the Unicode Wingdings mapping.
    ///     Only the commonly used bullet, check, and box glyphs are listed.
    /// </summary>
    private static readonly FrozenDictionary<byte, string> Wingdings = Build(
    [
        (0x6C, 0x26AB), // BLACK CIRCLE (MEDIUM)
        (0x6E, 0x25FC), // BLACK MEDIUM SQUARE
        (0x71, 0x2751), // LOWER RIGHT SHADOWED WHITE SQUARE
        (0x72, 0x2752), // UPPER RIGHT SHADOWED WHITE SQUARE
        (0x76, 0x2756), // BLACK DIAMOND MINUS WHITE X
        (0x9E, 0x2219), // BULLET OPERATOR
        (0x9F, 0x2022), // BULLET
        (0xA7, 0x25AA), // BLACK SMALL SQUARE
        (0xD8, 0x2B9A), // THREE-D TOP-LIGHTED RIGHTWARDS EQUILATERAL ARROWHEAD
        (0xFB, 0x1F5F6), // BALLOT X
        (0xFC, 0x2713), // CHECK MARK
        (0xFD, 0x1F5F7), // BALLOT BOX WITH BOLD SCRIPT X
        (0xFE, 0x1F5F9) // BALLOT BOX WITH BOLD CHECK
    ]);

    /// <summary>The recognized fonts that currently carry no verified entries.</summary>
    private static readonly FrozenDictionary<byte, string> Empty = Build([]);

    /// <summary>The recognized symbol fonts by case-insensitive name.</summary>
    private static readonly FrozenDictionary<string, FrozenDictionary<byte, string>> Fonts =
        new Dictionary<string, FrozenDictionary<byte, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Symbol"] = Symbol,
            ["Wingdings"] = Wingdings,
            ["Wingdings 2"] = Empty,
            ["Wingdings 3"] = Empty,
            ["Webdings"] = Empty
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Resolves a symbol font and character code to Unicode text.
    /// </summary>
    /// <param name="font">The <c>w:font</c> attribute, or <see langword="null"/>.</param>
    /// <param name="charHex">The <c>w:char</c> attribute (hexadecimal), or <see langword="null"/>.</param>
    /// <returns>
    ///     The mapped text, or <see cref="Placeholder"/> flagged as not mapped when the font is not
    ///     recognized, the code is malformed or out of range, or the font has no verified entry.
    /// </returns>
    /// <remarks>
    ///     Codes in the Private Use Area (<c>F000</c>–<c>F0FF</c>) and bare codes (<c>0020</c>–<c>00FF</c>)
    ///     address the same font code point. Pure.
    /// </remarks>
    public static WordSymbolResult Resolve(string? font, string? charHex)
    {
        var unmapped = new WordSymbolResult(Placeholder, false);
        if (font is null
            || charHex is null
            || !int.TryParse(charHex.Trim(), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var code))
        {
            return unmapped;
        }

        if (code is >= PrivateUseOffset and <= PrivateUseOffset + 0xFF)
        {
            code -= PrivateUseOffset;
        }

        if (code is < 0x20 or > 0xFF
            || !Fonts.TryGetValue(font.Trim(), out var table)
            || !table.TryGetValue((byte)code, out var text))
        {
            return unmapped;
        }

        return new WordSymbolResult(text, true);
    }

    /// <summary>
    ///     Builds a font table from code and Unicode scalar pairs.
    /// </summary>
    /// <param name="entries">The (font code, Unicode scalar) pairs.</param>
    /// <returns>The frozen table.</returns>
    /// <remarks>Pure.</remarks>
    private static FrozenDictionary<byte, string> Build((byte Code, int Scalar)[] entries) =>
        entries.ToFrozenDictionary(entry => entry.Code, entry => char.ConvertFromUtf32(entry.Scalar));
}
