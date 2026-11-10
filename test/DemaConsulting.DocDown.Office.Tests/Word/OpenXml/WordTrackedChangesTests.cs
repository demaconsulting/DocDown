using DemaConsulting.DocDown.Office.Tests.Word.TestData;
using DemaConsulting.DocDown.Word.Markdown;
using DemaConsulting.DocDown.Word.OpenXml;
using DocumentFormat.OpenXml.Packaging;

namespace DemaConsulting.DocDown.Office.Tests.Word.OpenXml;

/// <summary>
///     Unit tests for tracked-change (redline) handling in <see cref="WordOpenXmlReader"/>,
///     <see cref="WordRevisionScanner"/>, and <see cref="WordMarkdownWriter"/>, over small
///     synthetic documents with invented text.
/// </summary>
public class WordTrackedChangesTests
{
    /// <summary>
    ///     Proves the scanner finds every revision with its author, date, and location.
    /// </summary>
    [Fact]
    public void WordRevisionScanner_Scan_RedlineDocument_FindsRevisionsWithAuthorDateAndLocation()
    {
        var revisions = Scan(DocxFixtures.DocumentWithRedline());

        Assert.Equal(3, revisions.Count);
        Assert.Equal(WordRevisionKind.Inserted, revisions[0].Kind);
        Assert.Equal("Alice", revisions[0].Author);
        Assert.Equal(new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc).Date, revisions[0].Date!.Value.Date);
        Assert.Equal("added words", revisions[0].Snippet);
        Assert.Equal(WordRevisionKind.Deleted, revisions[1].Kind);
        Assert.Equal("Bob", revisions[1].Author);
        Assert.Equal("dropped words", revisions[1].Snippet);
        Assert.Contains("Scope", revisions[0].Location, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves a document without revisions scans to nothing.
    /// </summary>
    [Fact]
    public void WordRevisionScanner_Scan_CleanDocument_FindsNothing() =>
        Assert.Empty(Scan(DocxFixtures.CleanDocument()));

    /// <summary>
    ///     Proves a formatting-only change is counted as a formatting change.
    /// </summary>
    [Fact]
    public void WordRevisionScanner_Scan_FormattingChange_IsCountedAsFormatting()
    {
        var revision = Assert.Single(Scan(DocxFixtures.DocumentWithFormattingChangeOnly()));

        Assert.Equal(WordRevisionKind.FormattingChanged, revision.Kind);
        Assert.Equal("Carol", revision.Author);
    }

    /// <summary>
    ///     Proves a move is counted at both ends and an inserted paragraph mark is counted.
    /// </summary>
    [Fact]
    public void WordRevisionScanner_Scan_MoveAndInsertedMark_CountsEachElement()
    {
        var kinds = Scan(DocxFixtures.DocumentWithMoveAndInsertedMark()).Select(revision => revision.Kind).ToList();

        Assert.Contains(WordRevisionKind.MovedFrom, kinds);
        Assert.Contains(WordRevisionKind.MovedTo, kinds);
        Assert.Contains(WordRevisionKind.ParagraphMarkInserted, kinds);
    }

    /// <summary>
    ///     Proves the scan covers headers and footnote bodies with distinct locations.
    /// </summary>
    [Fact]
    public void WordRevisionScanner_Scan_HeaderAndFootnote_ReportsTheirLocations()
    {
        var revisions = Read(DocxFixtures.DocumentWithHeaderAndFootnoteRevisions()).RevisionList;

        Assert.Equal(2, revisions.Count);
        Assert.Contains(revisions, revision => revision.Part == WordRevisionPart.Header
            && revision.Location.StartsWith("Header: paragraph 1", StringComparison.Ordinal));
        Assert.Contains(revisions, revision => revision.Part == WordRevisionPart.Footnote
            && revision.Location.StartsWith("Footnote [^1]: paragraph 1", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Proves row revisions are located by table row.
    /// </summary>
    [Fact]
    public void WordRevisionScanner_Scan_TableRows_ReportsRowLocations()
    {
        var revisions = Scan(DocxFixtures.DocumentWithRevisedTableRows());

        Assert.Contains(revisions, revision => revision.Kind == WordRevisionKind.TableDeleted
            && revision.Location.Contains("table 1, row 2", StringComparison.Ordinal));
        Assert.Contains(revisions, revision => revision.Kind == WordRevisionKind.TableInserted
            && revision.Location.Contains("table 1, row 3", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Proves a redline document renders insertions and deletions as markup by default.
    /// </summary>
    [Fact]
    public void WordOpenXmlReader_Read_Redline_RendersInsertionAndDeletionMarkup()
    {
        var markdown = Render(DocxFixtures.DocumentWithRedline());

        Assert.Contains("# Scope", markdown, StringComparison.Ordinal);
        Assert.Contains("{++added words++}", markdown, StringComparison.Ordinal);
        Assert.Contains("{--dropped words--}", markdown, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves strike, double strike, and underline are rendered in redline mode, but not inside a
    ///     tracked insertion.
    /// </summary>
    [Fact]
    public void WordOpenXmlReader_Read_Redline_RendersStrikeAndUnderlineConvention()
    {
        var markdown = Render(DocxFixtures.DocumentWithRedline());

        Assert.Contains("<u>underlined</u>", markdown, StringComparison.Ordinal);
        Assert.Contains("~~struck~~", markdown, StringComparison.Ordinal);
        Assert.Contains("<del>double</del>", markdown, StringComparison.Ordinal);
        Assert.Contains("{++quiet++}", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("<u>quiet", markdown, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves the accept option yields the accepted view with no markup but the same count.
    /// </summary>
    [Fact]
    public void WordOpenXmlReader_Read_RedlineWithAcceptOption_RendersAcceptedViewAndKeepsCount()
    {
        var model = Read(DocxFixtures.DocumentWithRedline(), accept: true);
        var markdown = WordMarkdownWriter.Write(model, new Dictionary<string, string>());

        Assert.Contains("added words", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("dropped words", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("{++", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("<u>", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("~~", markdown, StringComparison.Ordinal);
        Assert.Equal(3, model.TrackedChangeCount);
        Assert.True(model.RevisionsAccepted);
    }

    /// <summary>
    ///     Proves the accepted view of a document with no revisions is identical to the default view.
    /// </summary>
    [Fact]
    public void WordOpenXmlReader_Read_CleanDocument_IsIdenticalWithOrWithoutAcceptOption()
    {
        var bytes = DocxFixtures.CleanDocument();
        var plain = WordMarkdownWriter.Write(Read(bytes), new Dictionary<string, string>());
        var accepted = WordMarkdownWriter.Write(Read(bytes, accept: true), new Dictionary<string, string>());

        Assert.Equal(plain, accepted);
        Assert.DoesNotContain("{++", plain, StringComparison.Ordinal);
        Assert.DoesNotContain("<u>", plain, StringComparison.Ordinal);
        Assert.Equal(0, Read(bytes).TrackedChangeCount);
    }

    /// <summary>
    ///     Proves tracked changes are rendered as accepted view when requested (the superseded
    ///     behavior is retained behind the accept option).
    /// </summary>
    [Fact]
    public void WordOpenXmlReader_Read_TrackedChangesWithAcceptOption_DropsDeletedTextAndCounts()
    {
        var model = Read(DocxFixtures.DocumentWithTrackedChanges(), accept: true);

        var text = string.Concat(model.Body.Where(block => block.Inlines is not null)
            .SelectMany(block => block.Inlines!).Select(inline => inline.Text));
        Assert.Contains("inserted-text", text, StringComparison.Ordinal);
        Assert.DoesNotContain("removed-text", text, StringComparison.Ordinal);
        Assert.Equal(2, model.TrackedChangeCount);
    }

    /// <summary>
    ///     Proves a deleted paragraph mark merges the two paragraphs in both modes, and the heading
    ///     style of the first does not swallow the second.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WordOpenXmlReader_Read_DeletedParagraphMark_MergesWithNextParagraph(bool accept)
    {
        var model = Read(DocxFixtures.DocumentWithDeletedParagraphMark(), accept);

        Assert.DoesNotContain(model.Body, block => block.Kind == WordBlockKind.Heading);
        var merged = Assert.Single(model.Body, block => block.Inlines is not null
            && string.Concat(block.Inlines.Select(inline => inline.Text)).Contains("First half", StringComparison.Ordinal));
        Assert.Equal("First half second half", string.Concat(merged.Inlines!.Select(inline => inline.Text)));
        Assert.Contains(model.Body, block => block.Inlines is not null
            && string.Concat(block.Inlines.Select(inline => inline.Text)) == "Unrelated paragraph.");
    }

    /// <summary>
    ///     Proves a move renders as a deletion at the source and an insertion at the destination.
    /// </summary>
    [Fact]
    public void WordOpenXmlReader_Read_Move_RendersDeletionThenInsertion()
    {
        var markdown = Render(DocxFixtures.DocumentWithMoveAndInsertedMark());

        var deleted = markdown.IndexOf("{--relocated clause--}", StringComparison.Ordinal);
        var inserted = markdown.IndexOf("{++relocated clause++}", StringComparison.Ordinal);
        Assert.True(deleted >= 0 && inserted > deleted, "deletion at source precedes insertion at destination");
    }

    /// <summary>
    ///     Proves a formatting-only change leaves the content unmarked yet counted.
    /// </summary>
    [Fact]
    public void WordOpenXmlReader_Read_FormattingChangeOnly_CountsButDoesNotMarkContent()
    {
        var model = Read(DocxFixtures.DocumentWithFormattingChangeOnly());
        var markdown = WordMarkdownWriter.Write(model, new Dictionary<string, string>());

        Assert.Equal(1, model.TrackedChangeCount);
        Assert.DoesNotContain("{++", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("{--", markdown, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves a tracked row deletion and insertion are marked, and the deleted row is dropped on accept.
    /// </summary>
    [Fact]
    public void WordOpenXmlReader_Read_RevisedTableRows_MarksRowsAndDropsDeletedOnAccept()
    {
        var markup = Render(DocxFixtures.DocumentWithRevisedTableRows());
        Assert.Contains("{--Gamma--}", markup, StringComparison.Ordinal);
        Assert.Contains("{++Delta++}", markup, StringComparison.Ordinal);

        var accepted = WordMarkdownWriter.Write(
            Read(DocxFixtures.DocumentWithRevisedTableRows(), accept: true), new Dictionary<string, string>());
        Assert.DoesNotContain("Gamma", accepted, StringComparison.Ordinal);
        Assert.Contains("Delta", accepted, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves a footnote body revision is marked and the marker's own state does not leak in.
    /// </summary>
    [Fact]
    public void WordOpenXmlReader_Read_FootnoteRevision_MarksFootnoteBody()
    {
        var markdown = Render(DocxFixtures.DocumentWithHeaderAndFootnoteRevisions());

        Assert.Contains("{--old note--}", markdown, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves every kind of place gets a location with part, heading path, and position.
    /// </summary>
    [Fact]
    public void WordRevisionScanner_Scan_LocatedRevisions_DescribesPartHeadingPathAndPosition()
    {
        var revisions = Read(DocxFixtures.DocumentWithLocatedRevisions()).RevisionList;

        Assert.Equal(8, revisions.Count);
        Assert.Equal(
            "Body: before the first heading, paragraph 1 — \"Preface early note\"", revisions[0].Location);
        Assert.Equal(
            "Body: Alpha, paragraph 2 — \"Alpha second gone claim\"", revisions[1].Location);
        Assert.Equal("Body: Alpha > Beta, paragraph 1 — \"Beta text new\"", revisions[2].Location);
        Assert.Equal("Body: Alpha > Beta, table 1, row 1, column 2 — \"Cell edit\"", revisions[3].Location);
        Assert.Equal("Body: Retired heading (heading)", revisions[4].Location);
        Assert.Equal("Body: Retired heading, paragraph 1 — \"Formatted words\"", revisions[5].Location);
        Assert.Equal(WordRevisionKind.FormattingChanged, revisions[5].Kind);
        Assert.Equal(WordRevisionPart.Footer, revisions[6].Part);
        Assert.Equal("Footer: paragraph 1 — \"old footer\"", revisions[6].Location);
        Assert.Equal("Footnote [^1]: paragraph 1 — \"note addition\"", revisions[7].Location);
        Assert.Equal(WordRevisionPart.Footnote, revisions[7].Part);
        Assert.All(revisions.Take(6), revision => Assert.Equal(WordRevisionPart.Body, revision.Part));
    }

    /// <summary>
    ///     Proves the heading path in a location matches a heading that content.md renders, and that
    ///     in the accepted view a fully deleted heading is no longer a heading.
    /// </summary>
    [Fact]
    public void WordRevisionScanner_Scan_DeletedHeading_FollowsTheRenderedMode()
    {
        var bytes = DocxFixtures.DocumentWithLocatedRevisions();
        Assert.Contains("# {--Retired heading--}", Render(bytes), StringComparison.Ordinal);

        var accepted = Read(bytes, accept: true).RevisionList;

        Assert.Equal("Body: Alpha > Beta, after paragraph 1", accepted[4].Location);
        Assert.Equal("Body: Alpha > Beta, paragraph 2 — \"Formatted words\"", accepted[5].Location);
    }

    /// <summary>
    ///     Proves table row numbers in locations match the table as content.md renders it: deleted rows
    ///     count in markup mode and are omitted in the accepted view.
    /// </summary>
    [Fact]
    public void WordRevisionScanner_Scan_DeletedRow_NumbersRowsAsRendered()
    {
        var bytes = DocxFixtures.DocumentWithDeletedRowBeforeEdit();

        var markup = Read(bytes).RevisionList;
        Assert.Contains("| Gear | Ready {++soon++} |", Render(bytes), StringComparison.Ordinal);
        Assert.Contains(markup, revision => revision.Location.StartsWith(
            "Body: before the first heading, table 1, row 4, column 2", StringComparison.Ordinal));
        Assert.Contains(markup, revision => revision.Kind == WordRevisionKind.TableDeleted
            && revision.Location.StartsWith("Body: before the first heading, table 1, row 2", StringComparison.Ordinal));

        var acceptedModel = Read(bytes, accept: true);
        var accepted = acceptedModel.RevisionList;
        var acceptedMarkdown = WordMarkdownWriter.Write(acceptedModel, new Dictionary<string, string>());
        Assert.DoesNotContain("Widget", acceptedMarkdown, StringComparison.Ordinal);
        Assert.Contains("| Gear | Ready soon |", acceptedMarkdown, StringComparison.Ordinal);
        Assert.Contains(accepted, revision => revision.Location.StartsWith(
            "Body: before the first heading, table 1, row 3, column 2", StringComparison.Ordinal));
        Assert.Contains(accepted, revision => revision.Kind == WordRevisionKind.TableDeleted
            && revision.Location.StartsWith(
                "Body: before the first heading, table 1, deleted row (omitted from the accepted view), after row 1",
                StringComparison.Ordinal));
    }

    /// <summary>
    ///     Proves a table whose only content is tracked-deleted is omitted from the accepted view and
    ///     does not consume a table ordinal, while markup mode still numbers it.
    /// </summary>
    [Fact]
    public void WordRevisionScanner_Scan_FullyDeletedTable_DoesNotShiftAcceptedOrdinals()
    {
        var bytes = DocxFixtures.DocumentWithFullyDeletedTableBeforeEditedTable();

        var accepted = Read(bytes, accept: true).RevisionList;
        Assert.Contains(accepted, revision => revision.Location.Contains("empty table (omitted from content.md)", StringComparison.Ordinal));
        Assert.Contains(accepted, revision => revision.Location.Contains("table 1, row 1, column 1", StringComparison.Ordinal));
        Assert.DoesNotContain(accepted, revision => revision.Location.Contains("table 2", StringComparison.Ordinal));

        var markup = Read(bytes).RevisionList;
        Assert.Contains(markup, revision => revision.Location.Contains("table 2, row 1, column 1", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Proves a deleted paragraph mark before a table does not merge the carried text into the
    ///     paragraph after the table: the reader emits it standalone, so the following paragraph is 2.
    /// </summary>
    [Fact]
    public void WordRevisionScanner_Scan_DeletedMarkBeforeTable_DoesNotMergeAcrossTable()
    {
        var bytes = DocxFixtures.DocumentWithDeletedMarkBeforeTable();

        var markdown = Render(bytes);
        Assert.Contains("Lead\\-in words\n\n| Cell text |", markdown, StringComparison.Ordinal);
        var revision = Assert.Single(Read(bytes, accept: true).RevisionList,
            candidate => candidate.Kind == WordRevisionKind.Inserted);
        Assert.Contains("paragraph 2", revision.Location, StringComparison.Ordinal);
        Assert.DoesNotContain("Lead-in", revision.Location, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves an image-only table, which the reader omits, does not consume a table ordinal.
    /// </summary>
    [Fact]
    public void WordRevisionScanner_Scan_ImageOnlyTable_DoesNotShiftTableOrdinals()
    {
        var bytes = DocxFixtures.DocumentWithImageOnlyTableBeforeEditedTable();

        foreach (var accept in new[] { false, true })
        {
            var revisions = Read(bytes, accept).RevisionList;
            Assert.Contains(revisions, revision => revision.Location.Contains("table 1, row 1, column 1", StringComparison.Ordinal));
            Assert.DoesNotContain(revisions, revision => revision.Location.Contains("table 2", StringComparison.Ordinal));
        }
    }

    /// <summary>
    ///     Proves a deleted paragraph mark in a footer merges its paragraph into the next one.
    /// </summary>
    [Fact]
    public void WordOpenXmlReader_Read_FooterDeletedParagraphMark_MergesParagraphs()
    {
        var markdown = Render(DocxFixtures.DocumentWithMergedFooterParagraphs());

        Assert.Contains("Left part right part", markdown, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves header and footer locations use the labels content.md emits for their sections.
    /// </summary>
    [Fact]
    public void WordRevisionScanner_Scan_TwoFooters_UsesContentMarkdownLabels()
    {
        var bytes = DocxFixtures.DocumentWithTwoRevisedFooters();
        var markdown = Render(bytes);
        Assert.Contains("### Footer \\(1\\)", markdown, StringComparison.Ordinal);
        Assert.Contains("### Footer \\(2\\)", markdown, StringComparison.Ordinal);

        var revisions = Read(bytes).RevisionList;

        Assert.Contains(revisions, revision => revision.Location.StartsWith(
            "Footer (1): paragraph 1", StringComparison.Ordinal));
        Assert.Contains(revisions, revision => revision.Location.StartsWith(
            "Footer (2): paragraph 1", StringComparison.Ordinal));
        Assert.DoesNotContain(revisions, revision => revision.Location.Contains(".xml", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Proves page-numbering-only footer paragraphs consume no ordinal, matching the reader.
    /// </summary>
    [Fact]
    public void WordRevisionScanner_Scan_PageNumberOnlyFooterParagraphs_AreNotCounted()
    {
        var bytes = DocxFixtures.DocumentWithPageNumberFooterBeforeRevision();
        var markdown = Render(bytes);
        Assert.Contains("### Footer\n\nDraft footer {++note++}", markdown.Replace("\r\n", "\n"), StringComparison.Ordinal);
        Assert.DoesNotContain("PAGE", markdown, StringComparison.Ordinal);

        var revision = Assert.Single(Read(bytes).RevisionList);

        Assert.StartsWith("Footer: paragraph 1", revision.Location, StringComparison.Ordinal);
        Assert.DoesNotContain("7", revision.Location, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves the scanner alone, given no labels, names the part file and says it is not shown.
    /// </summary>
    [Fact]
    public void WordRevisionScanner_Scan_WithoutLabels_NamesPartFile()
    {
        var revision = Assert.Single(Scan(DocxFixtures.DocumentWithTwoRevisedFooters()), item => item.Location.Contains("one", StringComparison.Ordinal));

        Assert.StartsWith("Footer (footer", revision.Location, StringComparison.Ordinal);
        Assert.Contains("not shown in content.md", revision.Location, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves a table of contents is omitted, counted as one region, and its revisions are
    ///     flagged and located with a distinct label.
    /// </summary>
    [Fact]
    public void WordOpenXmlReader_Read_TrackedTableOfContents_IsOmittedButItsRevisionsAreCounted()
    {
        var model = Read(DocxFixtures.DocumentWithTrackedTableOfContents());
        var markdown = WordMarkdownWriter.Write(model, new Dictionary<string, string>());

        Assert.DoesNotContain("Overview entry", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("Contents", markdown, StringComparison.Ordinal);
        Assert.Contains("# Overview", markdown, StringComparison.Ordinal);
        Assert.Equal(1, model.TocRegionsSkipped);
        Assert.Equal(4, model.TocParagraphsSkipped);
        Assert.Equal(3, model.TrackedChangeCount);
        Assert.Equal(2, model.RevisionList.Count(revision => revision.InToc));
        Assert.Contains(model.RevisionList, revision => revision.InToc
            && revision.Location == "Body: table of contents (omitted from content.md), entry 3 — \"Retired entry\"");

        var real = model.RevisionList.Single(revision => !revision.InToc);
        Assert.Equal("Body: Overview, paragraph 1 — \"Real text added\"", real.Location);
    }

    /// <summary>
    ///     Proves style-only contents paragraphs are omitted, and that an ordinary content control is
    ///     read through rather than dropped.
    /// </summary>
    [Fact]
    public void WordOpenXmlReader_Read_StyleOnlyTocAndContentControl_OmitsTocAndKeepsControlText()
    {
        var model = Read(DocxFixtures.DocumentWithStyleOnlyTocAndContentControl());
        var markdown = WordMarkdownWriter.Write(model, new Dictionary<string, string>());

        Assert.DoesNotContain("Alpha entry", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("Beta entry", markdown, StringComparison.Ordinal);
        Assert.Contains("Inside a content control", markdown, StringComparison.Ordinal);
        Assert.Equal(1, model.TocRegionsSkipped);
        Assert.Equal(2, model.TocParagraphsSkipped);
    }

    /// <summary>
    ///     Proves a document without a table of contents reports none skipped.
    /// </summary>
    [Fact]
    public void WordOpenXmlReader_Read_DocumentWithoutToc_ReportsNoneSkipped()
    {
        var model = Read(DocxFixtures.DocumentWithRedline());

        Assert.Equal(0, model.TocRegionsSkipped);
        Assert.Equal(0, model.TocParagraphsSkipped);
    }

    /// <summary>
    ///     Proves a deleted field's instruction is never rendered while its deleted result is marked.
    /// </summary>
    [Fact]
    public void WordOpenXmlReader_Read_DeletedField_RendersResultOnlyAsDeletion()
    {
        var markdown = Render(DocxFixtures.DocumentWithDeletedField());

        Assert.Contains("{--field result--}", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("hidden-bookmark", markdown, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves <c>w:object</c>, <c>w:pict</c>, and <c>mc:AlternateContent</c> pictures are collected
    ///     once each rather than dropped.
    /// </summary>
    [Fact]
    public void WordOpenXmlReader_Read_LegacyObjects_CollectsEachPreviewImage()
    {
        var model = Read(DocxFixtures.DocumentWithLegacyObjectImages());

        var images = model.Body.Where(block => block.Kind == WordBlockKind.Image).Select(block => block.Image!).ToList();
        Assert.Equal(3, images.Count);
        Assert.Contains(images, image => image.MediaType.Contains("emf", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Proves a <c>w:pict</c> preview in a header or footer is resolved against that part and kept.
    /// </summary>
    [Fact]
    public void WordOpenXmlReader_Read_HeaderAndFooterPict_CollectsPreviewImages()
    {
        var model = Read(DocxFixtures.DocumentWithHeaderAndFooterPictPreviews());

        var images = model.DocumentControl.SelectMany(section => section.Blocks)
            .Where(block => block.Kind == WordBlockKind.Image).Select(block => block.Image!).ToList();
        Assert.Equal(2, images.Count);
        Assert.Contains(images, image => image.MediaType.Contains("emf", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Proves a deleted footnote reference takes no number in the accepted view, and the
    ///     locations agree with the markers content.md uses in each view.
    /// </summary>
    [Fact]
    public void WordRevisionScanner_Scan_DeletedFootnoteReference_NumbersFootnotesAsRendered()
    {
        var bytes = DocxFixtures.DocumentWithDeletedFootnoteReferenceBeforeSurvivor();

        var accepted = Read(bytes, accept: true);
        var acceptedMarkdown = WordMarkdownWriter.Write(accepted, new Dictionary<string, string>());
        Assert.Contains("Claim[^1]", acceptedMarkdown, StringComparison.Ordinal);
        Assert.DoesNotContain("[^2]", acceptedMarkdown, StringComparison.Ordinal);
        Assert.Contains(accepted.RevisionList, revision => revision.Location.StartsWith(
            "Footnote [^1]:", StringComparison.Ordinal));

        var markup = Read(bytes);
        var markupMarkdown = WordMarkdownWriter.Write(markup, new Dictionary<string, string>());
        Assert.Contains("[^2]", markupMarkdown, StringComparison.Ordinal);
        Assert.Contains(markup.RevisionList, revision => revision.Location.StartsWith(
            "Footnote [^2]:", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Proves the accepted view keeps a deleted cell's column position but does not quote its
    ///     deleted text, while markup mode still quotes it.
    /// </summary>
    [Fact]
    public void WordRevisionScanner_Scan_DeletedCell_AcceptedViewDoesNotQuoteDeletedText()
    {
        var bytes = DocxFixtures.DocumentWithDeletedCell();

        var accepted = Read(bytes, accept: true).RevisionList;
        var cellDeletion = Assert.Single(accepted);
        Assert.Contains("column 2", cellDeletion.Location, StringComparison.Ordinal);
        Assert.DoesNotContain("confidential-old-value", cellDeletion.Location, StringComparison.Ordinal);

        var markup = Read(bytes).RevisionList;
        Assert.Contains("confidential-old-value", Assert.Single(markup).Location, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves consecutive runs of one revision kind share one marker pair and that raw and plain
    ///     runs are rendered unchanged.
    /// </summary>
    [Fact]
    public void WordMarkdownWriter_RenderInlines_ConsecutiveRevisionRuns_ShareOneMarkerPair()
    {
        WordInline[] inlines =
        [
            new("a ", Revision: WordInlineRevision.Inserted),
            new("b", Bold: true, Revision: WordInlineRevision.Inserted),
            new(" plain"),
            new("gone", Revision: WordInlineRevision.Deleted)
        ];

        Assert.Equal("{++a **b**++} plain{--gone--}", WordMarkdownWriter.RenderInlines(inlines));
    }

    /// <summary>
    ///     Proves literal document text that looks like a marker is escaped and cannot forge one.
    /// </summary>
    [Fact]
    public void WordMarkdownWriter_RenderInlines_LiteralMarkerText_IsEscaped()
    {
        var rendered = WordMarkdownWriter.RenderInlines([new WordInline("{++not a change++}")]);

        Assert.DoesNotContain("{++", rendered, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves strike, double strike, and underline nest around emphasis and inside a link.
    /// </summary>
    [Fact]
    public void WordMarkdownWriter_RenderInlines_StrikeAndUnderline_WrapText()
    {
        var rendered = WordMarkdownWriter.RenderInlines(
            [new WordInline("x", Strike: true, DoubleStrike: true, Underline: true)]);

        Assert.Equal("<u><del>~~x~~</del></u>", rendered);
    }

    /// <summary>
    ///     Proves a symbol in a tracked insertion and deletion renders inside the redline markers, is
    ///     kept or dropped by the accepted view, and reaches the revision snippets.
    /// </summary>
    [Fact]
    public void WordOpenXmlReader_Read_SymbolInTrackedChanges_RendersMarkupAndSnippets()
    {
        var bytes = DocxFixtures.DocumentWithSymbolInTrackedChanges();

        var markdown = Render(bytes);
        Assert.Contains("{++\u2022++}", markdown, StringComparison.Ordinal);
        Assert.Contains("{--\u2713--}", markdown, StringComparison.Ordinal);

        var accepted = WordMarkdownWriter.Write(Read(bytes, accept: true), new Dictionary<string, string>());
        Assert.Contains("Start \u2022 middle", accepted, StringComparison.Ordinal);
        Assert.DoesNotContain("\u2713", accepted, StringComparison.Ordinal);

        var revisions = Scan(bytes);
        Assert.Contains(revisions, revision => revision.Kind == WordRevisionKind.Inserted && revision.Snippet == "\u2022");
        Assert.Contains(revisions, revision => revision.Kind == WordRevisionKind.Deleted && revision.Snippet == "\u2713");
    }

    /// <summary>
    ///     Reads a document into a model.
    /// </summary>
    /// <param name="bytes">The document bytes.</param>
    /// <param name="accept">Whether to request the accepted view.</param>
    /// <returns>The model.</returns>
    private static WordDocumentModel Read(byte[] bytes, bool accept = false)
    {
        using var stream = new MemoryStream(bytes);
        return new WordOpenXmlReader(accept).Read(stream);
    }

    /// <summary>
    ///     Reads a document and renders its markdown with default options.
    /// </summary>
    /// <param name="bytes">The document bytes.</param>
    /// <returns>The markdown.</returns>
    private static string Render(byte[] bytes) =>
        WordMarkdownWriter.Write(Read(bytes), new Dictionary<string, string>());

    /// <summary>
    ///     Scans a document for tracked changes.
    /// </summary>
    /// <param name="bytes">The document bytes.</param>
    /// <returns>The revisions found.</returns>
    private static IReadOnlyList<WordRevision> Scan(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var document = WordprocessingDocument.Open(stream, false);
        return WordRevisionScanner.Scan(document.MainDocumentPart!);
    }
}
