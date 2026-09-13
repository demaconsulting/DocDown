using DocDown.Core;

namespace DemaConsulting.DocDown.Core.Tests.Extraction;

/// <summary>
///     Unit tests for <see cref="ExtractorSelector"/>, proving format filtering, availability
///     filtering, render-page preference, deterministic ranking, and the prose failures returned
///     when no extractor can be selected.
/// </summary>
/// <remarks>
///     The selector is now a pure static function over a detected format, effective options, and
///     the currently probed candidates. These tests therefore construct candidates directly and
///     assert only the selected descriptor and any returned prose failure.
/// </remarks>
public class ExtractorSelectorTests
{
    /// <summary>
    ///     Proves a candidate that supports the detected format is selected while a format-mismatched
    ///     candidate is ignored.
    /// </summary>
    [Fact]
    public void ExtractorSelector_Select_FormatMatchedCandidate_SelectsMatchingDescriptor()
    {
        // Arrange: one text candidate and one PDF-only candidate
        var candidates = new[]
        {
            Candidate("texter"),
            Candidate("pdfer", formats: [DocumentFormat.Pdf])
        };

        // Act: select for a text document
        var selected = ExtractorSelector.Select(TextDetection(), TextOnlyOptions(), candidates, out var failure);

        // Assert: the matching text descriptor is selected and no failure is returned
        Assert.Null(failure);
        Assert.Equal("texter", selected?.Id);
    }

