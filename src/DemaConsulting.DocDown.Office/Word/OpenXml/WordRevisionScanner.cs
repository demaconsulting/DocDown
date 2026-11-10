using System.Globalization;
using System.Text;
using DemaConsulting.DocDown.Word.Markdown;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace DemaConsulting.DocDown.Word.OpenXml;

/// <summary>
///     Finds every tracked change a Word document carries, so the reader and the reporting agree on
///     one authoritative count, author set, date range, and per-change location.
/// </summary>
/// <remarks>
///     <para>
///         Counting is done in a dedicated pre-pass rather than inside the content walk because the
///         content walk visits some paragraphs more than once (captions, comment anchors), which would
///         double-count. The scan covers the body, headers, footers, and footnotes; it deliberately
///         ignores the <c>trackRevisions</c> setting, which records whether tracking is switched on
///         and says nothing about whether revisions exist. Revisions inside a table of contents are
///         counted too and flagged, because the contents themselves are omitted from <c>content.md</c>.
///     </para>
///     <para>
///         Content revisions (insertions, deletions, moves, table row and cell insertions and
///         deletions, paragraph-mark insertions and deletions) and property changes (formatting,
///         section, table, numbering) are all counted.
///     </para>
///     <para>
///         Each revision's location is built for markdown consumers, never as a page number: the part
///         label, the heading path as <c>content.md</c> shows it (<c>Heading &gt; Subheading</c>), the
///         paragraph or table position under that heading, and a short snippet of the surrounding
///         text. The walk mirrors the reader's block rules (empty paragraphs produce no block, a
///         deleted paragraph mark merges into the next paragraph, deleted text is invisible in the
///         accepted view) so the ordinals agree with <c>content.md</c>; table row numbers match the
///         rendered table (deleted rows count in markup mode and are omitted in the accepted view) and
///         column numbers are Word's own. Header and footer labels are those <c>content.md</c> uses
///         for their sections. Stateless and thread-safe.
///     </para>
/// </remarks>
internal static class WordRevisionScanner
{
    /// <summary>The longest snippet of changed text recorded for one revision.</summary>
    private const int SnippetMaxLength = 200;

    /// <summary>The longest snippet of surrounding text quoted in a location.</summary>
    private const int ContextMaxLength = 40;

    /// <summary>The longest heading text quoted in a heading path.</summary>
    private const int HeadingMaxLength = 80;

    /// <summary>The WordprocessingML main namespace, for reading author and date attributes.</summary>
    private const string WordNamespace = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    /// <summary>
    ///     Scans a document for tracked changes.
    /// </summary>
    /// <param name="mainPart">The main document part. Must not be null.</param>
    /// <param name="markup">
    ///     <see langword="true"/> when deleted text is visible in <c>content.md</c> (markup mode, the
    ///     default); <see langword="false"/> for the accepted view, where it is not. Decides how
    ///     headings and paragraphs are recognized in the locations.
    /// </param>
    /// <param name="toc">The table-of-contents paragraphs, or <see langword="null"/> to detect them here.</param>
    /// <param name="partLabels"></param>
    /// <returns>The tracked changes in document order: body, then headers, footers, and footnotes.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mainPart"/> is <see langword="null"/>.</exception>
    /// <remarks>Read-only over the package.</remarks>
    public static IReadOnlyList<WordRevision> Scan(
        MainDocumentPart mainPart,
        bool markup = true,
        WordTocInfo? toc = null,
        IReadOnlyDictionary<string, string>? partLabels = null)
    {
        ArgumentNullException.ThrowIfNull(mainPart);
        var tocInfo = toc ?? WordTocDetector.Find(mainPart);
        var context = new ScanContext(markup, tocInfo, FootnoteOrdinals(mainPart, tocInfo, markup));

        if (mainPart.Document?.Body is { } body)
        {
            ScanContainer(body.ChildElements, new RootState(WordRevisionPart.Body, "Body", isBody: true), context);
        }

        foreach (var header in mainPart.HeaderParts)
        {
            if (header.Header is { } root)
            {
                var label = PartLabel(header, "Header", partLabels);
                ScanContainer(root.ChildElements, new RootState(WordRevisionPart.Header, label, isBody: false), context);
            }
        }

        foreach (var footer in mainPart.FooterParts)
        {
            if (footer.Footer is { } root)
            {
                var label = PartLabel(footer, "Footer", partLabels);
                ScanContainer(root.ChildElements, new RootState(WordRevisionPart.Footer, label, isBody: false), context);
            }
        }

        if (mainPart.FootnotesPart?.Footnotes is { } footnotes)
        {
            foreach (var footnote in footnotes.Elements<W.Footnote>())
            {
                var type = footnote.Type?.Value;
                if (type is not null && type != W.FootnoteEndnoteValues.Normal)
                {
                    continue;
                }

                var id = footnote.Id?.Value ?? 0;
                var label = context.FootnoteNumbers.TryGetValue(id, out var ordinal)
                    ? $"Footnote [^{ordinal.ToString(CultureInfo.InvariantCulture)}]"
                    : $"Footnote (id {id.ToString(CultureInfo.InvariantCulture)}, not referenced in the body)";
                ScanContainer(footnote.ChildElements, new RootState(WordRevisionPart.Footnote, label, isBody: false), context);
            }
        }

        return context.Revisions;
    }

