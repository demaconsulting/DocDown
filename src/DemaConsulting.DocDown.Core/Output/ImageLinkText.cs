using System.Text;

namespace DocDown.Core;

/// <summary>
///     Produces the alt text for an inline markdown image link, applying the shared honesty and
///     escaping policy every Open XML backend uses.
/// </summary>
/// <remarks>
///     A backend links each embedded image at its point of occurrence in <c>content.md</c>, so the
///     alt text must read honestly and must never break the link syntax. This helper centralizes both
///     rules: a neutral <c>image</c> placeholder is used when the document offered no descriptive
///     source — rather than implying a description it never gave — and the link-structural characters
///     are escaped so a descriptive alt cannot terminate the bracket or parenthesis early. Mirrors the
///     Word backend's <c>AppendImage</c> alt handling so every format links images the same way.
///     Stateless, pure, and thread-safe.
/// </remarks>
public static class ImageLinkText
{
    /// <summary>The neutral placeholder used when the document offered no descriptive alt text.</summary>
    private const string Placeholder = "image";

    /// <summary>
    ///     Produces the alt text for an image link from an optional descriptive source.
    /// </summary>
    /// <param name="altText">The image's descriptive alt text, or <see langword="null"/> when none was chosen.</param>
    /// <returns>The escaped descriptive text, or the neutral <c>image</c> placeholder when none was given.</returns>
    /// <remarks>
    ///     Escapes the characters that would otherwise close the alt bracket, open or close the link
    ///     target, or introduce a line break, and collapses any newline to a space so the link stays
    ///     on one line. Pure.
    /// </remarks>
    public static string Alt(string? altText)
    {
        if (string.IsNullOrWhiteSpace(altText))
        {
            return Placeholder;
        }

        var builder = new StringBuilder(altText.Length);
        foreach (var character in altText)
        {
            switch (character)
            {
                case '\r':
                    break;
                case '\n':
                    builder.Append(' ');
                    break;
                case '[':
                case ']':
                case '(':
                case ')':
                case '\\':
                    builder.Append('\\').Append(character);
                    break;
                default:
                    builder.Append(character);
                    break;
            }
        }

        return builder.ToString();
    }
}
