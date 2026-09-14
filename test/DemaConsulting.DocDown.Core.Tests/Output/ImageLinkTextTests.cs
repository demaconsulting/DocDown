using DocDown.Core;

namespace DemaConsulting.DocDown.Core.Tests.Output;

/// <summary>
///     Unit tests for <see cref="ImageLinkText" />.
/// </summary>
/// <remarks>
///     Two rules meet in this helper, and both matter to the reader of <c>content.md</c>. The alt text
///     must be honest — a document that offered no description must not appear to have offered one —
///     and it must not break the link it sits inside, because a stray bracket turns the rest of the
///     line into something the reader never wrote.
/// </remarks>
public class ImageLinkTextTests
{
    /// <summary>
    ///     Proves an absent or blank description yields the neutral placeholder rather than an empty
    ///     alt or an invented one.
    /// </summary>
    /// <param name="altText">The description the document offered, or the absence of one.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Alt_NoDescription_UsesNeutralPlaceholder(string? altText)
    {
        Assert.Equal("image", ImageLinkText.Alt(altText));
    }

    /// <summary>
    ///     Proves a plain description is preserved exactly.
    /// </summary>
    [Fact]
    public void Alt_PlainDescription_IsPreserved()
    {
        Assert.Equal("Site plan", ImageLinkText.Alt("Site plan"));
    }

    /// <summary>
    ///     Proves the characters that would close the alt bracket or the link target are escaped.
    /// </summary>
    /// <remarks>
    ///     Without this, a description containing <c>]</c> would end the alt early and the remainder
    ///     would be rendered as text beside a broken link.
    /// </remarks>
    [Theory]
    [InlineData("a]b")]
    [InlineData("a[b")]
    [InlineData("a(b")]
    [InlineData("a)b")]
    public void Alt_LinkStructuralCharacters_AreEscaped(string altText)
    {
        var result = ImageLinkText.Alt(altText);

        Assert.DoesNotContain("]", result.Replace("\\]", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.DoesNotContain("[", result.Replace("\\[", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.DoesNotContain("(", result.Replace("\\(", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.DoesNotContain(")", result.Replace("\\)", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves a description spanning lines is collapsed onto one, so the link survives.
    /// </summary>
    [Theory]
    [InlineData("first\nsecond")]
    [InlineData("first\r\nsecond")]
    [InlineData("first\rsecond")]
    public void Alt_MultiLineDescription_StaysOnOneLine(string altText)
    {
        var result = ImageLinkText.Alt(altText);

        Assert.DoesNotContain('\n', result);
        Assert.DoesNotContain('\r', result);
        Assert.Contains("first", result, StringComparison.Ordinal);
        Assert.Contains("second", result, StringComparison.Ordinal);
    }
}