    /// <summary>
    ///     Proves a well-known format with no registered candidate returns failure prose naming the
    ///     providing package.
    /// </summary>
    [Fact]
    public void ExtractorSelector_Select_DocxWithoutMatchingCandidate_ReturnsPackageHint()
    {
        // Arrange: only a text candidate is registered while a Word document is detected
        var detection = new FormatDetection(DocumentFormat.Docx, DetectionBasis.Extension, 0.9);
        var candidates = new[] { Candidate("texter") };

        // Act: select for the unsupported docx format
        var selected = ExtractorSelector.Select(detection, TextOnlyOptions(), candidates, out var failure);

        // Assert: no descriptor is selected and the prose names the owning package factually
        Assert.Null(selected);
        Assert.NotNull(failure);
        Assert.Equal("No registered extractor supports the detected format.", failure.Summary);
        Assert.Contains("Detected format: docx", failure.Explanation, StringComparison.Ordinal);
        Assert.Contains("DemaConsulting.DocDown.Word", failure.Explanation, StringComparison.Ordinal);
        Assert.Contains("which a host registers with the engine", failure.Explanation, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves a legacy binary format with no registered candidate returns the fixed unsupported
    ///     legacy-binary wording rather than naming a package.
    /// </summary>
    [Fact]
    public void ExtractorSelector_Select_LegacyFormatWithoutMatchingCandidate_ReturnsLegacyBinaryWording()
    {
        // Arrange: only a text candidate is registered while a legacy Word document is detected
        var detection = new FormatDetection(DocumentFormat.Doc, DetectionBasis.Extension, 0.9);
        var candidates = new[] { Candidate("texter") };

        // Act: select for the legacy format
        var selected = ExtractorSelector.Select(detection, TextOnlyOptions(), candidates, out var failure);

        // Assert: no descriptor is selected and the failure states the legacy-binary fact plainly
        Assert.Null(selected);
        Assert.NotNull(failure);
        Assert.Contains("Detected format: doc", failure.Explanation, StringComparison.Ordinal);
        Assert.Contains(
            "DocDown does not support the legacy binary Office formats",
            failure.Explanation,
            StringComparison.Ordinal);
        Assert.DoesNotContain("DemaConsulting.DocDown.Word", failure.Explanation, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves an unmapped custom format falls back to the generic wording naming only the format.
    /// </summary>
    [Fact]
    public void ExtractorSelector_Select_CustomFormatWithoutMatchingCandidate_ReturnsGenericWording()
    {
        // Arrange: only a text candidate is registered while a custom format is detected
        var custom = DocumentFormat.Custom("dwg", "image/vnd.dwg");
        var detection = new FormatDetection(custom, DetectionBasis.Extension, 0.9);
        var candidates = new[] { Candidate("texter") };

        // Act: select for the unmapped custom format
        var selected = ExtractorSelector.Select(detection, TextOnlyOptions(), candidates, out var failure);

        // Assert: no package name is invented for an unmapped format
        Assert.Null(selected);
        Assert.NotNull(failure);
        Assert.Contains("Detected format: dwg", failure.Explanation, StringComparison.Ordinal);
        Assert.Contains("No registered extractor supports 'dwg'.", failure.Explanation, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves that when every in-format candidate is unavailable the selector returns the
    ///     unavailability failure prose.
    /// </summary>
    [Fact]
    public void ExtractorSelector_Select_AllMatchingCandidatesUnavailable_ReturnsAvailabilityFailure()
    {
        // Arrange: the only text candidate is unavailable in this environment
        var candidates = new[] { Unavailable("only", "temporarily offline") };

        // Act: select automatically
        var selected = ExtractorSelector.Select(TextDetection(), TextOnlyOptions(), candidates, out var failure);

        // Assert: no candidate can be selected and the failure names the format
        Assert.Null(selected);
        Assert.NotNull(failure);
        Assert.Equal("No available extractor can process the detected format in this environment.", failure.Summary);
        Assert.Contains("Detected format: text", failure.Explanation, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves a render-capable candidate is preferred when page rendering was requested.
    /// </summary>
    [Fact]
    public void ExtractorSelector_Select_RenderPagesRequested_PrefersCandidateProvidingRenderedPages()
    {
        // Arrange: the non-rendering candidate ranks higher, but a renderer is available
        var options = TextOnlyOptions();
        options.RenderPages = true;
        var candidates = new[]
        {
            Candidate("high-text", priority: 100),
            Candidate("renderer", priority: 1, providesRenderedPages: true)
        };

        // Act: select with page rendering requested
        var selected = ExtractorSelector.Select(TextDetection(), options, candidates, out var failure);

        // Assert: renderer preference outranks the ordinary priority comparison
        Assert.Null(failure);
        Assert.Equal("renderer", selected?.Id);
    }

    /// <summary>
    ///     Proves a page-render request falls back to the ordinary ranking when no available
    ///     candidate can render pages.
    /// </summary>
    [Fact]
    public void ExtractorSelector_Select_RenderPagesRequestedWithoutRenderer_FallsBackToPriority()
    {
        // Arrange: neither available candidate can render pages
        var options = TextOnlyOptions();
        options.RenderPages = true;
        var candidates = new[]
        {
            Candidate("banana", priority: 10),
            Candidate("apple", priority: 5)
        };

        // Act: select with the unsatisfied render preference
        var selected = ExtractorSelector.Select(TextDetection(), options, candidates, out var failure);

        // Assert: selection still succeeds and uses the ordinary priority ordering
        Assert.Null(failure);
        Assert.Equal("banana", selected?.Id);
    }

    /// <summary>
    ///     Proves the higher priority candidate wins when the selector is otherwise indifferent.
    /// </summary>
    [Fact]
    public void ExtractorSelector_Select_EqualCandidates_HigherPriorityWins()
    {
        // Arrange: two ordinary text candidates differing only by priority
        var candidates = new[]
        {
            Candidate("apple", priority: 5),
            Candidate("banana", priority: 10)
        };

        // Act: select between them
        var selected = ExtractorSelector.Select(TextDetection(), TextOnlyOptions(), candidates, out var failure);

        // Assert: the higher priority descriptor is selected
        Assert.Null(failure);
        Assert.Equal("banana", selected?.Id);
    }

    /// <summary>
    ///     Proves the lower identifier is the deterministic tie-break when priorities are equal.
    /// </summary>
    [Fact]
    public void ExtractorSelector_Select_EqualPriorityCandidates_LowerIdentifierWins()
    {
        // Arrange: two equal-priority text candidates
        var candidates = new[]
        {
            Candidate("zebra", priority: 5),
            Candidate("alpha", priority: 5)
        };

        // Act: select between them
        var selected = ExtractorSelector.Select(TextDetection(), TextOnlyOptions(), candidates, out var failure);

        // Assert: the final tie-break is the ascending identifier
        Assert.Null(failure);
        Assert.Equal("alpha", selected?.Id);
    }

    /// <summary>
    ///     Proves the selector remains deterministic regardless of the input candidate order.
    /// </summary>
    [Fact]
    public void ExtractorSelector_Select_ShuffledCandidates_ProducesSameWinner()
    {
        // Arrange: the same three candidates in two different orders
        var one = Candidate("gamma", priority: 5);
        var two = Candidate("alpha", priority: 5);
        var three = Candidate("beta", priority: 5);
        var forward = new[] { one, two, three };
        var shuffled = new[] { three, one, two };

        // Act: select from both orderings
        var forwardWinner = ExtractorSelector.Select(TextDetection(), TextOnlyOptions(), forward, out var forwardFailure);
        var shuffledWinner = ExtractorSelector.Select(TextDetection(), TextOnlyOptions(), shuffled, out var shuffledFailure);

        // Assert: equal inputs modulo ordering produce the same selected descriptor and no failures
        Assert.Null(forwardFailure);
        Assert.Null(shuffledFailure);
        Assert.Equal(forwardWinner, shuffledWinner);
    }

    /// <summary>
    ///     Creates an available text candidate for selector tests.
    /// </summary>
    /// <param name="id">The candidate identifier.</param>
    /// <param name="priority">The ranking priority.</param>
    /// <param name="providesRenderedPages">Whether the candidate can render pages in this environment.</param>
    /// <param name="formats">The supported formats, defaulting to plain text.</param>
    /// <param name="pageRenderingApplicable">Whether page rendering is meaningful for the candidate's formats.</param>
    /// <returns>The configured available candidate.</returns>
    private static ExtractorCandidate Candidate(
        string id,
        int priority = 0,
        bool providesRenderedPages = false,
        IReadOnlyList<DocumentFormat>? formats = null,
        bool pageRenderingApplicable = true) =>
        new(
            new ExtractorDescriptor(
                id,
                id + " backend",
                formats ?? [DocumentFormat.Text],
                priority,
                pageRenderingApplicable),
            ExtractorAvailability.Available(providesRenderedPages));

    /// <summary>
    ///     Creates an unavailable text candidate for selector tests.
    /// </summary>
    /// <param name="id">The candidate identifier.</param>
    /// <param name="reason">The unavailability reason.</param>
    /// <param name="priority">The ranking priority.</param>
    /// <returns>The configured unavailable candidate.</returns>
    private static ExtractorCandidate Unavailable(string id, string reason, int priority = 0) =>
        new(
            new ExtractorDescriptor(id, id + " backend", [DocumentFormat.Text], priority),
            ExtractorAvailability.Unavailable(reason));

    /// <summary>
    ///     Creates options whose only selection-relevant setting is that page rendering is off.
    /// </summary>
    /// <returns>The text-only options.</returns>
    private static ExtractionOptions TextOnlyOptions() => new() { IncludeEmbeddedImages = false };

    /// <summary>
    ///     Creates a plain-text detection for selector tests.
    /// </summary>
    /// <returns>A text detection by file extension.</returns>
    private static FormatDetection TextDetection() => new(DocumentFormat.Text, DetectionBasis.Extension, 0.5);
}
