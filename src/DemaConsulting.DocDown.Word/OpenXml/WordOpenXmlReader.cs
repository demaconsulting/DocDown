using System.Globalization;
using System.Text;
using DocDown.Core;
using DocDown.Word.Markdown;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using A = DocumentFormat.OpenXml.Drawing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
using W = DocumentFormat.OpenXml.Wordprocessing;
using WP = DocumentFormat.OpenXml.Drawing.Wordprocessing;

namespace DocDown.Word.OpenXml;

/// <summary>
///     Reads a Word Open XML document into the backend-neutral <see cref="WordDocumentModel"/>.
/// </summary>
/// <remarks>
///     <para>
///         This unit owns the mapping from Open XML structure to the model: headings from styles,
///         ordered and bulleted lists from the numbering definitions, genuine tables from
///         <c>w:tbl</c>, images from blips, the accepted-revisions view of tracked changes, comments,
///         footnotes, and — the feature that most distinguishes Word from PDF — the document-control
///         section built from headers and footers once, with page-numbering furniture detected
///         structurally by field instruction rather than by pattern-matching rendered text.
///     </para>
///     <para>
///         It opens the package read-only and never materializes <c>InnerXml</c>, so memory scales
///         with document size. A password-protected document arrives as an OLE compound file rather
///         than a ZIP, which is detected up front and turned into a clear
///         <see cref="WordExtractionException"/> that Core maps to a structured failure. An instance
///         holds per-read state, so a fresh instance is used per document.
///     </para>
/// </remarks>
internal sealed class WordOpenXmlReader
{
    /// <summary>The OLE compound-file signature that fronts a password-protected package.</summary>
    private static readonly byte[] OleCompoundFileSignature =
        [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];

    /// <summary>The field instructions whose result is page-numbering furniture.</summary>
    private static readonly HashSet<string> FurnitureFieldInstructions =
        new(StringComparer.OrdinalIgnoreCase) { "PAGE", "NUMPAGES", "SECTIONPAGES", "SECTIONPAGESNUM" };

    /// <summary>The numbering map from a numbering id and level to whether the list is ordered.</summary>
    private Dictionary<int, Dictionary<int, bool>> _numbering = [];

    /// <summary>The footnote bodies collected in reference order.</summary>
    private readonly List<IReadOnlyList<WordInline>> _footnoteBodies = [];

    /// <summary>The footnote definitions indexed by id.</summary>
    private Dictionary<int, W.Footnote> _footnotesById = [];

    /// <summary>The number of tracked-change revisions seen in the accepted view.</summary>
    private int _trackedChanges;

    /// <summary>The number of empty tables skipped.</summary>
    private int _emptyTables;

    /// <summary>The text of the nearest preceding heading in the body walk, for image naming context.</summary>
    /// <remarks>
    ///     Threaded through the body walk so a drawing can offer its section heading as a low-confidence
    ///     naming hint. It is context, not a description, so it never becomes asserted alt text —
    ///     only a filename hint and an honestly-sourced manifest entry.
    /// </remarks>
    private string? _currentHeading;

    /// <summary>
    ///     Reads a document stream into the model.
    /// </summary>
    /// <param name="docx">A seekable stream over the document. Must not be null.</param>
    /// <returns>The document model.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="docx"/> is <see langword="null"/>.</exception>
    /// <exception cref="WordExtractionException">Thrown when the stream is a password-protected (OLE compound file) document.</exception>
    /// <remarks>Read-only. Adverse documents other than the detected encrypted case are left to the SDK, which Core wraps.</remarks>
    public WordDocumentModel Read(Stream docx)
    {
        ArgumentNullException.ThrowIfNull(docx);
        ThrowIfPasswordProtected(docx);

        using var document = WordprocessingDocument.Open(docx, false);
        var mainPart = document.MainDocumentPart
            ?? throw new WordExtractionException("The document has no main document part and cannot be read.");

        _numbering = BuildNumbering(mainPart);
        _footnotesById = BuildFootnoteIndex(mainPart);
        _footnoteBodies.Clear();
        _trackedChanges = 0;
        _emptyTables = 0;
        _currentHeading = null;

        var body = mainPart.Document?.Body;
        var blocks = new List<WordBlock>();
        if (body is not null)
        {
            foreach (var element in body.ChildElements)
            {
                AppendBodyElement(blocks, mainPart, element);
            }
        }

        var (control, found, emptyParts, furnitureParts) = BuildDocumentControl(mainPart, body);
        var comments = BuildComments(mainPart);

        return new WordDocumentModel(
            Body: blocks,
            DocumentControl: control,
            Comments: comments,
            Footnotes: _footnoteBodies.ToList(),
            Title: NullIfBlank(document.PackageProperties.Title),
            Author: NullIfBlank(document.PackageProperties.Creator),
            ProducerPageCount: ReadProducerPageCount(document),
            TrackedChangeCount: _trackedChanges,
            HeaderFooterPartsFound: found,
            HeaderFooterPartsEmpty: emptyParts,
            HeaderFooterPartsPageFurniture: furnitureParts,
            EmptyTablesSkipped: _emptyTables,
            Metadata: OpcMetadataMapper.From(new OpcCoreProperties(
                document.PackageProperties.Creator,
                document.PackageProperties.LastModifiedBy,
                document.PackageProperties.Created,
                document.PackageProperties.Modified,
                document.PackageProperties.Revision,
                document.PackageProperties.Title,
                document.PackageProperties.Subject,
                document.PackageProperties.Keywords,
                document.PackageProperties.Category,
                document.PackageProperties.ContentStatus,
                document.PackageProperties.Description,
                document.PackageProperties.LastPrinted,
                document.PackageProperties.Version,
                document.PackageProperties.Language,
                document.PackageProperties.Identifier)));
    }

    /// <summary>
    ///     Appends the blocks produced by one top-level body element.
    /// </summary>
    /// <param name="blocks">The block list to append to.</param>
    /// <param name="mainPart">The main document part, for image resolution.</param>
    /// <param name="element">The body element (paragraph, table, or section properties).</param>
    /// <remarks>Section properties are handled separately for document control. Side effect: appends.</remarks>
    private void AppendBodyElement(List<WordBlock> blocks, MainDocumentPart mainPart, OpenXmlElement element)
    {
        switch (element)
        {
            case W.Paragraph paragraph:
                AppendParagraph(blocks, mainPart, paragraph, stripFurniture: false);
                break;

            case W.Table table:
                var model = BuildTable(mainPart, table);
                if (model is null)
                {
                    _emptyTables++;
                }
                else
                {
                    blocks.Add(new WordBlock(WordBlockKind.Table, Table: model));
                }

                break;

            default:
                break;
        }
    }

