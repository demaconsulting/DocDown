using DemaConsulting.DocDown.Core;
using DemaConsulting.DocDown.Pdf.Tests.TestData;
using DemaConsulting.DocDown.TestSupport;

namespace DemaConsulting.DocDown.Pdf.Tests;

/// <summary>
///     Unit tests for the PDF annotation extractor, proving which annotations count as reviewer
///     commentary, how an author is recovered from the raw annotation dictionary, and that a page
///     carrying no comments produces none.
/// </summary>
/// <remarks>
///     These tests drive the internal extractor directly against a fixture's pages, so the inclusion
///     decision can be inspected where it is made rather than inferred from the rendered
///     <c>review-comments.md</c>. The inclusion rule is the unit's whole substance, so each scenario
///     isolates one of its halves: the type table, or the requirement that an annotation carry text.
/// </remarks>
public class PdfAnnotationExtractorTests
{
    /// <summary>Gets the ambient test cancellation token so the extraction stays responsive to cancellation.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Proves an annotation's text, author, and page all reach the result.
    /// </summary>
    [Fact]
    public void PdfAnnotationExtractor_Extract_NamedAnnotation_ReportsBodyAuthorAndPage()
    {
        // Arrange: a document whose first annotation is a named sticky note
        var sink = new RecordingSink();

        // Act: read its annotations
        var comments = Extract(PdfFixtures.WithAnnotations(), sink);

        // Assert: the remark arrives verbatim, attributed to the person and the page it came from
        var first = comments[0];
        Assert.Equal("Section 2 needs a citation.", first.Body);
        Assert.Equal("Alice Reviewer", first.Author);
        Assert.Equal(1, first.PageNumber);
    }

    /// <summary>
    ///     Proves an annotation that names no author yields no author rather than a placeholder.
    /// </summary>
    [Fact]
    public void PdfAnnotationExtractor_Extract_AnnotationWithoutTitleEntry_ReportsNullAuthor()
    {
        // Arrange: the fixture's second annotation has no /T entry at all
        var sink = new RecordingSink();

        // Act: read its annotations
        var comments = Extract(PdfFixtures.WithAnnotations(), sink);

        // Assert: the comment is kept, but nothing is invented about who wrote it
        var unattributed = Assert.Single(comments, comment => comment.Body == "Typo in the second paragraph.");
        Assert.Null(unattributed.Author);
    }

    /// <summary>
    ///     Proves an author stored as a hexadecimal string is read as text.
    /// </summary>
    /// <remarks>
    ///     A PDF may hold the title entry as either a literal or a hexadecimal string, and a reader
    ///     that handled only the first would silently drop attributions that are present in the file.
    /// </remarks>
    [Fact]
    public void PdfAnnotationExtractor_Extract_HexadecimalAuthorString_ReportsDecodedAuthor()
    {
        // Arrange: the fixture's third annotation stores its author as <426F6220486578>
        var sink = new RecordingSink();

        // Act: read its annotations
        var comments = Extract(PdfFixtures.WithAnnotations(), sink);

        // Assert: the name is decoded rather than rendered as its hexadecimal digits
        var hexNamed = Assert.Single(comments, comment => comment.Body == "Check the table alignment.");
        Assert.Equal("Bob Hex", hexNamed.Author);
    }

    /// <summary>
    ///     Proves a body padded with leading and trailing spaces reaches the result verbatim.
    /// </summary>
    /// <remarks>
    ///     The blankness check must reject only a whitespace-only body, never trim a body that is
    ///     merely padded: <c>manifest.json</c> promises the comment text as the document records it,
    ///     the same promise every other backend keeps.
    /// </remarks>
    [Fact]
    public void PdfAnnotationExtractor_Extract_PaddedBody_ReportsBodyVerbatim()
    {
        // Arrange: a document whose one annotation's /Contents carries leading and trailing spaces
        var sink = new RecordingSink();

        // Act: read its annotations
        var comments = Extract(PdfFixtures.WithPaddedAnnotationBody(), sink);

        // Assert: the spacing survives exactly as the document recorded it
        var padded = Assert.Single(comments);
        Assert.Equal("  Leave the spacing exactly as typed.  ", padded.Body);
    }

    /// <summary>
    ///     Proves a non-commentary annotation is excluded even though it carries text.
    /// </summary>
    /// <remarks>
    ///     This is the scenario that makes the type table load-bearing: the link's text would pass the
    ///     content rule on its own, so its absence can only be explained by the type decision.
    /// </remarks>
    [Fact]
    public void PdfAnnotationExtractor_Extract_LinkAnnotationWithContent_IsExcludedByType()
    {
        // Arrange: the fixture carries a /Link annotation whose /Contents is non-empty
        var sink = new RecordingSink();

        // Act: read its annotations
        var comments = Extract(PdfFixtures.WithAnnotations(), sink);

        // Assert: navigation machinery never becomes a review comment
        Assert.DoesNotContain(comments, comment => comment.Body == "Jump to the appendix.");
    }

