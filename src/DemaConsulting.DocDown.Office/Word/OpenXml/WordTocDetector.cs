using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace DemaConsulting.DocDown.Word.OpenXml;

/// <summary>
///     The table-of-contents paragraphs found in a document body.
/// </summary>
/// <param name="Paragraphs">The body paragraphs that belong to a table of contents, compared by reference.</param>
/// <param name="Regions">The number of separate tables of contents (runs of adjacent contents paragraphs).</param>
/// <remarks>Immutable once built; thread-safe for reads.</remarks>
internal sealed record WordTocInfo(HashSet<OpenXmlElement> Paragraphs, int Regions)
{
    /// <summary>Gets an empty result for a document with no table of contents.</summary>
    public static WordTocInfo None => new(new HashSet<OpenXmlElement>(ReferenceEqualityComparer.Instance), 0);
}

/// <summary>
///     Finds the table-of-contents paragraphs of a Word body, so the reader can leave them out of
///     <c>content.md</c> on purpose and the reporting can say so.
/// </summary>
/// <remarks>
///     <para>
///         A table of contents is generated navigation: every entry repeats a heading that
///         <c>content.md</c> already carries, with a page number that means nothing in markdown.
///         Rendering it, and especially its tracked changes, would add noise without information, so
///         it is omitted deliberately and reported (count and tracked changes inside it) instead of
///         disappearing silently.
///     </para>
///     <para>
///         Three structural signals identify it, never rendered text: a block content control whose
///         document-part gallery is "Table of Contents", a paragraph whose style is <c>TOC1</c> to
///         <c>TOCn</c> (by style id or by the style's name <c>toc n</c>), and a paragraph inside a
///         <c>TOC</c> complex or simple field. Stateless and thread-safe.
///     </para>
/// </remarks>
internal static class WordTocDetector
{
    /// <summary>Matches a table-of-contents style id or name such as <c>TOC1</c> or <c>toc 2</c>.</summary>
    private static readonly Regex TocStyleName =
        new(@"^toc\s*\d+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    /// <summary>The document-part gallery name Word gives a built-in table of contents.</summary>
    private const string TocGallery = "Table of Contents";

    /// <summary>
    ///     Finds the table-of-contents paragraphs in a document body.
    /// </summary>
    /// <param name="mainPart">The main document part. Must not be null.</param>
    /// <returns>The paragraphs and region count; empty when the document has none.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mainPart"/> is <see langword="null"/>.</exception>
    /// <remarks>Read-only over the package.</remarks>
    public static WordTocInfo Find(MainDocumentPart mainPart)
    {
        ArgumentNullException.ThrowIfNull(mainPart);
        if (mainPart.Document?.Body is not { } body)
        {
            return WordTocInfo.None;
        }

        var styleIds = TocStyleIds(mainPart);
        var found = new HashSet<OpenXmlElement>(ReferenceEqualityComparer.Instance);
        var fields = new Stack<FieldState>();
        var regions = 0;
        var previousWasToc = false;

        foreach (var paragraph in body.Descendants<W.Paragraph>())
        {
            var styleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
            var styled = styleId is not null && styleIds.Contains(styleId);

            // A heading cannot be a contents entry, so meeting one while a contents field is still
            // open means the field was never closed; stop treating the rest as contents
            if (!styled && fields.Any(field => field.IsToc) && WordOpenXmlReader.DetermineHeadingLevel(paragraph) > 0
                && !paragraph.Ancestors<W.SdtBlock>().Any(IsTocSdt))
            {
                fields.Clear();
            }

            var inField = TrackFields(paragraph, fields);
            var isToc = styled || inField || paragraph.Ancestors<W.SdtBlock>().Any(IsTocSdt);
            if (isToc)
            {
                found.Add(paragraph);
                if (!previousWasToc)
                {
                    regions++;
                }
            }

            previousWasToc = isToc;
        }

        return new WordTocInfo(found, regions);
    }

    /// <summary>
    ///     Determines whether a block content control is a built-in table of contents.
    /// </summary>
    /// <param name="sdt">The content control.</param>
    /// <returns><see langword="true"/> when its document-part gallery is "Table of Contents".</returns>
    /// <remarks>Pure.</remarks>
    public static bool IsTocSdt(W.SdtBlock sdt)
    {
        ArgumentNullException.ThrowIfNull(sdt);
        return sdt.SdtProperties?.Descendants<W.DocPartGallery>()
            .Any(gallery => string.Equals(gallery.Val?.Value, TocGallery, StringComparison.OrdinalIgnoreCase)) == true;
    }

    /// <summary>
    ///     Collects the ids of the table-of-contents paragraph styles.
    /// </summary>
    /// <param name="mainPart">The main document part.</param>
    /// <returns>The style ids, matched by id or by style name, compared case-insensitively.</returns>
    /// <remarks>Read-only.</remarks>
    private static HashSet<string> TocStyleIds(MainDocumentPart mainPart)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var styles = mainPart.StyleDefinitionsPart?.Styles?.Elements<W.Style>();
        if (styles is not null)
        {
            foreach (var style in styles)
            {
                var id = style.StyleId?.Value;
                var name = style.StyleName?.Val?.Value;
                if (id is not null && name is not null && TocStyleName.IsMatch(name))
                {
                    ids.Add(id);
                }
            }
        }

        // The built-in ids are recognized even when the document carries no style definitions
        for (var level = 1; level <= 9; level++)
        {
            ids.Add($"TOC{level}");
        }

        return ids;
    }