    /// <summary>
    ///     Appends the blocks produced by one paragraph.
    /// </summary>
    /// <param name="blocks">The block list to append to.</param>
    /// <param name="part">The part the paragraph belongs to, for image and hyperlink resolution.</param>
    /// <param name="paragraph">The paragraph to render.</param>
    /// <param name="stripFurniture">Whether to strip page-numbering field results (used for headers and footers).</param>
    /// <remarks>Side effect: appends heading, list-item, paragraph, image, and page-break blocks in document order.</remarks>
    private void AppendParagraph(List<WordBlock> blocks, OpenXmlPartContainer part, W.Paragraph paragraph, bool stripFurniture)
    {
        var inlines = new List<WordInline>();
        var images = new List<WordImageRef>();
        var pageBreak = false;
        var hadFurnitureField = false;
        WalkContainer(paragraph, part, new Stack<FieldFrame>(), inlines, images, stripFurniture,
            ref pageBreak, ref hadFurnitureField);

        var level = DetermineHeadingLevel(paragraph);
        if (HasVisibleText(inlines))
        {
            if (level > 0)
            {
                blocks.Add(new WordBlock(WordBlockKind.Heading, inlines, HeadingLevel: level));

                // Remember this heading so a later drawing can offer it as a naming hint; the walk
                // above already collected any image in this paragraph against the previous heading,
                // which is the correct "nearest preceding" context
                _currentHeading = string.Concat(inlines.Select(inline => inline.Text)).Trim();
            }
            else if (TryGetListInfo(paragraph, out var list))
            {
                blocks.Add(new WordBlock(WordBlockKind.ListItem, inlines, List: list));
            }
            else
            {
                blocks.Add(new WordBlock(WordBlockKind.Paragraph, inlines));
            }
        }

        foreach (var image in images)
        {
            blocks.Add(new WordBlock(WordBlockKind.Image, Image: image));
        }

        if (pageBreak)
        {
            blocks.Add(new WordBlock(WordBlockKind.PageBreak));
        }
    }

    /// <summary>
    ///     Walks a run container, appending inline runs and collecting images, honoring the
    ///     accepted-revisions view and field furniture stripping.
    /// </summary>
    /// <param name="container">The container to walk (a paragraph, hyperlink, inserted run, or field).</param>
    /// <param name="part">The part for image and hyperlink resolution.</param>
    /// <param name="fields">The active complex-field frames.</param>
    /// <param name="inlines">The inline list to append to.</param>
    /// <param name="images">The image list to append to.</param>
    /// <param name="stripFurniture">Whether to strip page-numbering field results.</param>
    /// <param name="pageBreak">Set to <see langword="true"/> when a page break is seen.</param>
    /// <param name="hadFurnitureField">Set to <see langword="true"/> when a furniture field is seen.</param>
    /// <remarks>
    ///     Inserted runs are included and deleted runs excluded so the output is the accepted view;
    ///     each is counted as a tracked change. Side effect: appends to <paramref name="inlines"/> and
    ///     <paramref name="images"/>.
    /// </remarks>
    private void WalkContainer(
        OpenXmlElement container, OpenXmlPartContainer part, Stack<FieldFrame> fields,
        List<WordInline> inlines, List<WordImageRef> images, bool stripFurniture,
        ref bool pageBreak, ref bool hadFurnitureField)
    {
        foreach (var child in container.ChildElements)
        {
            switch (child)
            {
                case W.Run run:
                    ProcessRun(run, part, fields, inlines, images, stripFurniture, ref pageBreak, ref hadFurnitureField);
                    break;

                case W.InsertedRun inserted:
                    _trackedChanges++;
                    WalkContainer(inserted, part, fields, inlines, images, stripFurniture, ref pageBreak, ref hadFurnitureField);
                    break;

                case W.DeletedRun:
                    // Accepted view: a deleted run contributes no text, but the revision is counted
                    _trackedChanges++;
                    break;

                case W.Hyperlink hyperlink:
                    AppendHyperlink(hyperlink, part, fields, inlines, images, stripFurniture, ref pageBreak, ref hadFurnitureField);
                    break;

                case W.SimpleField simpleField:
                    AppendSimpleField(simpleField, part, fields, inlines, images, stripFurniture, ref pageBreak, ref hadFurnitureField);
                    break;

                default:
                    break;
            }
        }
    }

    /// <summary>
    ///     Processes a single run, appending its text and collecting any images and footnote markers.
    /// </summary>
    /// <param name="run">The run to process.</param>
    /// <param name="part">The part for image resolution.</param>
    /// <param name="fields">The active complex-field frames.</param>
    /// <param name="inlines">The inline list to append to.</param>
    /// <param name="images">The image list to append to.</param>
    /// <param name="stripFurniture">Whether to strip page-numbering field results.</param>
    /// <param name="pageBreak">Set to <see langword="true"/> when a page break is seen.</param>
    /// <param name="hadFurnitureField">Set to <see langword="true"/> when a furniture field is seen.</param>
    /// <remarks>Side effect: appends to <paramref name="inlines"/> and <paramref name="images"/>.</remarks>
    private void ProcessRun(
        W.Run run, OpenXmlPartContainer part, Stack<FieldFrame> fields, List<WordInline> inlines,
        List<WordImageRef> images, bool stripFurniture, ref bool pageBreak, ref bool hadFurnitureField)
    {
        var bold = IsOn(run.RunProperties?.Bold);
        var italic = IsOn(run.RunProperties?.Italic);
        var text = new StringBuilder();

        foreach (var child in run.ChildElements)
        {
            switch (child)
            {
                case W.FieldChar fieldChar:
                    UpdateFieldState(fields, fieldChar, ref hadFurnitureField);
                    break;

                case W.FieldCode code:
                    if (fields.Count > 0)
                    {
                        fields.Peek().Instruction.Append(code.Text);
                    }

                    break;

                case W.Text runText when !SkipText(fields, stripFurniture):
                    text.Append(runText.Text);
                    break;

                case W.TabChar when !SkipText(fields, stripFurniture):
                    text.Append(' ');
                    break;

                case W.Break brk:
                    if (brk.Type?.Value == W.BreakValues.Page)
                    {
                        pageBreak = true;
                    }
                    else if (!SkipText(fields, stripFurniture))
                    {
                        text.Append('\n');
                    }

                    break;

                case W.Drawing drawing:
                    CollectImage(drawing, part, images);
                    break;

                case W.FootnoteReference footnoteReference:
                    FlushText(text, bold, italic, inlines);
                    AppendFootnote(footnoteReference, part, inlines);
                    break;

                default:
                    break;
            }
        }

        FlushText(text, bold, italic, inlines);
    }