    /// <summary>
    ///     Proves a markup annotation with no remark attached is excluded.
    /// </summary>
    [Fact]
    public void PdfAnnotationExtractor_Extract_HighlightWithoutContent_IsExcluded()
    {
        // Arrange: the fixture carries a highlight whose /Contents is empty
        var sink = new RecordingSink();

        // Act: read its annotations
        var comments = Extract(PdfFixtures.WithAnnotations(), sink);

        // Assert: nothing empty is emitted, so a bare reading mark invents no comment
        Assert.DoesNotContain(comments, comment => string.IsNullOrWhiteSpace(comment.Body));
        Assert.Equal(5, comments.Count);
    }

    /// <summary>
    ///     Proves a markup annotation that does carry a remark is included.
    /// </summary>
    /// <remarks>
    ///     Paired with the empty-highlight scenario, this shows the content rule — not the type —
    ///     decides between two annotations of the same type, which is exactly the documented design.
    /// </remarks>
    [Fact]
    public void PdfAnnotationExtractor_Extract_HighlightWithContent_IsIncluded()
    {
        // Arrange: the fixture carries a highlight with a remark typed into it
        var sink = new RecordingSink();

        // Act: read its annotations
        var comments = Extract(PdfFixtures.WithAnnotations(), sink);

        // Assert: the remark is kept, attributed to its own author
        var highlighted = Assert.Single(comments, comment => comment.Body == "This claim needs evidence.");
        Assert.Equal("Carol Editor", highlighted.Author);
    }

    /// <summary>
    ///     Proves a popup with no text of its own does not duplicate its parent's remark.
    /// </summary>
    [Fact]
    public void PdfAnnotationExtractor_Extract_EmptyPopup_IsExcluded()
    {
        // Arrange: the fixture's popup window carries no /Contents of its own
        var sink = new RecordingSink();

        // Act: read its annotations
        var comments = Extract(PdfFixtures.WithAnnotations(), sink);

        // Assert: the first page contributes four comments, none of them an empty window
        Assert.Equal(4, comments.Count(comment => comment.PageNumber == 1));
    }

    /// <summary>
    ///     Characterizes what happens when a producer copies a parent annotation's text into the
    ///     popup that displays it: the remark is reported twice.
    /// </summary>
    /// <remarks>
    ///     This test asserts the current behavior rather than a desired one, so the limitation is
    ///     reviewable instead of merely undiscovered. Deduplicating would mean resolving the popup's
    ///     <c>/Parent</c> through an indirect reference, which this unit does not do, and matching on
    ///     text would silently drop a reviewer who genuinely wrote the same words twice. Duplicating
    ///     loses nothing a reviewer wrote, which is the safer of the two errors; if that judgment
    ///     ever changes, this test is where the change becomes visible.
    /// </remarks>
    [Fact]
    public void PdfAnnotationExtractor_Extract_PopupDuplicatingParent_ReportsBothEntries()
    {
        // Arrange: a page whose popup repeats its parent sticky note's /Contents verbatim
        var sink = new RecordingSink();

        // Act: read its annotations
        var comments = Extract(PdfFixtures.WithPopupDuplicatingParent(), sink);

        // Assert: both annotations pass the type table and the content rule, so both are kept
        Assert.Equal(2, comments.Count);
        Assert.All(comments, comment => Assert.Equal("Section 2 needs a citation.", comment.Body));
        Assert.All(comments, comment => Assert.Equal("Alice Reviewer", comment.Author));
    }

    /// <summary>
    ///     Proves a comment is attributed to the page its annotation sits on.
    /// </summary>
    [Fact]
    public void PdfAnnotationExtractor_Extract_MultiPageDocument_AttributesCommentsToTheirPages()
    {
        // Arrange: a two-page document whose second page carries one annotation
        var sink = new RecordingSink();

        // Act: read its annotations
        var comments = Extract(PdfFixtures.WithAnnotations(), sink);

        // Assert: the second page's remark is the only one attributed to page two
        var second = Assert.Single(comments, comment => comment.PageNumber == 2);
        Assert.Equal("Rewrite this conclusion.", second.Body);
        Assert.Equal("Dave Approver", second.Author);
    }