    /// <summary>
    ///     Gets the label for a header or footer part in a location.
    /// </summary>
    /// <param name="part">The part.</param>
    /// <param name="kind">The kind label, <c>Header</c> or <c>Footer</c>.</param>
    /// <param name="partLabels">The labels content.md uses, keyed by part URI, or <see langword="null"/>.</param>
    /// <returns>
    ///     The label content.md shows for the part's section; when the part is not shown there (empty,
    ///     page furniture only, or unreferenced) or no labels were supplied, the kind with the part
    ///     file name.
    /// </returns>
    /// <remarks>Pure.</remarks>
    private static string PartLabel(OpenXmlPart part, string kind, IReadOnlyDictionary<string, string>? partLabels) =>
        partLabels is not null && partLabels.TryGetValue(part.Uri.OriginalString, out var label)
            ? label
            : $"{kind} ({PartName(part)}, not shown in content.md)";

    /// <summary>
    ///     Gets the file name of a part, for naming a header or footer in a location.
    /// </summary>
    /// <param name="part">The part.</param>
    /// <returns>The last segment of the part's URI.</returns>
    /// <remarks>Pure.</remarks>
    private static string PartName(OpenXmlPart part) =>
        part.Uri.OriginalString[(part.Uri.OriginalString.LastIndexOf('/') + 1)..];

    /// <summary>
    ///     Numbers the footnotes the way <c>content.md</c> does: by reference order in the body.
    /// </summary>
    /// <param name="mainPart">The main document part.</param>
    /// <param name="toc">The table-of-contents paragraphs, whose references are not rendered.</param>
    /// <param name="markup">
    ///     Whether the markup view is scanned. In the accepted view the reader drops deleted and
    ///     moved-from runs, so references inside them are not rendered and do not take a number.
    /// </param>
    /// <returns>The map from footnote id to the number in its <c>[^n]</c> marker.</returns>
    /// <remarks>Pure over the package.</remarks>
    private static Dictionary<long, int> FootnoteOrdinals(MainDocumentPart mainPart, WordTocInfo toc, bool markup)
    {
        var ordinals = new Dictionary<long, int>();
        var known = mainPart.FootnotesPart?.Footnotes?.Elements<W.Footnote>()
            .Where(footnote => footnote.Id?.Value is > 0)
            .Select(footnote => footnote.Id!.Value)
            .ToHashSet() ?? [];
        if (known.Count == 0 || mainPart.Document?.Body is not { } body)
        {
            return ordinals;
        }

        var next = 0;
        foreach (var reference in body.Descendants<W.FootnoteReference>())
        {
            var id = reference.Id?.Value;
            if (id is null || !known.Contains(id.Value) || reference.Ancestors<W.Paragraph>().Any(toc.Paragraphs.Contains))
            {
                continue;
            }

            if (!markup && reference.Ancestors().Any(ancestor => ancestor is W.DeletedRun or W.MoveFromRun))
            {
                continue;
            }

            next++;
            ordinals.TryAdd(id.Value, next);
        }

        return ordinals;
    }