    /// <summary>
    ///     Flushes accumulated run text as an inline, if it carries any content.
    /// </summary>
    /// <param name="text">The accumulated text, cleared by this call.</param>
    /// <param name="bold">Whether the run is bold.</param>
    /// <param name="italic">Whether the run is italic.</param>
    /// <param name="inlines">The inline list to append to.</param>
    /// <remarks>Side effect: appends to <paramref name="inlines"/> and clears <paramref name="text"/>.</remarks>
    private static void FlushText(StringBuilder text, bool bold, bool italic, List<WordInline> inlines)
    {
        if (text.Length == 0)
        {
            return;
        }

        inlines.Add(new WordInline(text.ToString(), bold, italic));
        text.Clear();
    }

    /// <summary>
    ///     Appends a hyperlink's text as a single linked inline.
    /// </summary>
    /// <param name="hyperlink">The hyperlink element.</param>
    /// <param name="part">The part for relationship resolution.</param>
    /// <param name="fields">The active complex-field frames.</param>
    /// <param name="inlines">The inline list to append to.</param>
    /// <param name="images">The image list to append to.</param>
    /// <param name="stripFurniture">Whether to strip page-numbering field results.</param>
    /// <param name="pageBreak">Set to <see langword="true"/> when a page break is seen.</param>
    /// <param name="hadFurnitureField">Set to <see langword="true"/> when a furniture field is seen.</param>
    /// <remarks>Side effect: appends to <paramref name="inlines"/>.</remarks>
    private void AppendHyperlink(
        W.Hyperlink hyperlink, OpenXmlPartContainer part, Stack<FieldFrame> fields, List<WordInline> inlines,
        List<WordImageRef> images, bool stripFurniture, ref bool pageBreak, ref bool hadFurnitureField)
    {
        var inner = new List<WordInline>();
        WalkContainer(hyperlink, part, fields, inner, images, stripFurniture, ref pageBreak, ref hadFurnitureField);

        var text = string.Concat(inner.Select(inline => inline.Text)).Trim();
        if (text.Length == 0)
        {
            return;
        }

        var href = ResolveHyperlink(hyperlink, part);
        inlines.Add(new WordInline(text, Href: href));
    }

    /// <summary>
    ///     Appends the cached result of a simple field, honoring furniture stripping.
    /// </summary>
    /// <param name="field">The simple field element.</param>
    /// <param name="part">The part for image resolution.</param>
    /// <param name="fields">The active complex-field frames.</param>
    /// <param name="inlines">The inline list to append to.</param>
    /// <param name="images">The image list to append to.</param>
    /// <param name="stripFurniture">Whether to strip page-numbering field results.</param>
    /// <param name="pageBreak">Set to <see langword="true"/> when a page break is seen.</param>
    /// <param name="hadFurnitureField">Set to <see langword="true"/> when a furniture field is seen.</param>
    /// <remarks>Side effect: appends to <paramref name="inlines"/>.</remarks>
    private void AppendSimpleField(
        W.SimpleField field, OpenXmlPartContainer part, Stack<FieldFrame> fields, List<WordInline> inlines,
        List<WordImageRef> images, bool stripFurniture, ref bool pageBreak, ref bool hadFurnitureField)
    {
        var furniture = IsFurnitureInstruction(field.Instruction?.Value);
        if (furniture)
        {
            hadFurnitureField = true;
            if (stripFurniture)
            {
                return;
            }
        }

        WalkContainer(field, part, fields, inlines, images, stripFurniture, ref pageBreak, ref hadFurnitureField);
    }

    /// <summary>
    ///     Updates the complex-field frame stack for a field character.
    /// </summary>
    /// <param name="fields">The field frame stack.</param>
    /// <param name="fieldChar">The field character.</param>
    /// <param name="hadFurnitureField">Set to <see langword="true"/> when the field is furniture.</param>
    /// <remarks>Side effect: mutates <paramref name="fields"/>.</remarks>
    private static void UpdateFieldState(Stack<FieldFrame> fields, W.FieldChar fieldChar, ref bool hadFurnitureField)
    {
        switch (fieldChar.FieldCharType?.Value)
        {
            case W.FieldCharValues f when f == W.FieldCharValues.Begin:
                fields.Push(new FieldFrame());
                break;

            case W.FieldCharValues f when f == W.FieldCharValues.Separate && fields.Count > 0:
                var frame = fields.Peek();
                frame.InResult = true;
                frame.Furniture = IsFurnitureInstruction(frame.Instruction.ToString());
                hadFurnitureField |= frame.Furniture;
                break;

            case W.FieldCharValues f when f == W.FieldCharValues.End && fields.Count > 0:
                fields.Pop();
                break;

            default:
                break;
        }
    }

    /// <summary>
    ///     Determines whether the current field text should be skipped.
    /// </summary>
    /// <param name="fields">The active field frames.</param>
    /// <param name="stripFurniture">Whether furniture fields are being stripped.</param>
    /// <returns><see langword="true"/> when the text is a field definition or stripped furniture; otherwise <see langword="false"/>.</returns>
    /// <remarks>Field-definition text (before the separator) is never content; furniture results are stripped on request. Pure.</remarks>
    private static bool SkipText(Stack<FieldFrame> fields, bool stripFurniture)
    {
        if (fields.Count == 0)
        {
            return false;
        }

        var frame = fields.Peek();
        return !frame.InResult || (frame.Furniture && stripFurniture);
    }

    /// <summary>
    ///     Collects the images referenced by a drawing, choosing each image's naming and description
    ///     text from the sources the document offers.
    /// </summary>
    /// <param name="drawing">The drawing element.</param>
    /// <param name="part">The part the drawing's relationships are scoped to.</param>
    /// <param name="images">The image list to append to.</param>
    /// <remarks>
    ///     Gathers candidate text in preference order (author description, title, adjacent caption,
    ///     object name, nearest preceding heading) and hands it to <see cref="WordOpenXmlImageReader"/>,
    ///     which appends the media-name fallback and applies the shared policy. Side effect: appends
    ///     to <paramref name="images"/>.
    /// </remarks>
    private void CollectImage(W.Drawing drawing, OpenXmlPartContainer part, List<WordImageRef> images)
    {
        var candidates = GatherImageTextCandidates(drawing, part);
        foreach (var blip in drawing.Descendants<A.Blip>())
        {
            var image = WordOpenXmlImageReader.Resolve(part, blip.Embed?.Value, candidates);
            if (image is not null)
            {
                images.Add(image);
            }
        }
    }