    /// <summary>
    ///     Advances the open-field state through one paragraph.
    /// </summary>
    /// <param name="paragraph">The paragraph.</param>
    /// <param name="fields">The open complex fields, carried across paragraphs.</param>
    /// <returns><see langword="true"/> when any part of the paragraph lies inside a <c>TOC</c> field.</returns>
    /// <remarks>A contents field spans many paragraphs, so the stack outlives the paragraph. Side effect: mutates <paramref name="fields"/>.</remarks>
    private static bool TrackFields(W.Paragraph paragraph, Stack<FieldState> fields)
    {
        var inside = fields.Any(field => field.IsToc);
        foreach (var node in paragraph.Descendants())
        {
            switch (node)
            {
                case W.FieldChar fieldChar when fieldChar.FieldCharType?.Value == W.FieldCharValues.Begin:
                    fields.Push(new FieldState());
                    break;

                case W.FieldChar fieldChar when fieldChar.FieldCharType?.Value == W.FieldCharValues.End && fields.Count > 0:
                    fields.Pop();
                    break;

                case W.FieldCode or W.DeletedFieldCode when fields.Count > 0:
                    var frame = fields.Peek();
                    frame.Instruction.Append(node.InnerText);
                    if (IsTocInstruction(frame.Instruction.ToString()))
                    {
                        frame.IsToc = true;
                        inside = true;
                    }

                    break;

                case W.SimpleField simple when IsTocInstruction(simple.Instruction?.Value):
                    inside = true;
                    break;

                default:
                    break;
            }
        }

        return inside || fields.Any(field => field.IsToc);
    }

    /// <summary>
    ///     Determines whether a field instruction is a contents field.
    /// </summary>
    /// <param name="instruction">The instruction text, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when its first token is <c>TOC</c>.</returns>
    /// <remarks>Structural, so <c>TOCHEADING</c> or a cross-reference to a heading is not matched. Pure.</remarks>
    private static bool IsTocInstruction(string? instruction)
    {
        var token = instruction?.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return string.Equals(token, "TOC", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The state of one open complex field.</summary>
    private sealed class FieldState
    {
        /// <summary>Gets the accumulated instruction text.</summary>
        public StringBuilder Instruction { get; } = new();

        /// <summary>Gets or sets a value indicating whether the instruction is a contents field.</summary>
        public bool IsToc { get; set; }
    }
}