    /// <summary>
    ///     Scans the block-level children of a root, tracking position as the reader numbers blocks.
    /// </summary>
    /// <param name="children">The block-level children.</param>
    /// <param name="state">The position state of the root being scanned.</param>
    /// <param name="context">The shared scan context.</param>
    /// <remarks>Side effect: appends to the context's revisions and updates <paramref name="state"/>.</remarks>
    private static void ScanContainer(IEnumerable<OpenXmlElement> children, RootState state, ScanContext context)
    {
        foreach (var child in children)
        {
            switch (child)
            {
                case W.Paragraph paragraph:
                    ScanParagraph(paragraph, state, context);
                    break;

                case W.Table table:
                    ScanTable(table, state, context);
                    break;

                case W.SdtBlock sdt:
                    foreach (var element in sdt.SdtProperties?.Descendants() ?? [])
                    {
                        AddIfRevision(element, Compose(state, null, null), state, context, inToc: false);
                    }

                    if (sdt.SdtContentBlock is { } content)
                    {
                        ScanContainer(content.ChildElements, state, context);
                    }

                    break;

                case W.CustomXmlBlock custom:
                    ScanContainer(custom.ChildElements, state, context);
                    break;

                default:
                    var where = child is W.SectionProperties
                        ? "end of document (section properties)"
                        : AfterParagraph(state);
                    var location = Compose(state, where, null);
                    foreach (var element in child.Descendants())
                    {
                        AddIfRevision(element, location, state, context, inToc: false);
                    }

                    break;
            }
        }
    }

    /// <summary>
    ///     Scans one paragraph, updating the heading path and paragraph ordinal as the reader would.
    /// </summary>
    /// <param name="paragraph">The paragraph.</param>
    /// <param name="state">The position state of the root.</param>
    /// <param name="context">The shared scan context.</param>
    /// <remarks>
    ///     A table-of-contents paragraph leaves the path and ordinal alone and is numbered on its own.
    ///     A paragraph whose mark is deleted merges into the next one, so it takes the ordinal the
    ///     merged paragraph will have. Side effect: appends revisions and updates <paramref name="state"/>.
    /// </remarks>
    private static void ScanParagraph(W.Paragraph paragraph, RootState state, ScanContext context)
    {
        var text = TextOf(paragraph, context.Markup, IsFurnitureRoot(state));
        var inToc = state.IsBody && context.Toc.Paragraphs.Contains(paragraph);
        string location;

        if (inToc)
        {
            state.TocEntries++;
            var entry = $"table of contents (omitted from content.md), entry {state.TocEntries.ToString(CultureInfo.InvariantCulture)}";
            location = Compose(state, entry, text, includePath: false);
        }
        else
        {
            var combined = state.CarryText + text;
            var hasText = !string.IsNullOrWhiteSpace(combined);
            if (HasDeletedMark(paragraph))
            {
                state.CarryText = combined;
                var merged = $"paragraph {(state.ParagraphCount + 1).ToString(CultureInfo.InvariantCulture)}";
                location = Compose(state, hasText ? merged : AfterParagraph(state), text);
            }
            else
            {
                state.CarryText = string.Empty;
                var level = state.IsBody && hasText ? WordOpenXmlReader.DetermineHeadingLevel(paragraph) : 0;
                if (level > 0)
                {
                    PushHeading(state, level, Collapse(combined));
                    location = Compose(state, null, null) + " (heading)";
                }
                else if (hasText)
                {
                    state.ParagraphCount++;
                    location = Compose(state, $"paragraph {state.ParagraphCount.ToString(CultureInfo.InvariantCulture)}", text);
                }
                else
                {
                    location = Compose(state, AfterParagraph(state), null);
                }
            }
        }

        foreach (var element in paragraph.Descendants())
        {
            AddIfRevision(element, location, state, context, inToc);
        }
    }

