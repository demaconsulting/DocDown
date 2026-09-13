using DocDown.Core;

namespace DemaConsulting.DocDown.Core.Tests.Output;

/// <summary>
///     Unit tests for <see cref="PartResourceLinkRewriter"/>, proving that root-relative resource
///     links are made relative to a part file's own directory while external, anchor, and
///     already-relative targets are left untouched.
/// </summary>
/// <remarks>
///     These are pure-function tests: the rewriter performs no I/O, so each test supplies a markdown
///     body and a part path and asserts on the returned string. They pin the narrow contract that
///     lets a part file under <c>parts/</c> reach the scratch-root resource folders, and the
///     leave-untouched cases that keep the rewrite from corrupting content links.
/// </remarks>
public class PartResourceLinkRewriterTests
{
    /// <summary>
    ///     Proves an image link to <c>images/…</c> in a flat part file gains a single <c>../</c>.
    /// </summary>
    [Fact]
    public void PartResourceLinkRewriter_Rewrite_ImageLinkInFlatPart_PrefixesOneParent()
    {
        // Arrange: an image link a sheet part emits root-relative
        const string markdown = "![Sales chart](images/0001-image.png)\n";

        // Act: rewrite for a flat part path one directory below the root
        var result = PartResourceLinkRewriter.Rewrite(markdown, "parts/0001-sheet-charts.md");

        // Assert: the link now reaches back up one level to the images folder
        Assert.Equal("![Sales chart](../images/0001-image.png)\n", result);
    }

    /// <summary>
    ///     Proves a plain link to <c>pages/…</c> is rewritten just like the image form.
    /// </summary>
    [Fact]
    public void PartResourceLinkRewriter_Rewrite_PageLinkPlainForm_PrefixesOneParent()
    {
        // Arrange: a plain (non-image) link to a rendered page
        const string markdown = "See [page 2](pages/page0002.png).";

        // Act: rewrite for a flat part path
        var result = PartResourceLinkRewriter.Rewrite(markdown, "parts/0002-slide.md");

        // Assert: the page target is made relative to the part directory
        Assert.Equal("See [page 2](../pages/page0002.png).", result);
    }

    /// <summary>
    ///     Proves an external http(s) target is never rewritten.
    /// </summary>
    [Fact]
    public void PartResourceLinkRewriter_Rewrite_ExternalUrl_LeftUntouched()
    {
        // Arrange: an external hyperlink of the kind Word emits inside a part
        const string markdown = "Visit [the site](https://example.com/images/logo.png).";

        // Act: rewrite for a flat part path
        var result = PartResourceLinkRewriter.Rewrite(markdown, "parts/0001-section.md");

        // Assert: the absolute URL is unchanged even though it contains "images/"
        Assert.Equal(markdown, result);
    }

    /// <summary>
    ///     Proves a mailto target is never rewritten.
    /// </summary>
    [Fact]
    public void PartResourceLinkRewriter_Rewrite_MailtoLink_LeftUntouched()
    {
        // Arrange: a mailto hyperlink
        const string markdown = "Email [us](mailto:team@example.com).";

        // Act: rewrite for a flat part path
        var result = PartResourceLinkRewriter.Rewrite(markdown, "parts/0001-section.md");

        // Assert: the mailto link is unchanged
        Assert.Equal(markdown, result);
    }

    /// <summary>
    ///     Proves an in-document anchor target is never rewritten.
    /// </summary>
    [Fact]
    public void PartResourceLinkRewriter_Rewrite_AnchorLink_LeftUntouched()
    {
        // Arrange: an intra-document anchor link
        const string markdown = "Jump to [the top](#top).";

        // Act: rewrite for a flat part path
        var result = PartResourceLinkRewriter.Rewrite(markdown, "parts/0001-section.md");

        // Assert: the anchor link is unchanged
        Assert.Equal(markdown, result);
    }

    /// <summary>
    ///     Proves an already-relative target is left untouched, making the rewrite idempotent.
    /// </summary>
    [Fact]
    public void PartResourceLinkRewriter_Rewrite_AlreadyRelativeTarget_LeftUntouched()
    {
        // Arrange: markdown that already carries the correct part-relative link
        const string markdown = "![alt](../images/0001-image.png)\n";

        // Act: rewrite again for a flat part path
        var result = PartResourceLinkRewriter.Rewrite(markdown, "parts/0001-sheet.md");

        // Assert: a second pass changes nothing, so the operation is idempotent
        Assert.Equal(markdown, result);
    }

    /// <summary>
    ///     Proves a part written at the scratch root (depth zero) is returned verbatim.
    /// </summary>
    [Fact]
    public void PartResourceLinkRewriter_Rewrite_RootLevelPart_ReturnsVerbatim()
    {
        // Arrange: a root-level document with a root-relative image link
        const string markdown = "![alt](images/0001-image.png)\n";

        // Act: rewrite for a root-level path with no directory above it
        var result = PartResourceLinkRewriter.Rewrite(markdown, "content.md");

        // Assert: a root-level file needs no prefix, so the text is unchanged
        Assert.Equal(markdown, result);
    }

    /// <summary>
    ///     Proves a nested part path yields one <c>../</c> per directory level.
    /// </summary>
    [Fact]
    public void PartResourceLinkRewriter_Rewrite_NestedPart_PrefixesPerLevel()
    {
        // Arrange: a hypothetical two-level-deep part path
        const string markdown = "![alt](images/0001-image.png)";

        // Act: rewrite for a part two directories below the root
        var result = PartResourceLinkRewriter.Rewrite(markdown, "parts/group/0001-sheet.md");

        // Assert: two directory levels produce two parent hops
        Assert.Equal("![alt](../../images/0001-image.png)", result);
    }

    /// <summary>
    ///     Proves multiple resource links in one body are all rewritten.
    /// </summary>
    [Fact]
    public void PartResourceLinkRewriter_Rewrite_MultipleLinks_RewritesEach()
    {
        // Arrange: a body mixing two resource links with an external link that must survive
        const string markdown =
            "![a](images/0001-a.png)\n![b](images/0002-b.png)\n[ext](https://x/images/c.png)\n";

        // Act: rewrite for a flat part path
        var result = PartResourceLinkRewriter.Rewrite(markdown, "parts/0001-sheet.md");

        // Assert: both resource links move and the external link is preserved
        Assert.Equal(
            "![a](../images/0001-a.png)\n![b](../images/0002-b.png)\n[ext](https://x/images/c.png)\n",
            result);
    }

    /// <summary>
    ///     Proves a null markdown body is rejected as a caller error.
    /// </summary>
    [Fact]
    public void PartResourceLinkRewriter_Rewrite_NullMarkdown_ThrowsArgumentNullException()
    {
        // Act + Assert: the body is mandatory
        Assert.Throws<ArgumentNullException>(() => PartResourceLinkRewriter.Rewrite(null!, "parts/0001-sheet.md"));
    }

    /// <summary>
    ///     Proves a null part path is rejected as a caller error.
    /// </summary>
    [Fact]
    public void PartResourceLinkRewriter_Rewrite_NullPartPath_ThrowsArgumentNullException()
    {
        // Act + Assert: the part path is mandatory
        Assert.Throws<ArgumentNullException>(() => PartResourceLinkRewriter.Rewrite("text", null!));
    }
}