    /// <summary>
    ///     Gathers the document-supplied text candidates for a drawing, in preference order.
    /// </summary>
    /// <param name="drawing">The drawing element.</param>
    /// <param name="part">The part the drawing belongs to, for reading an adjacent caption.</param>
    /// <returns>The candidates: author description, title, caption, object name, and nearest heading.</returns>
    /// <remarks>
    ///     The <c>wp:docPr/@descr</c> and <c>wp:docPr/@title</c> are the author's alt text and title;
    ///     the caption is the adjacent <c>Caption</c>-styled paragraph read through the field-aware
    ///     walker so its <c>SEQ</c> numbering is included structurally; the object name is
    ///     <c>pic:cNvPr/@name</c>; the heading is the nearest preceding body heading. The media part
    ///     name is not gathered here — the image reader appends it as the final fallback. Read-only.
    /// </remarks>
    private List<ImageTextCandidate> GatherImageTextCandidates(W.Drawing drawing, OpenXmlPartContainer part)
    {
        var docProperties = drawing.Descendants<WP.DocProperties>().FirstOrDefault();
        var pictureName = drawing.Descendants<PIC.NonVisualDrawingProperties>().FirstOrDefault()?.Name?.Value;

        return
        [
            new ImageTextCandidate(docProperties?.Description?.Value, ImageTextSource.Description),
            new ImageTextCandidate(docProperties?.Title?.Value, ImageTextSource.Title),
            new ImageTextCandidate(FindAdjacentCaption(drawing, part), ImageTextSource.Caption),
            new ImageTextCandidate(pictureName, ImageTextSource.PictureName),
            new ImageTextCandidate(_currentHeading, ImageTextSource.Heading)
        ];
    }

    /// <summary>
    ///     Reads the text of a <c>Caption</c>-styled paragraph adjacent to a drawing, when present.
    /// </summary>
    /// <param name="drawing">The drawing whose caption is sought.</param>
    /// <param name="part">The part the caption paragraph belongs to, for the field-aware walk.</param>
    /// <returns>The caption text, or <see langword="null"/> when no adjacent caption exists.</returns>
    /// <remarks>
    ///     A caption follows or precedes the image's paragraph. Its text is read through the same
    ///     field-aware walker the body uses, so a <c>SEQ</c> field's rendered number is included as a
    ///     field <em>result</em> rather than by matching digits in text. Read-only.
    /// </remarks>
    private string? FindAdjacentCaption(W.Drawing drawing, OpenXmlPartContainer part)
    {
        var paragraph = drawing.Ancestors<W.Paragraph>().FirstOrDefault();
        if (paragraph is null)
        {
            return null;
        }

        var caption = AdjacentCaptionParagraph(paragraph);
        return caption is null ? null : ReadParagraphText(part, caption);
    }

    /// <summary>
    ///     Finds the caption paragraph adjacent to an image's paragraph, preferring the one after it.
    /// </summary>
    /// <param name="paragraph">The image's paragraph.</param>
    /// <returns>The adjacent <c>Caption</c>-styled paragraph, or <see langword="null"/> when none.</returns>
    /// <remarks>A figure caption conventionally follows the figure, so the following paragraph is preferred. Pure.</remarks>
    private static W.Paragraph? AdjacentCaptionParagraph(W.Paragraph paragraph)
    {
        var following = FirstParagraphSibling(paragraph, forward: true);
        if (following is not null && IsCaptionStyle(following))
        {
            return following;
        }

        var preceding = FirstParagraphSibling(paragraph, forward: false);
        return preceding is not null && IsCaptionStyle(preceding) ? preceding : null;
    }

    /// <summary>
    ///     Finds the first paragraph sibling in a direction, skipping non-paragraph siblings.
    /// </summary>
    /// <param name="element">The element to search out from.</param>
    /// <param name="forward"><see langword="true"/> to search following siblings; otherwise preceding.</param>
    /// <returns>The nearest paragraph sibling, or <see langword="null"/> when none.</returns>
    /// <remarks>Skips intervening bookmarks and properties so an immediately adjacent caption is still found. Pure.</remarks>
    private static W.Paragraph? FirstParagraphSibling(OpenXmlElement element, bool forward)
    {
        var sibling = forward ? element.NextSibling() : element.PreviousSibling();
        while (sibling is not null)
        {
            if (sibling is W.Paragraph paragraph)
            {
                return paragraph;
            }

            sibling = forward ? sibling.NextSibling() : sibling.PreviousSibling();
        }

        return null;
    }