    /// <summary>
    ///     Scans one top-level table, locating each revision by table, row, and column.
    /// </summary>
    /// <param name="table">The table.</param>
    /// <param name="state">The position state of the root.</param>
    /// <param name="context">The shared scan context.</param>
    /// <remarks>
    ///     Tables are numbered per heading, skipping those the reader omits as empty. Row numbers
    ///     match the table as rendered in <c>content.md</c> (deleted rows count in markup mode and are
    ///     omitted in the accepted view); column numbers are Word's (nested tables are located by their outermost cell, matching how
    ///     <c>content.md</c> flattens them). Side effect: appends revisions.
    /// </remarks>
    private static void ScanTable(W.Table table, RootState state, ScanContext context)
    {
        // The reader flushes carried paragraph content before a table, so it stands alone as its own
        // paragraph rather than merging into the paragraph after the table
        if (!string.IsNullOrWhiteSpace(state.CarryText))
        {
            state.ParagraphCount++;
        }

        state.CarryText = string.Empty;

        // Decide presence from the cells the reader keeps: in the accepted view that excludes
        // tracked-deleted rows and cells, exactly as BuildTable does
        var visibleCells = table.Elements<W.TableRow>()
            .Where(row => context.Markup || !IsDeletedRow(row))
            .SelectMany(row => row.Elements<W.TableCell>())
            .Where(cell => context.Markup || cell.TableCellProperties?.GetFirstChild<W.CellDeletion>() is null)
            .ToList();
        // The reader keeps only textual cell content (BuildTable ignores images), so an image-only
        // table is omitted from content.md and must not consume an ordinal here either
        var hasContent = visibleCells.Any(cell => !string.IsNullOrWhiteSpace(TextOf(cell, context.Markup, IsFurnitureRoot(state))));
        string tableNumber;
        if (hasContent)
        {
            state.TableCount++;
            tableNumber = $"table {state.TableCount.ToString(CultureInfo.InvariantCulture)}";
        }
        else
        {
            tableNumber = "empty table (omitted from content.md)";
        }

        foreach (var element in table.Descendants())
        {
            if (Classify(element) is null)
            {
                continue;
            }

            var row = element.Ancestors<W.TableRow>().FirstOrDefault(candidate => ReferenceEquals(candidate.Parent, table));
            var location = Compose(state, tableNumber, null);
            if (row is not null)
            {
                // Number rows as content.md renders them: every row in markup mode, but without
                // tracked-deleted rows in the accepted view
                var renderedRows = table.Elements<W.TableRow>()
                    .Where(candidate => context.Markup || !IsDeletedRow(candidate))
                    .ToList();
                var rowIndex = renderedRows.IndexOf(row);
                var cell = element.Ancestors<W.TableCell>().FirstOrDefault(candidate => ReferenceEquals(candidate.Parent, row));
                string position;
                string? quoted = TextOf(row, context.Markup, IsFurnitureRoot(state));
                if (rowIndex < 0)
                {
                    // A deleted row has no rendered number in the accepted view: place it by the
                    // rendered rows that precede it
                    var preceding = table.Elements<W.TableRow>()
                        .TakeWhile(candidate => !ReferenceEquals(candidate, row))
                        .Count(candidate => !IsDeletedRow(candidate));
                    position = $"{tableNumber}, deleted row (omitted from the accepted view), after row "
                        + preceding.ToString(CultureInfo.InvariantCulture);
                    quoted = null;
                }
                else
                {
                    position = $"{tableNumber}, row {(rowIndex + 1).ToString(CultureInfo.InvariantCulture)}";
                }

                if (cell is not null && rowIndex >= 0)
                {
                    var columnNumber = row.Elements<W.TableCell>().ToList().IndexOf(cell) + 1;
                    position += $", column {columnNumber.ToString(CultureInfo.InvariantCulture)}";
                    // In the accepted view a deleted cell renders empty, so its deleted text is not quoted
                    quoted = !context.Markup && cell.TableCellProperties?.GetFirstChild<W.CellDeletion>() is not null
                        ? null
                        : TextOf(cell, context.Markup, IsFurnitureRoot(state));
                }

                location = Compose(state, position, quoted);
            }

            AddIfRevision(element, location, state, context, inToc: false);
        }
    }

    /// <summary>
    ///     Determines whether a table row carries a tracked deletion.
    /// </summary>
    /// <param name="row">The row.</param>
    /// <returns><see langword="true"/> when the row is tracked-deleted.</returns>
    /// <remarks>Pure.</remarks>
    private static bool IsDeletedRow(W.TableRow row) =>
        row.TableRowProperties?.GetFirstChild<W.Deleted>() is not null;

    /// <summary>
    ///     Records a revision when the element is one.
    /// </summary>
    /// <param name="element">The candidate element.</param>
    /// <param name="location">The location text to record.</param>
    /// <param name="state">The root state, for the part.</param>
    /// <param name="context">The shared scan context.</param>
    /// <param name="inToc">Whether the element sits inside a table of contents.</param>
    /// <remarks>Side effect: appends to the context's revisions.</remarks>
    private static void AddIfRevision(
        OpenXmlElement element, string location, RootState state, ScanContext context, bool inToc)
    {
        var kind = Classify(element);
        if (kind is null)
        {
            return;
        }

        var snippet = kind is WordRevisionKind.Inserted or WordRevisionKind.Deleted
            or WordRevisionKind.MovedFrom or WordRevisionKind.MovedTo
            ? BuildSnippet(element)
            : null;
        context.Revisions.Add(new WordRevision(
            kind.Value,
            ReadAttribute(element, "author"),
            ParseDate(ReadAttribute(element, "date")),
            location,
            snippet,
            state.Part,
            inToc));
    }