    /// <summary>
    ///     Proves comments arrive in document order.
    /// </summary>
    /// <remarks>
    ///     Order is part of the contract rather than an accident: the review-comments artifact renders
    ///     comments as reported, and a reader scanning it expects to travel through the document.
    /// </remarks>
    [Fact]
    public void PdfAnnotationExtractor_Extract_MultiPageDocument_PreservesDocumentOrder()
    {
        // Arrange: a document whose annotations span two pages
        var sink = new RecordingSink();

        // Act: read its annotations
        var comments = Extract(PdfFixtures.WithAnnotations(), sink);

        // Assert: page numbers never decrease as the results are walked
        Assert.Equal([1, 1, 1, 1, 2], comments.Select(comment => comment.PageNumber));
    }

    /// <summary>
    ///     Proves a document with no annotations reports nothing at all.
    /// </summary>
    [Fact]
    public void PdfAnnotationExtractor_Extract_DocumentWithoutAnnotations_ReportsNothing()
    {
        // Arrange: an ordinary text document with no annotations on any page
        var sink = new RecordingSink();

        // Act: read its annotations
        var comments = Extract(PdfFixtures.SimpleText(), sink);

        // Assert: no comments, and no note either, because nothing was incomplete
        Assert.Empty(comments);
        Assert.Empty(sink.Notes);
    }

    /// <summary>
    ///     Proves an annotated document raises no note when every page was read.
    /// </summary>
    [Fact]
    public void PdfAnnotationExtractor_Extract_ReadableAnnotations_RaisesNoNote()
    {
        // Arrange: the annotated fixture, all of whose pages parse
        var sink = new RecordingSink();

        // Act: read its annotations
        Extract(PdfFixtures.WithAnnotations(), sink);

        // Assert: notes are reserved for steps that could not complete, and none applies here
        Assert.Empty(sink.Notes);
    }

    /// <summary>
    ///     Proves damaged annotation structures on one page cost only that page's comments.
    /// </summary>
    /// <remarks>
    ///     Containment is the point: a single broken annotation reference must not cost a reader the
    ///     comments on every other page, still less the document's text. PdfPig's lenient parser
    ///     absorbs this particular damage and simply reports no annotations for the page, so no note
    ///     is due here — the note exists for damage severe enough to fault the parser mid-walk, which
    ///     no fixture buildable here could provoke.
    /// </remarks>
    [Fact]
    public void PdfAnnotationExtractor_Extract_DamagedAnnotationReference_KeepsOtherPagesComments()
    {
        // Arrange: a two-page document whose first page's /Annots points at a non-existent object
        var sink = new RecordingSink();

        // Act: read its annotations
        var comments = Extract(PdfFixtures.WithUnreadableAnnotations(), sink);

        // Assert: the intact page's remark survives and nothing is invented for the damaged one
        var survivor = Assert.Single(comments);
        Assert.Equal("This page survived.", survivor.Body);
        Assert.Equal("Erin Checker", survivor.Author);
        Assert.Equal(2, survivor.PageNumber);
    }

    /// <summary>
    ///     Proves the extractor rejects a missing page list rather than reporting an empty result.
    /// </summary>
    [Fact]
    public void PdfAnnotationExtractor_Extract_NullPages_ThrowsArgumentNullException()
    {
        // Arrange: a usable sink but no pages
        var sink = new RecordingSink();

        // Act and assert: the mistake is refused at the boundary
        Assert.Throws<ArgumentNullException>(() =>
            global::DemaConsulting.DocDown.Pdf.PdfAnnotationExtractor.Extract(null!, sink, Ct));
    }

    /// <summary>
    ///     Proves the extractor rejects a missing sink rather than failing later when a note is due.
    /// </summary>
    [Fact]
    public void PdfAnnotationExtractor_Extract_NullSink_ThrowsArgumentNullException()
    {
        // Act and assert: the mistake is refused at the boundary
        Assert.Throws<ArgumentNullException>(() =>
            global::DemaConsulting.DocDown.Pdf.PdfAnnotationExtractor.Extract([], null!, Ct));
    }

    /// <summary>
    ///     Reads the annotations of every page of a fixture.
    /// </summary>
    /// <param name="bytes">The fixture document's bytes.</param>
    /// <param name="sink">The sink any note is recorded on.</param>
    /// <returns>The comments the extractor collected.</returns>
    /// <remarks>
    ///     Opens the fixture and calls the unit directly rather than running the engine, which keeps
    ///     these scenarios scoped to the inclusion decision instead of to the pipeline around it.
    /// </remarks>
    private static IReadOnlyList<global::DemaConsulting.DocDown.Pdf.PdfReviewAnnotation> Extract(byte[] bytes, RecordingSink sink)
    {
        using var document = PdfFixtures.Open(bytes);
        var pages = document.GetPages().ToList();
        return global::DemaConsulting.DocDown.Pdf.PdfAnnotationExtractor.Extract(pages, sink, Ct);
    }
}