    /// <summary>
    ///     Reports whether a paragraph uses the <c>Caption</c> style.
    /// </summary>
    /// <param name="paragraph">The paragraph to test.</param>
    /// <returns><see langword="true"/> when the paragraph's style resolves to <c>Caption</c>; otherwise <see langword="false"/>.</returns>
    /// <remarks>Structural: identified by style id, matching how headings are classified, not by content. Pure.</remarks>
    private static bool IsCaptionStyle(W.Paragraph paragraph)
    {
        var styleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
        if (styleId is null)
        {
            return false;
        }

        var normalized = styleId.Replace(" ", string.Empty, StringComparison.Ordinal);
        return string.Equals(normalized, "Caption", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Reads the visible text of a paragraph through the field-aware inline walker.
    /// </summary>
    /// <param name="part">The part the paragraph belongs to.</param>
    /// <param name="paragraph">The paragraph to read.</param>
    /// <returns>The concatenated inline text, which may be empty.</returns>
    /// <remarks>
    ///     Reuses the same walk the body uses so a caption's field results (for example a <c>SEQ</c>
    ///     number) are surfaced structurally; any nested images are discarded here because only the
    ///     caption text is wanted. Read-only.
    /// </remarks>
    private string ReadParagraphText(OpenXmlPartContainer part, W.Paragraph paragraph)
    {
        var inlines = new List<WordInline>();
        var discardedImages = new List<WordImageRef>();
        var pageBreak = false;
        var hadFurnitureField = false;
        WalkContainer(paragraph, part, new Stack<FieldFrame>(), inlines, discardedImages,
            stripFurniture: false, ref pageBreak, ref hadFurnitureField);
        return string.Concat(inlines.Select(inline => inline.Text)).Trim();
    }

    /// <summary>
    ///     Appends a footnote reference marker and collects the footnote body.
    /// </summary>
    /// <param name="reference">The footnote reference.</param>
    /// <param name="part">The part for footnote image resolution.</param>
    /// <param name="inlines">The inline list to append the marker to.</param>
    /// <remarks>Side effect: appends a raw marker inline and records the footnote body.</remarks>
    private void AppendFootnote(W.FootnoteReference reference, OpenXmlPartContainer part, List<WordInline> inlines)
    {
        var id = reference.Id?.Value;
        if (id is null || !_footnotesById.TryGetValue((int)id.Value, out var footnote))
        {
            return;
        }

        var ordinal = _footnoteBodies.Count + 1;
        inlines.Add(new WordInline($"[^{ordinal.ToString(CultureInfo.InvariantCulture)}]", Raw: true));

        var bodyInlines = new List<WordInline>();
        var images = new List<WordImageRef>();
        var pageBreak = false;
        var hadFurniture = false;
        foreach (var paragraph in footnote.Elements<W.Paragraph>())
        {
            WalkContainer(paragraph, part, new Stack<FieldFrame>(), bodyInlines, images, stripFurniture: false,
                ref pageBreak, ref hadFurniture);
        }

        _footnoteBodies.Add(bodyInlines);
    }

    /// <summary>
    ///     Builds a table model from a table element, or reports it empty.
    /// </summary>
    /// <param name="part">The part for image resolution.</param>
    /// <param name="table">The table element.</param>
    /// <returns>The table model, or <see langword="null"/> when the table has no cell content.</returns>
    /// <remarks>
    ///     Horizontal merges pad the grid with empty cells, vertical-merge continuations are emptied,
    ///     and nested tables are flattened; each is counted so the caller can report the flattening.
    ///     Read-only.
    /// </remarks>
    private WordTableModel? BuildTable(OpenXmlPartContainer part, W.Table table)
    {
        var rows = new List<IReadOnlyList<WordTableCell>>();
        var merged = 0;
        var nested = 0;
        var firstRowIsHeader = false;
        var rowElements = table.Elements<W.TableRow>().ToList();

        for (var rowIndex = 0; rowIndex < rowElements.Count; rowIndex++)
        {
            var rowElement = rowElements[rowIndex];
            if (rowIndex == 0)
            {
                firstRowIsHeader = IsHeaderRow(rowElement);
            }

            var cells = new List<WordTableCell>();
            foreach (var cellElement in rowElement.Elements<W.TableCell>())
            {
                var (content, nestedInCell) = BuildCellContent(part, cellElement);
                nested += nestedInCell;

                if (IsVerticalMergeContinuation(cellElement))
                {
                    cells.Add(new WordTableCell([]));
                    merged++;
                }
                else
                {
                    cells.Add(content);
                }

                var span = GridSpanOf(cellElement);
                for (var extra = 1; extra < span; extra++)
                {
                    cells.Add(new WordTableCell([]));
                    merged++;
                }
            }

            rows.Add(cells);
        }

        var hasContent = rows.Any(row => row.Any(cell =>
            cell.Content.Any(inline => !string.IsNullOrWhiteSpace(inline.Text))));
        return hasContent ? new WordTableModel(rows, firstRowIsHeader, merged, nested) : null;
    }

    /// <summary>
    ///     Builds a cell's inline content, flattening any nested tables.
    /// </summary>
    /// <param name="part">The part for image resolution.</param>
    /// <param name="cell">The cell element.</param>
    /// <returns>The cell content and the number of nested tables flattened into it.</returns>
    /// <remarks>
    ///     Paragraphs are joined by hard breaks; a nested table is rendered as break-joined rows in a
    ///     single raw inline because GFM cannot nest a table. Read-only.
    /// </remarks>
    private (WordTableCell Cell, int Nested) BuildCellContent(OpenXmlPartContainer part, W.TableCell cell)
    {
        var inlines = new List<WordInline>();
        var nested = 0;

        foreach (var element in cell.ChildElements)
        {
            switch (element)
            {
                case W.Paragraph paragraph:
                    var paragraphInlines = new List<WordInline>();
                    var images = new List<WordImageRef>();
                    var pageBreak = false;
                    var hadFurniture = false;
                    WalkContainer(paragraph, part, new Stack<FieldFrame>(), paragraphInlines, images,
                        stripFurniture: false, ref pageBreak, ref hadFurniture);
                    if (paragraphInlines.Count > 0)
                    {
                        if (inlines.Count > 0)
                        {
                            inlines.Add(new WordInline("\n", Raw: true));
                        }

                        inlines.AddRange(paragraphInlines);
                    }

                    break;

                case W.Table innerTable:
                    nested++;
                    var flattened = FlattenNestedTable(part, innerTable);
                    if (flattened.Length > 0)
                    {
                        if (inlines.Count > 0)
                        {
                            inlines.Add(new WordInline("\n", Raw: true));
                        }

                        inlines.Add(new WordInline(flattened, Raw: true));
                    }

                    break;

                default:
                    break;
            }
        }

        return (new WordTableCell(inlines), nested);
    }

    /// <summary>
    ///     Flattens a nested table into break-joined rows for a parent cell.
    /// </summary>
    /// <param name="part">The part for image resolution.</param>
    /// <param name="table">The nested table.</param>
    /// <returns>The flattened rows as markdown, joined by hard breaks.</returns>
    /// <remarks>Read-only.</remarks>
    private string FlattenNestedTable(OpenXmlPartContainer part, W.Table table)
    {
        var lines = new List<string>();
        foreach (var row in table.Elements<W.TableRow>())
        {
            var cellTexts = new List<string>();
            foreach (var cell in row.Elements<W.TableCell>())
            {
                var (content, _) = BuildCellContent(part, cell);
                var text = WordMarkdownWriter.RenderInlines(content.Content).Replace("\n", " ", StringComparison.Ordinal).Trim();
                if (text.Length > 0)
                {
                    cellTexts.Add(text);
                }
            }

            if (cellTexts.Count > 0)
            {
                lines.Add(string.Join(" | ", cellTexts));
            }
        }

        return string.Join("<br>", lines);
    }

    /// <summary>
    ///     Builds the document-control section from headers and footers, once and deduplicated.
    /// </summary>
    /// <param name="mainPart">The main document part.</param>
    /// <param name="body">The document body, whose section properties reference the parts.</param>
    /// <returns>The surviving subsections, the parts found, the empty parts omitted, and the page-furniture parts omitted.</returns>
    /// <remarks>
    ///     Furniture is detected structurally, by field instruction, never by matching rendered text.
    ///     Identical rendered content across sections collapses to one entry. The two omission counts
    ///     are kept apart so each can be reported with its own matching wording — an empty part and a
    ///     furniture-only part are distinct expected cases, never described by one another's text.
    ///     Read-only.
    /// </remarks>
    private (IReadOnlyList<WordDocumentControlSection> Sections, int Found, int EmptyParts, int FurnitureParts)
        BuildDocumentControl(MainDocumentPart mainPart, W.Body? body)
    {
        var references = CollectHeaderFooterReferences(mainPart, body);
        var headerSurvivors = new List<IReadOnlyList<WordBlock>>();
        var footerSurvivors = new List<IReadOnlyList<WordBlock>>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var empty = new Dictionary<string, string>();

        var found = 0;
        var emptyParts = 0;
        var furnitureParts = 0;

        foreach (var (isHeader, part) in references)
        {
            found++;
            var blocks = RenderPartBlocks(part, out var hadFurnitureField);
            if (!HasVisibleContent(blocks))
            {
                // Classify the omission by cause so each is reported with its own matching wording:
                // a part that carried only page-numbering fields is furniture; otherwise it was empty.
                if (hadFurnitureField)
                {
                    furnitureParts++;
                }
                else
                {
                    emptyParts++;
                }

                continue;
            }

            var key = (isHeader ? "H|" : "F|") + WordMarkdownWriter.WriteBlocks(blocks, empty);
            if (!seen.Add(key))
            {
                continue;
            }

            (isHeader ? headerSurvivors : footerSurvivors).Add(blocks);
        }

        var sections = new List<WordDocumentControlSection>();
        AddLabeledSections(sections, "Header", headerSurvivors);
        AddLabeledSections(sections, "Footer", footerSurvivors);

        return (sections, found, emptyParts, furnitureParts);
    }

    /// <summary>
    ///     Renders a header or footer part into blocks with furniture stripped.
    /// </summary>
    /// <param name="part">The header or footer part.</param>
    /// <param name="hadFurnitureField">Set to <see langword="true"/> when the part carried a furniture field.</param>
    /// <returns>The rendered blocks.</returns>
    /// <remarks>Read-only.</remarks>
    private List<WordBlock> RenderPartBlocks(OpenXmlPart part, out bool hadFurnitureField)
    {
        hadFurnitureField = false;
        var blocks = new List<WordBlock>();
        var root = (part as HeaderPart)?.Header as OpenXmlElement ?? (part as FooterPart)?.Footer;
        if (root is null)
        {
            return blocks;
        }

        foreach (var element in root.ChildElements)
        {
            switch (element)
            {
                case W.Paragraph paragraph:
                    var before = blocks.Count;
                    var seenFurniture = AppendParagraphTrackingFurniture(blocks, part, paragraph);
                    hadFurnitureField |= seenFurniture;
                    _ = before;
                    break;

                case W.Table table:
                    var model = BuildTable(part, table);
                    if (model is not null)
                    {
                        blocks.Add(new WordBlock(WordBlockKind.Table, Table: model));
                    }

                    break;

                default:
                    break;
            }
        }

        return blocks;
    }

    /// <summary>
    ///     Appends a header or footer paragraph, stripping furniture and reporting whether any was seen.
    /// </summary>
    /// <param name="blocks">The block list to append to.</param>
    /// <param name="part">The part for image resolution.</param>
    /// <param name="paragraph">The paragraph to render.</param>
    /// <returns><see langword="true"/> when the paragraph carried a furniture field; otherwise <see langword="false"/>.</returns>
    /// <remarks>Side effect: appends to <paramref name="blocks"/>.</remarks>
    private bool AppendParagraphTrackingFurniture(List<WordBlock> blocks, OpenXmlPartContainer part, W.Paragraph paragraph)
    {
        var inlines = new List<WordInline>();
        var images = new List<WordImageRef>();
        var pageBreak = false;
        var hadFurnitureField = false;
        WalkContainer(paragraph, part, new Stack<FieldFrame>(), inlines, images, stripFurniture: true,
            ref pageBreak, ref hadFurnitureField);

        if (HasVisibleText(inlines))
        {
            blocks.Add(new WordBlock(WordBlockKind.Paragraph, inlines));
        }

        foreach (var image in images)
        {
            blocks.Add(new WordBlock(WordBlockKind.Image, Image: image));
        }

        return hadFurnitureField;
    }

    /// <summary>
    ///     Collects the header and footer parts referenced by every section, in document order.
    /// </summary>
    /// <param name="mainPart">The main document part.</param>
    /// <param name="body">The document body.</param>
    /// <returns>The referenced parts, each tagged as a header or footer.</returns>
    /// <remarks>Read-only.</remarks>
    private static List<(bool IsHeader, OpenXmlPart Part)> CollectHeaderFooterReferences(
        MainDocumentPart mainPart, W.Body? body)
    {
        var result = new List<(bool, OpenXmlPart)>();
        if (body is null)
        {
            return result;
        }

        var sectionProperties = body.Descendants<W.SectionProperties>().ToList();
        foreach (var section in sectionProperties)
        {
            foreach (var reference in section.Elements<W.HeaderReference>())
            {
                if (TryResolvePart(mainPart, reference.Id?.Value) is { } part)
                {
                    result.Add((true, part));
                }
            }

            foreach (var reference in section.Elements<W.FooterReference>())
            {
                if (TryResolvePart(mainPart, reference.Id?.Value) is { } part)
                {
                    result.Add((false, part));
                }
            }
        }

        return result;
    }

    /// <summary>
    ///     Resolves a relationship id to a header or footer part.
    /// </summary>
    /// <param name="mainPart">The main document part.</param>
    /// <param name="id">The relationship id, or <see langword="null"/>.</param>
    /// <returns>The resolved part, or <see langword="null"/> when it is neither a header nor a footer part.</returns>
    /// <remarks>Read-only.</remarks>
    private static OpenXmlPart? TryResolvePart(MainDocumentPart mainPart, string? id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        var part = mainPart.GetPartById(id);
        return part is HeaderPart or FooterPart ? part : null;
    }

    /// <summary>
    ///     Adds one labeled subsection per surviving header or footer.
    /// </summary>
    /// <param name="sections">The section list to append to.</param>
    /// <param name="kind">The kind label (<c>Header</c> or <c>Footer</c>).</param>
    /// <param name="survivors">The distinct surviving block sequences.</param>
    /// <remarks>A single survivor is labeled by kind; multiples are numbered. Side effect: appends.</remarks>
    private static void AddLabeledSections(
        List<WordDocumentControlSection> sections, string kind, List<IReadOnlyList<WordBlock>> survivors)
    {
        for (var index = 0; index < survivors.Count; index++)
        {
            var label = survivors.Count == 1
                ? kind
                : $"{kind} ({(index + 1).ToString(CultureInfo.InvariantCulture)})";
            sections.Add(new WordDocumentControlSection(label, survivors[index]));
        }
    }

    /// <summary>
    ///     Builds the comments list from the comments part.
    /// </summary>
    /// <param name="mainPart">The main document part.</param>
    /// <returns>The comments, author-attributed, in document order.</returns>
    /// <remarks>Read-only.</remarks>
    private List<WordComment> BuildComments(MainDocumentPart mainPart)
    {
        var comments = new List<WordComment>();
        var part = mainPart.WordprocessingCommentsPart;
        if (part?.Comments is null)
        {
            return comments;
        }

        foreach (var comment in part.Comments.Elements<W.Comment>())
        {
            var inlines = new List<WordInline>();
            var images = new List<WordImageRef>();
            var pageBreak = false;
            var hadFurniture = false;
            foreach (var paragraph in comment.Elements<W.Paragraph>())
            {
                WalkContainer(paragraph, mainPart, new Stack<FieldFrame>(), inlines, images,
                    stripFurniture: false, ref pageBreak, ref hadFurniture);
            }

            comments.Add(new WordComment(NullIfBlank(comment.Author?.Value), inlines));
        }

        return comments;
    }

    /// <summary>
    ///     Builds the numbering map from a numbering id and level to whether the list is ordered.
    /// </summary>
    /// <param name="mainPart">The main document part.</param>
    /// <returns>The numbering map, empty when the document defines no numbering.</returns>
    /// <remarks>A level's format decides ordering: any format other than bullet is ordered. Read-only.</remarks>
    private static Dictionary<int, Dictionary<int, bool>> BuildNumbering(MainDocumentPart mainPart)
    {
        var result = new Dictionary<int, Dictionary<int, bool>>();
        var numbering = mainPart.NumberingDefinitionsPart?.Numbering;
        if (numbering is null)
        {
            return result;
        }

        var abstractLevels = new Dictionary<int, Dictionary<int, bool>>();
        foreach (var abstractNum in numbering.Elements<W.AbstractNum>())
        {
            var id = abstractNum.AbstractNumberId?.Value;
            if (id is null)
            {
                continue;
            }

            var levels = new Dictionary<int, bool>();
            foreach (var level in abstractNum.Elements<W.Level>())
            {
                var levelIndex = level.LevelIndex?.Value ?? 0;
                var format = level.NumberingFormat?.Val?.Value;
                levels[levelIndex] = format is not null
                    && format != W.NumberFormatValues.Bullet && format != W.NumberFormatValues.None;
            }

            abstractLevels[id.Value] = levels;
        }

        foreach (var instance in numbering.Elements<W.NumberingInstance>())
        {
            var numId = instance.NumberID?.Value;
            var abstractId = instance.GetFirstChild<W.AbstractNumId>()?.Val?.Value;
            if (numId is not null && abstractId is not null && abstractLevels.TryGetValue(abstractId.Value, out var levels))
            {
                result[numId.Value] = levels;
            }
        }

        return result;
    }

    /// <summary>
    ///     Builds the footnote index from the footnotes part.
    /// </summary>
    /// <param name="mainPart">The main document part.</param>
    /// <returns>The footnote definitions indexed by id, empty when the document has none.</returns>
    /// <remarks>Read-only.</remarks>
    private static Dictionary<int, W.Footnote> BuildFootnoteIndex(MainDocumentPart mainPart)
    {
        var result = new Dictionary<int, W.Footnote>();
        var footnotes = mainPart.FootnotesPart?.Footnotes;
        if (footnotes is null)
        {
            return result;
        }

        foreach (var footnote in footnotes.Elements<W.Footnote>())
        {
            var id = footnote.Id?.Value;

            // The separator and continuation footnotes carry non-positive ids and are not content
            if (id is > 0)
            {
                result[(int)id.Value] = footnote;
            }
        }

        return result;
    }

    /// <summary>
    ///     Reads the producer-cached page count from the extended file properties.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <returns>The page count, or <see langword="null"/> when absent or unparsable.</returns>
    /// <remarks>Open XML has no true page count; this value is producer-reported and never computed. Read-only.</remarks>
    private static int? ReadProducerPageCount(WordprocessingDocument document)
    {
        var pages = document.ExtendedFilePropertiesPart?.Properties?.Pages?.Text;
        return int.TryParse(pages, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) ? count : null;
    }

    /// <summary>
    ///     Resolves a hyperlink element to its target URL.
    /// </summary>
    /// <param name="hyperlink">The hyperlink element.</param>
    /// <param name="part">The part carrying the hyperlink relationships.</param>
    /// <returns>The target URL, or <see langword="null"/> when it cannot be resolved.</returns>
    /// <remarks>Read-only.</remarks>
    private static string? ResolveHyperlink(W.Hyperlink hyperlink, OpenXmlPartContainer part)
    {
        var id = hyperlink.Id?.Value;
        if (string.IsNullOrEmpty(id) || part is not OpenXmlPart openXmlPart)
        {
            return null;
        }

        var relationship = openXmlPart.HyperlinkRelationships.FirstOrDefault(rel => rel.Id == id);
        return relationship?.Uri?.ToString();
    }

    /// <summary>
    ///     Determines a paragraph's heading level from its style or outline level.
    /// </summary>
    /// <param name="paragraph">The paragraph.</param>
    /// <returns>The 1-based heading level, or zero when the paragraph is not a heading.</returns>
    /// <remarks>Style is authoritative; the outline level is the fallback. Pure.</remarks>
    private static int DetermineHeadingLevel(W.Paragraph paragraph)
    {
        var styleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
        if (styleId is not null)
        {
            var normalized = styleId.Replace(" ", string.Empty, StringComparison.Ordinal);
            if (string.Equals(normalized, "Title", StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            if (normalized.StartsWith("Heading", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(normalized["Heading".Length..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var level)
                && level is >= 1 and <= 9)
            {
                return level;
            }
        }

        var outline = paragraph.ParagraphProperties?.OutlineLevel?.Val?.Value;
        return outline is >= 0 and <= 8 ? outline.Value + 1 : 0;
    }

    /// <summary>
    ///     Determines whether a paragraph is a list item, and its level and ordering.
    /// </summary>
    /// <param name="paragraph">The paragraph.</param>
    /// <param name="info">The resolved list info when the paragraph is a list item.</param>
    /// <returns><see langword="true"/> when the paragraph is a numbered or bulleted list item; otherwise <see langword="false"/>.</returns>
    /// <remarks>Ordering comes from the numbering definitions; an unknown numbering defaults to bulleted. Pure.</remarks>
    private bool TryGetListInfo(W.Paragraph paragraph, out WordListInfo info)
    {
        info = new WordListInfo(0, false);
        var numbering = paragraph.ParagraphProperties?.NumberingProperties;
        var numId = numbering?.NumberingId?.Val?.Value;
        if (numId is null or 0)
        {
            return false;
        }

        var level = numbering!.NumberingLevelReference?.Val?.Value ?? 0;
        var ordered = _numbering.TryGetValue(numId.Value, out var levels)
            && levels.TryGetValue(level, out var isOrdered) && isOrdered;
        info = new WordListInfo(level, ordered);
        return true;
    }

    /// <summary>
    ///     Reports whether a table row is a header row.
    /// </summary>
    /// <param name="row">The table row.</param>
    /// <returns><see langword="true"/> when the row is marked a repeated header row; otherwise <see langword="false"/>.</returns>
    /// <remarks>Pure.</remarks>
    private static bool IsHeaderRow(W.TableRow row)
    {
        var header = row.TableRowProperties?.GetFirstChild<W.TableHeader>();
        return header is not null && (header.Val is null || header.Val.Value == W.OnOffOnlyValues.On);
    }

    /// <summary>
    ///     Reports whether a cell is a vertical-merge continuation.
    /// </summary>
    /// <param name="cell">The table cell.</param>
    /// <returns><see langword="true"/> when the cell continues a vertical merge; otherwise <see langword="false"/>.</returns>
    /// <remarks>A merge element with no value (or a continue value) continues; a restart begins a new merge. Pure.</remarks>
    private static bool IsVerticalMergeContinuation(W.TableCell cell)
    {
        var merge = cell.TableCellProperties?.VerticalMerge;
        return merge is not null && (merge.Val is null || merge.Val.Value == W.MergedCellValues.Continue);
    }

    /// <summary>
    ///     Reads the horizontal span of a cell.
    /// </summary>
    /// <param name="cell">The table cell.</param>
    /// <returns>The number of grid columns the cell spans, at least one.</returns>
    /// <remarks>Pure.</remarks>
    private static int GridSpanOf(W.TableCell cell) =>
        Math.Max(1, cell.TableCellProperties?.GridSpan?.Val?.Value ?? 1);

    /// <summary>
    ///     Reports whether a run-property on/off toggle is enabled.
    /// </summary>
    /// <param name="toggle">The on/off property, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the property is present and not turned off.</returns>
    /// <remarks>An on/off element with no value defaults to on. Pure.</remarks>
    private static bool IsOn(W.OnOffType? toggle) =>
        toggle is not null && (toggle.Val is null || toggle.Val.Value);

    /// <summary>
    ///     Reports whether an inline list carries any visible text.
    /// </summary>
    /// <param name="inlines">The inlines to inspect.</param>
    /// <returns><see langword="true"/> when any inline has non-whitespace text.</returns>
    /// <remarks>Pure.</remarks>
    private static bool HasVisibleText(IReadOnlyList<WordInline> inlines) =>
        inlines.Any(inline => !string.IsNullOrWhiteSpace(inline.Text));

    /// <summary>
    ///     Reports whether a block sequence carries any visible content.
    /// </summary>
    /// <param name="blocks">The blocks to inspect.</param>
    /// <returns><see langword="true"/> when any block has visible text, an image, or a table.</returns>
    /// <remarks>Pure.</remarks>
    private static bool HasVisibleContent(IReadOnlyList<WordBlock> blocks) =>
        blocks.Any(block => block.Kind is WordBlockKind.Image or WordBlockKind.Table
            || (block.Inlines is not null && HasVisibleText(block.Inlines)));

    /// <summary>
    ///     Reports whether a field instruction is page-numbering furniture.
    /// </summary>
    /// <param name="instruction">The field instruction text, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the instruction's keyword is a furniture field.</returns>
    /// <remarks>
    ///     Structural: the instruction's first token is matched against the furniture set, so a
    ///     cross-reference such as <c>PAGEREF</c> is not misclassified and a revision string that
    ///     happens to contain digits is never mistaken for page numbering. Pure.
    /// </remarks>
    private static bool IsFurnitureInstruction(string? instruction)
    {
        if (string.IsNullOrWhiteSpace(instruction))
        {
            return false;
        }

        var token = instruction.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return token is not null && FurnitureFieldInstructions.Contains(token);
    }

    /// <summary>
    ///     Detects a password-protected document by its OLE compound-file signature.
    /// </summary>
    /// <param name="stream">The seekable document stream, rewound to the start on return.</param>
    /// <exception cref="WordExtractionException">Thrown when the stream begins with the OLE compound-file signature.</exception>
    /// <remarks>
    ///     A password-protected <c>.docx</c> is wrapped in an OLE compound file rather than a ZIP;
    ///     detecting the signature lets this backend raise a clear message rather than the SDK's raw
    ///     package error. Read-only.
    /// </remarks>
    private static void ThrowIfPasswordProtected(Stream stream)
    {
        if (!stream.CanSeek)
        {
            return;
        }

        var header = new byte[OleCompoundFileSignature.Length];
        var read = stream.Read(header, 0, header.Length);
        stream.Position = 0;

        if (read == header.Length && header.AsSpan().SequenceEqual(OleCompoundFileSignature))
        {
            throw new WordExtractionException(
                "The document is password-protected; DocDown cannot open encrypted Word documents.");
        }
    }

    /// <summary>
    ///     Normalizes a blank metadata value to <see langword="null"/>.
    /// </summary>
    /// <param name="value">The metadata value, which may be blank.</param>
    /// <returns>The trimmed value, or <see langword="null"/> when it carries no information.</returns>
    /// <remarks>Unknown and empty are different facts, so a blank value is reported as absent. Pure.</remarks>
    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    ///     One frame of the complex-field state stack.
    /// </summary>
    /// <remarks>Tracks a field's accumulated instruction and whether its result is being emitted.</remarks>
    private sealed class FieldFrame
    {
        /// <summary>Gets the accumulated field instruction text.</summary>
        public StringBuilder Instruction { get; } = new();

        /// <summary>Gets or sets a value indicating whether the field's result portion is active.</summary>
        public bool InResult { get; set; }

        /// <summary>Gets or sets a value indicating whether the field is page-numbering furniture.</summary>
        public bool Furniture { get; set; }
    }
}