    /// <summary>
    ///     Composes a location string from the root state and a position.
    /// </summary>
    /// <param name="state">The root state.</param>
    /// <param name="position">The position within the heading (for example <c>paragraph 2</c>), or <see langword="null"/>.</param>
    /// <param name="context">The surrounding text to quote, or <see langword="null"/> for none.</param>
    /// <param name="includePath">Whether to include the body heading path.</param>
    /// <returns>The location: <c>Label: path, position — "context"</c>.</returns>
    /// <remarks>Pure.</remarks>
    private static string Compose(RootState state, string? position, string? context, bool includePath = true)
    {
        var parts = new List<string>();
        if (state.IsBody && includePath)
        {
            parts.Add(state.Path.Count == 0
                ? "before the first heading"
                : string.Join(" > ", state.Path.Select(heading => Truncate(heading.Text, HeadingMaxLength))));
        }

        if (position is not null)
        {
            parts.Add(position);
        }

        var builder = new StringBuilder(state.Label);
        if (parts.Count > 0)
        {
            builder.Append(": ").Append(string.Join(", ", parts));
        }

        var quoted = Collapse(context);
        if (quoted.Length > 0)
        {
            builder.Append(" — \"").Append(Truncate(quoted, ContextMaxLength)).Append('"');
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Describes the gap after the last paragraph, for a change that sits in an empty paragraph.
    /// </summary>
    /// <param name="state">The root state.</param>
    /// <returns>The position text.</returns>
    /// <remarks>Pure.</remarks>
    private static string AfterParagraph(RootState state) =>
        state.ParagraphCount == 0
            ? "before the first paragraph"
            : $"after paragraph {state.ParagraphCount.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>
    ///     Pushes a heading onto the path, replacing any heading at the same or a deeper level.
    /// </summary>
    /// <param name="state">The root state.</param>
    /// <param name="level">The heading level.</param>
    /// <param name="text">The heading text.</param>
    /// <remarks>
    ///     A new heading restarts the paragraph and table ordinals, because they count within a
    ///     heading. Side effect: mutates <paramref name="state"/>.
    /// </remarks>
    private static void PushHeading(RootState state, int level, string text)
    {
        while (state.Path.Count > 0 && state.Path[^1].Level >= level)
        {
            state.Path.RemoveAt(state.Path.Count - 1);
        }

        state.Path.Add((level, text));
        state.ParagraphCount = 0;
        state.TableCount = 0;
    }

    /// <summary>
    ///     Determines whether a root is one whose page-numbering fields the reader strips.
    /// </summary>
    /// <param name="state">The root state.</param>
    /// <returns><see langword="true"/> for headers and footers.</returns>
    /// <remarks>Pure.</remarks>
    private static bool IsFurnitureRoot(RootState state) =>
        state.Part is WordRevisionPart.Header or WordRevisionPart.Footer;

    /// <summary>
    ///     Reads the text of an element as <c>content.md</c> would show it.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <param name="markup">Whether deleted text is visible.</param>
    /// <param name="stripFurniture">Whether page-numbering field results are dropped, as the reader does for headers and footers.</param>
    /// <returns>The concatenated text; tabs and breaks become spaces.</returns>
    /// <remarks>Pure.</remarks>
    private static string TextOf(OpenXmlElement element, bool markup, bool stripFurniture = false)
    {
        var builder = new StringBuilder();
        var fields = new Stack<WordOpenXmlReader.FieldFrame>();
        var unusedFurnitureFlag = false;
        foreach (var node in element.Descendants())
        {
            if (stripFurniture)
            {
                // Apply the reader's own field rules so header and footer text matches what it renders
                if (node is W.FieldChar fieldChar)
                {
                    WordOpenXmlReader.UpdateFieldState(fields, fieldChar, ref unusedFurnitureFlag);
                    continue;
                }

                if (node is W.FieldCode or W.DeletedFieldCode)
                {
                    WordOpenXmlReader.AppendFieldInstruction(fields, node);
                    continue;
                }

                if (WordOpenXmlReader.SkipText(fields, stripFurniture: true)
                    || node.Ancestors<W.SimpleField>().Any(field => WordOpenXmlReader.IsFurnitureInstruction(field.Instruction?.Value)))
                {
                    continue;
                }
            }

            switch (node)
            {
                case W.Text text:
                    builder.Append(text.Text);
                    break;
                case W.DeletedText deleted when markup:
                    builder.Append(deleted.Text);
                    break;
                case W.TabChar or W.Break:
                    builder.Append(' ');
                    break;
                default:
                    break;
            }
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Collapses whitespace runs to single spaces and trims.
    /// </summary>
    /// <param name="text">The text, or <see langword="null"/>.</param>
    /// <returns>The single-line text; empty for <see langword="null"/>.</returns>
    /// <remarks>Pure.</remarks>
    private static string Collapse(string? text) =>
        text is null ? string.Empty : string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>
    ///     Truncates text to a maximum length, marking the cut.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="maxLength">The maximum length before the marker.</param>
    /// <returns>The text, or its leading part and an ellipsis.</returns>
    /// <remarks>Pure.</remarks>
    private static string Truncate(string text, int maxLength) =>
        text.Length <= maxLength ? text : text[..maxLength].TrimEnd() + "…";

    /// <summary>
    ///     Determines whether a paragraph's mark was deleted or moved away.
    /// </summary>
    /// <param name="paragraph">The paragraph.</param>
    /// <returns><see langword="true"/> when accepting the change merges it into the next paragraph.</returns>
    /// <remarks>Pure.</remarks>
    private static bool HasDeletedMark(W.Paragraph paragraph)
    {
        var mark = paragraph.ParagraphProperties?.ParagraphMarkRunProperties;
        return mark is not null && (mark.GetFirstChild<W.Deleted>() is not null || mark.GetFirstChild<W.MoveFrom>() is not null);
    }

    /// <summary>
    ///     Classifies an element as a tracked change, or reports that it is not one.
    /// </summary>
    /// <param name="element">The element to classify.</param>
    /// <returns>The revision kind, or <see langword="null"/> when the element is not a tracked change.</returns>
    /// <remarks>
    ///     A paragraph-mark or row insertion and deletion are leaf <c>w:ins</c> and <c>w:del</c>
    ///     elements, told apart from run-level ones by type, and from each other by parent. Pure.
    /// </remarks>
    private static WordRevisionKind? Classify(OpenXmlElement element)
    {
        switch (element)
        {
            case W.InsertedRun:
                return WordRevisionKind.Inserted;
            case W.DeletedRun:
                return WordRevisionKind.Deleted;
            case W.MoveFromRun:
                return WordRevisionKind.MovedFrom;
            case W.MoveToRun:
                return WordRevisionKind.MovedTo;
            case W.Inserted or W.MoveTo:
                return ClassifyLeaf(element, inserted: true);
            case W.Deleted or W.MoveFrom:
                return ClassifyLeaf(element, inserted: false);
            case W.CellInsertion:
                return WordRevisionKind.TableInserted;
            case W.CellDeletion:
                return WordRevisionKind.TableDeleted;
            case W.CellMerge:
            case W.RunPropertiesChange:
            case W.ParagraphPropertiesChange:
            case W.ParagraphMarkRunPropertiesChange:
            case W.SectionPropertiesChange:
            case W.TablePropertiesChange:
            case W.TableGridChange:
            case W.TableCellPropertiesChange:
            case W.TableRowPropertiesChange:
            case W.TablePropertyExceptionsChange:
            case W.NumberingChange:
                return WordRevisionKind.FormattingChanged;
            default:
                return null;
        }
    }

    /// <summary>
    ///     Classifies a leaf insertion or deletion by where it sits.
    /// </summary>
    /// <param name="element">The leaf element.</param>
    /// <param name="inserted">Whether it is an insertion (or move destination) rather than a deletion.</param>
    /// <returns>The revision kind, or <see langword="null"/> for a leaf in a position that is not a content revision.</returns>
    /// <remarks>Pure.</remarks>
    private static WordRevisionKind? ClassifyLeaf(OpenXmlElement element, bool inserted)
    {
        return element.Parent switch
        {
            W.ParagraphMarkRunProperties => inserted
                ? WordRevisionKind.ParagraphMarkInserted
                : WordRevisionKind.ParagraphMarkDeleted,
            W.TableRowProperties => inserted ? WordRevisionKind.TableInserted : WordRevisionKind.TableDeleted,
            _ => null
        };
    }

    /// <summary>
    ///     Builds the bounded snippet of text a content revision changed.
    /// </summary>
    /// <param name="element">The revision element.</param>
    /// <returns>The snippet, or <see langword="null"/> when the revision carries no text.</returns>
    /// <remarks>Pure.</remarks>
    private static string? BuildSnippet(OpenXmlElement element)
    {
        var builder = new StringBuilder();
        foreach (var node in element.Descendants())
        {
            switch (node)
            {
                case W.Text text:
                    builder.Append(text.Text);
                    break;
                case W.DeletedText deleted:
                    builder.Append(deleted.Text);
                    break;
                default:
                    break;
            }
        }

        var snippet = Collapse(builder.ToString());
        if (snippet.Length == 0)
        {
            return null;
        }

        return snippet.Length <= SnippetMaxLength ? snippet : snippet[..SnippetMaxLength] + "…";
    }

    /// <summary>
    ///     Reads a Word-namespace attribute without throwing when it is absent or malformed.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <param name="localName">The attribute local name.</param>
    /// <returns>The raw attribute text, or <see langword="null"/> when absent or blank.</returns>
    /// <remarks>Reading the raw text avoids the typed accessors, which can throw on a malformed date. Pure.</remarks>
    private static string? ReadAttribute(OpenXmlElement element, string localName)
    {
        foreach (var attribute in element.GetAttributes())
        {
            if (attribute.LocalName == localName
                && (string.IsNullOrEmpty(attribute.NamespaceUri) || attribute.NamespaceUri == WordNamespace)
                && !string.IsNullOrWhiteSpace(attribute.Value))
            {
                return attribute.Value.Trim();
            }
        }

        return null;
    }

    /// <summary>
    ///     Parses a revision date as written, without time-zone conversion.
    /// </summary>
    /// <param name="value">The raw date text.</param>
    /// <returns>The date, or <see langword="null"/> when absent or unparsable.</returns>
    /// <remarks>Pure.</remarks>
    private static DateTime? ParseDate(string? value) =>
        value is not null
        && DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var date)
            ? date
            : null;

    /// <summary>The settings and results shared by the scan of every root.</summary>
    /// <param name="Markup">Whether deleted text is visible in the rendered output.</param>
    /// <param name="Toc">The table-of-contents paragraphs.</param>
    /// <param name="FootnoteNumbers">The footnote id to marker number map.</param>
    /// <remarks>Mutable only through <see cref="Revisions"/>; used within one scan.</remarks>
    private sealed record ScanContext(bool Markup, WordTocInfo Toc, Dictionary<long, int> FootnoteNumbers)
    {
        /// <summary>Gets the revisions found so far, in document order.</summary>
        public List<WordRevision> Revisions { get; } = [];
    }

    /// <summary>The running position within one root (the body, a header, a footer, or a footnote).</summary>
    /// <remarks>Mutable by design: the scan advances it block by block.</remarks>
    private sealed class RootState(WordRevisionPart part, string label, bool isBody)
    {
        /// <summary>Gets the part of the document the root belongs to.</summary>
        public WordRevisionPart Part { get; } = part;

        /// <summary>Gets the label opening every location in this root.</summary>
        public string Label { get; } = label;

        /// <summary>Gets a value indicating whether the root is the body, which has headings and a table of contents.</summary>
        public bool IsBody { get; } = isBody;

        /// <summary>Gets the heading path, outermost first.</summary>
        public List<(int Level, string Text)> Path { get; } = [];

        /// <summary>Gets or sets the number of text paragraphs seen under the current heading.</summary>
        public int ParagraphCount { get; set; }

        /// <summary>Gets or sets the number of tables seen under the current heading.</summary>
        public int TableCount { get; set; }

        /// <summary>Gets or sets the number of table-of-contents paragraphs seen.</summary>
        public int TocEntries { get; set; }

        /// <summary>Gets or sets the text carried forward from a paragraph whose mark was deleted.</summary>
        public string CarryText { get; set; } = string.Empty;
    }
}
