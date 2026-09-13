using DocDown.Core;

namespace DemaConsulting.DocDown.Core.Tests.Extraction;

/// <summary>
///     Unit tests for <see cref="ExtractorSelector"/>, proving its complete ranking table, its
///     format and availability filters, its never-falls-back caller-override semantics, every
///     failure kind it can produce, and its deterministic, order-independent, repeatable outcome.
/// </summary>
/// <remarks>
///     These tests bind only to <see cref="ExtractorSelector"/> and the immutable candidate,
///     descriptor, availability, and result types it operates on. Selection is a pure function of
///     its three arguments, so every scenario is expressed with hand-built candidates. Each test is
///     named for the unit requirement it evidences: format filtering, override honoring, override
///     never falling back, availability filtering, required-capability computation, hard-capability
///     failure, rank ordering, deterministic tie-break, candidate trace, and repeatability.
/// </remarks>
public class ExtractorSelectorTests
{
    /// <summary>
    ///     Proves a candidate that does not support the detected format is classified out (FormatFilter).
    /// </summary>
    [Fact]
    public void ExtractorSelector_Select_CandidateForOtherFormat_ClassifiedFormatNotSupported()
    {
        // Arrange: one text backend and one pdf-only backend, with a text document detected
        var selector = new ExtractorSelector();
        var candidates = new[]
        {
            Candidate("texter", ExtractorCapabilities.Text),
            Candidate("pdfer", ExtractorCapabilities.Text, formats: [DocumentFormat.Pdf])
        };

        // Act: select for the text format
        var result = selector.Select(TextDetection(), TextOnlyOptions(), candidates);

        // Assert: the text backend wins and the pdf backend is traced as format-not-supported
        Assert.Equal("texter", result.Selected?.Id);
        var pdfVerdict = result.Trace.Single(verdict => verdict.ExtractorId == "pdfer");
        Assert.Equal(CandidateOutcome.FormatNotSupported, pdfVerdict.Outcome);
    }

    /// <summary>
    ///     Proves that when no candidate supports the format the selection fails with DD0402 (FormatFilter).
    /// </summary>
    [Fact]
    public void ExtractorSelector_Select_NoCandidateSupportsFormat_FailsNoExtractorForFormat()
    {
        // Arrange: only a pdf backend, with a text document detected
        var selector = new ExtractorSelector();
        var candidates = new[] { Candidate("pdfer", ExtractorCapabilities.Text, formats: [DocumentFormat.Pdf]) };

        // Act: select for the unsupported text format
        var result = selector.Select(TextDetection(), TextOnlyOptions(), candidates);

        // Assert: no backend is selected and the failure is the no-extractor-for-format kind
        Assert.Null(result.Selected);
        Assert.NotNull(result.Failure);
        Assert.Equal(ExtractionFailureKind.NoExtractorForFormat, result.Failure.Kind);
        Assert.Equal("DD0402", result.Failure.Code);
    }

    /// <summary>
    ///     Proves the no-extractor remedy names the package that provides the backend (PackageHint).
    /// </summary>
    [Fact]
    public void ExtractorSelector_Select_DocxWithNoMatchingExtractor_RemedyNamesWordPackage()
    {
        // Arrange: only a text backend registered, with a Word document detected
        var selector = new ExtractorSelector();
        var candidates = new[] { Candidate("texter", ExtractorCapabilities.Text, formats: [DocumentFormat.Text]) };
        var detection = new FormatDetection(DocumentFormat.Docx, DetectionBasis.Extension, 0.9);

        // Act: select for the docx format no registered backend handles
        var result = selector.Select(detection, TextOnlyOptions(), candidates);

        // Assert: the failure names the format and the exact package that provides the extractor
        Assert.NotNull(result.Failure);
        Assert.Equal(ExtractionFailureKind.NoExtractorForFormat, result.Failure.Kind);
        Assert.Contains("docx", result.Failure.Remedy, StringComparison.Ordinal);
        Assert.Contains("DemaConsulting.DocDown.Word", result.Failure.Remedy, StringComparison.Ordinal);

        // Assert: the remedy explains where the capability lives rather than commanding an action
        Assert.Contains("Core does not extract this format itself", result.Failure.Remedy, StringComparison.Ordinal);
        Assert.Contains("which a host registers with the engine", result.Failure.Remedy, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves a legacy binary format's remedy states plainly that DocDown does not support the
    ///     format, without naming a package or a precondition that will never deliver it (PackageHint).
    /// </summary>
    /// <remarks>
    ///     The legacy binaries (<c>.doc/.xls/.ppt/.vsd</c>) are detectable but not extractable, and
    ///     no DocDown package will ever extract them. Detection stays because "this is a .doc, and
    ///     DocDown does not support legacy binary formats" beats "unrecognized format"; the remedy
    ///     must therefore be declarative about that unsupported status and must not point at an
    ///     extractor package or an Office-on-Windows precondition, either of which would promise a
    ///     capability that does not exist. The wording also carries no install verb, so it stays
    ///     compatible with the forbidden-substring guard on the well-known surface.
    /// </remarks>
    [Fact]
    public void ExtractorSelector_Select_LegacyFormatWithNoBackend_RemedyStatesFormatUnsupported()
    {
        // Arrange: only a text backend registered, with a legacy binary Word document detected
        var selector = new ExtractorSelector();
        var candidates = new[] { Candidate("texter", ExtractorCapabilities.Text, formats: [DocumentFormat.Text]) };
        var detection = new FormatDetection(DocumentFormat.Doc, DetectionBasis.Extension, 0.9);

        // Act: select for the legacy .doc format no registered backend handles
        var result = selector.Select(detection, TextOnlyOptions(), candidates);

        // Assert: the failure names the format and states the unsupported status declaratively
        Assert.NotNull(result.Failure);
        Assert.Equal(ExtractionFailureKind.NoExtractorForFormat, result.Failure.Kind);
        Assert.Contains("doc", result.Failure.Remedy, StringComparison.Ordinal);
        Assert.Contains(
            "DocDown does not support the legacy binary Office formats",
            result.Failure.Remedy,
            StringComparison.Ordinal);
        Assert.Contains("cannot be extracted by any DocDown package", result.Failure.Remedy, StringComparison.Ordinal);

        // Assert: it promises nothing — no providing package, no environment precondition
        Assert.DoesNotContain("DemaConsulting.DocDown.Word", result.Failure.Remedy, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft Office", result.Failure.Remedy, StringComparison.Ordinal);
        Assert.DoesNotContain("install", result.Failure.Remedy, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Proves every legacy binary format — not just <c>.doc</c> — carries the same declarative
    ///     unsupported wording (PackageHint).
    /// </summary>
    /// <remarks>
    ///     The four legacy binaries are a single class of thing, and a reader who tries a
    ///     <c>.xls</c> deserves the same honest answer a reader who tried a <c>.doc</c> got.
    ///     Checking all four together stops the wording drifting apart one format at a time.
    /// </remarks>
    [Fact]
    public void ExtractorSelector_Select_EveryLegacyFormat_RemedyStatesFormatUnsupported()
    {
        // Arrange: only a text backend registered, and every legacy binary format Core detects
        var selector = new ExtractorSelector();
        var candidates = new[] { Candidate("texter", ExtractorCapabilities.Text, formats: [DocumentFormat.Text]) };
        DocumentFormat[] legacy = [DocumentFormat.Doc, DocumentFormat.Xls, DocumentFormat.Ppt, DocumentFormat.Vsd];

        // Act: collect the remedy produced for each legacy format with no matching backend
        var remedies = legacy
            .Select(format => (format, Remedy: selector.Select(
                new FormatDetection(format, DetectionBasis.Extension, 0.9),
                TextOnlyOptions(),
                candidates).Failure?.Remedy ?? string.Empty))
            .ToArray();

        // Assert: each names its own format, states the unsupported fact, and promises nothing
        Assert.Multiple(remedies.Select<(DocumentFormat Format, string Remedy), Action>(entry => () =>
        {
            Assert.Contains(entry.Format.Id, entry.Remedy, StringComparison.Ordinal);
            Assert.Contains(
                "DocDown does not support the legacy binary Office formats",
                entry.Remedy,
                StringComparison.Ordinal);
            Assert.DoesNotContain("DemaConsulting.DocDown.", entry.Remedy, StringComparison.Ordinal);
            Assert.DoesNotContain("Microsoft Office", entry.Remedy, StringComparison.Ordinal);
            Assert.DoesNotContain("install", entry.Remedy, StringComparison.OrdinalIgnoreCase);
        }).ToArray());
    }

    /// <summary>
    ///     Proves no well-known format's remedy instructs an installation that cannot succeed, and
    ///     that none of them uses wording which expires when the packages ship (PackageHint).
    /// </summary>
    /// <remarks>
    ///     An imperative such as "install the ... package" presumes the reader acquires code by
    ///     package reference, which a vendored build or a single-file image with a fixed backend set
    ///     does not. This test guards the whole well-known surface against that wording and against
    ///     "not yet available" phrasing, which would become false on publication day and force a
    ///     code change then.
    /// </remarks>
    [Fact]
    public void ExtractorSelector_Select_WellKnownFormatWithNoBackend_RemedyNeverInstructsInstallation()
    {
        // Arrange: a single text-only backend, and every well-known format with its providing package
        var selector = new ExtractorSelector();
        var candidates = new[] { Candidate("texter", ExtractorCapabilities.Text, formats: [DocumentFormat.Text]) };
        var expected = new (DocumentFormat Format, string Package)[]
        {
            (DocumentFormat.Pdf, "DemaConsulting.DocDown.Pdf"),
            (DocumentFormat.Docx, "DemaConsulting.DocDown.Word"),
            (DocumentFormat.Xlsx, "DemaConsulting.DocDown.Excel"),
            (DocumentFormat.Pptx, "DemaConsulting.DocDown.PowerPoint"),
            (DocumentFormat.Vsdx, "DemaConsulting.DocDown.Visio"),
            (DocumentFormat.Html, "DemaConsulting.DocDown.Html")
        };
        string[] forbidden = ["install", "download", "nuget", "PackageReference", "dotnet add package"];
        string[] expiring = ["not yet available", "coming soon", "will be available", "in a future release"];

        // Act: collect the remedy produced for each well-known format with no matching backend
        var remedies = expected
            .Select(entry => (entry.Format, entry.Package,
                Remedy: selector.Select(
                    new FormatDetection(entry.Format, DetectionBasis.Extension, 0.9),
                    TextOnlyOptions(),
                    candidates).Failure?.Remedy ?? string.Empty))
            .ToArray();

        // Assert: every remedy still explains the failure and names its provider, and none commands
        // an impossible installation or makes a claim that expires when the packages ship
        Assert.Multiple(remedies.Select<(DocumentFormat Format, string Package, string Remedy), Action>(entry => () =>
        {
            Assert.Contains(entry.Format.Id, entry.Remedy, StringComparison.Ordinal);
            Assert.Contains(entry.Package, entry.Remedy, StringComparison.Ordinal);
            foreach (var word in forbidden)
            {
                Assert.DoesNotContain(word, entry.Remedy, StringComparison.OrdinalIgnoreCase);
            }

            foreach (var phrase in expiring)
            {
                Assert.DoesNotContain(phrase, entry.Remedy, StringComparison.OrdinalIgnoreCase);
            }
        }).ToArray());
    }

    /// <summary>
    ///     Proves a format with no known package falls back to the generic remedy wording (PackageHint).
    /// </summary>
    [Fact]
    public void ExtractorSelector_Select_UnmappedFormatWithNoMatchingExtractor_UsesGenericRemedy()
    {
        // Arrange: only a text backend registered, with a custom format detected
        var selector = new ExtractorSelector();
        var candidates = new[] { Candidate("texter", ExtractorCapabilities.Text, formats: [DocumentFormat.Text]) };
        var custom = DocumentFormat.Custom("dwg", "image/vnd.dwg");
        var detection = new FormatDetection(custom, DetectionBasis.Extension, 0.9);

        // Act: select for a format absent from the well-known package table
        var result = selector.Select(detection, TextOnlyOptions(), candidates);

        // Assert: no package name is invented; the generic wording names only the format
        Assert.NotNull(result.Failure);
        Assert.Equal("Register an extractor that supports 'dwg'.", result.Failure.Remedy);
    }

    /// <summary>
    ///     Proves a caller override selects the named backend even when it ranks lower (OverrideHonored).
    /// </summary>
    [Fact]
    public void ExtractorSelector_Select_OverrideNamesLowerPriority_SelectsNamedBackend()
    {
        // Arrange: a high-priority and a low-priority text backend, with the low one preferred
        var selector = new ExtractorSelector();
        var options = TextOnlyOptions();
        options.PreferredExtractorId = "low";
        var candidates = new[]
        {
            Candidate("high", ExtractorCapabilities.Text, priority: 100),
            Candidate("low", ExtractorCapabilities.Text, priority: 1)
        };

        // Act: select with the caller override in force
        var result = selector.Select(TextDetection(), options, candidates);

        // Assert: the named low-priority backend wins in caller-override mode
        Assert.Equal("low", result.Selected?.Id);
        Assert.Equal(SelectionMode.CallerOverride, result.Mode);
        Assert.Equal(CandidateOutcome.ExcludedByOverride, result.Trace.Single(v => v.ExtractorId == "high").Outcome);
    }

    /// <summary>
    ///     Proves an override that names an unavailable backend fails and never falls back (OverrideNeverFallsBack).
    /// </summary>
    [Fact]
    public void ExtractorSelector_Select_OverrideNamesUnavailable_FailsAndNeverFallsBack()
    {
        // Arrange: the preferred backend is unavailable while another capable backend is available
        var selector = new ExtractorSelector();
        var options = TextOnlyOptions();
        options.PreferredExtractorId = "pref";
        var candidates = new[]
        {
            Unavailable("pref", "the preferred backend is offline"),
            Candidate("fallback", ExtractorCapabilities.Text)
        };

        // Act: select with the override naming the offline backend
        var result = selector.Select(TextDetection(), options, candidates);

        // Assert: the selection fails rather than silently choosing the available fallback
        Assert.Null(result.Selected);
        Assert.Equal(ExtractionFailureKind.RequestedExtractorUnavailable, result.Failure?.Kind);
        Assert.Equal("DD0406", result.Failure?.Code);
    }

    /// <summary>
    ///     Proves an override that names a format-mismatched backend fails with DD0405 (OverrideNeverFallsBack).
    /// </summary>
    [Fact]
    public void ExtractorSelector_Select_OverrideNamesFormatMismatch_FailsRequestedExtractorNotApplicable()
    {
        // Arrange: the preferred backend supports only pdf while a text backend is present and text is detected
        var selector = new ExtractorSelector();
        var options = TextOnlyOptions();
        options.PreferredExtractorId = "pref";
        var candidates = new[]
        {
            Candidate("generic", ExtractorCapabilities.Text),
            Candidate("pref", ExtractorCapabilities.Text, formats: [DocumentFormat.Pdf])
        };

        // Act: select with the override naming a backend that cannot handle the format
        var result = selector.Select(TextDetection(), options, candidates);

        // Assert: the selection fails rather than falling back to the applicable generic backend
        Assert.Null(result.Selected);
        Assert.Equal(ExtractionFailureKind.RequestedExtractorNotApplicable, result.Failure?.Kind);
        Assert.Equal("DD0405", result.Failure?.Code);
    }

    /// <summary>
    ///     Proves an unavailable candidate is excluded and its reason is recorded in the trace (AvailabilityFilter).
    /// </summary>
    [Fact]
    public void ExtractorSelector_Select_UnavailableCandidate_ExcludedWithReasonInTrace()
    {
        // Arrange: an available backend and an unavailable one with a distinctive reason
        var selector = new ExtractorSelector();
        const string reason = "the accelerated backend requires a GPU that is absent";
        var candidates = new[]
        {
            Candidate("up", ExtractorCapabilities.Text),
            Unavailable("down", reason)
        };

        // Act: select automatically
        var result = selector.Select(TextDetection(), TextOnlyOptions(), candidates);

        // Assert: the available backend wins and the excluded one's verdict carries its reason
        Assert.Equal("up", result.Selected?.Id);
        var downVerdict = result.Trace.Single(verdict => verdict.ExtractorId == "down");
        Assert.Equal(CandidateOutcome.Unavailable, downVerdict.Outcome);
        Assert.Contains(reason, downVerdict.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves that when every candidate is unavailable the selection fails with DD0403 (AvailabilityFilter).
    /// </summary>
    [Fact]
    public void ExtractorSelector_Select_AllCandidatesUnavailable_FailsNoAvailableExtractor()
    {
        // Arrange: the only in-format backend is unavailable
        var selector = new ExtractorSelector();
        var candidates = new[] { Unavailable("only", "temporarily offline") };

        // Act: select automatically with nothing available
        var result = selector.Select(TextDetection(), TextOnlyOptions(), candidates);

        // Assert: the failure is the no-available-extractor kind
        Assert.Null(result.Selected);
        Assert.Equal(ExtractionFailureKind.NoAvailableExtractor, result.Failure?.Kind);
        Assert.Equal("DD0403", result.Failure?.Code);
    }

    /// <summary>
    ///     Proves a requested capability is added to the required set and satisfied when present (RequiredCapabilities).
    /// </summary>
    [Fact]
    public void ExtractorSelector_Select_RequireCapabilitiesSatisfied_AddsToRequiredAndSelects()
    {
        // Arrange: the caller requires rendered pages and a capable backend provides them
        var selector = new ExtractorSelector();
        var options = TextOnlyOptions();
        options.RequireCapabilities = ExtractorCapabilities.RenderedPages;
        var candidates = new[]
        {
            Candidate("full", ExtractorCapabilities.Text | ExtractorCapabilities.RenderedPages)
        };

        // Act: select with the extra capability requirement
        var result = selector.Select(TextDetection(), options, candidates);

        // Assert: the requirement is folded into the required set and satisfied by the winner
        Assert.Equal("full", result.Selected?.Id);
        Assert.True(result.RequiredCapabilities.HasFlag(ExtractorCapabilities.RenderedPages));
        Assert.True(result.SatisfiedCapabilities.HasFlag(ExtractorCapabilities.RenderedPages));
    }

    /// <summary>
    ///     Proves an unsatisfiable hard requirement fails rather than degrades (HardCapabilityFailure).
    /// </summary>
    [Fact]
    public void ExtractorSelector_Select_RequireCapabilitiesUnsatisfiable_FailsRequiredCapabilitiesUnavailable()
    {
        // Arrange: the caller requires rendered pages but the only backend is text-only
        var selector = new ExtractorSelector();
        var options = TextOnlyOptions();
        options.RequireCapabilities = ExtractorCapabilities.RenderedPages;
        var candidates = new[] { Candidate("texter", ExtractorCapabilities.Text) };

        // Act: select with the unsatisfiable hard requirement
        var result = selector.Select(TextDetection(), options, candidates);

        // Assert: a hard requirement no full satisfier can meet is a failure, not a degrade
        Assert.Null(result.Selected);
        Assert.Equal(ExtractionFailureKind.RequiredCapabilitiesUnavailable, result.Failure?.Kind);
        Assert.Equal("DD0404", result.Failure?.Code);
    }

    /// <summary>
    ///     Proves a full satisfier outranks a partial one regardless of priority (RankOrdering).
    /// </summary>
    [Fact]
    public void ExtractorSelector_Select_FullSatisfierVersusHigherPriorityPartial_SelectsFullSatisfier()
    {
        // Arrange: images are required; a full satisfier competes with a higher-priority partial one
        var selector = new ExtractorSelector();
        var options = new ExtractionOptions { IncludeEmbeddedImages = true };
        var candidates = new[]
        {
            Candidate("full", ExtractorCapabilities.Text | ExtractorCapabilities.EmbeddedImages, priority: 0),
            Candidate("partial", ExtractorCapabilities.Text, priority: 100)
        };

        // Act: select with the embedded-images requirement
        var result = selector.Select(TextDetection(), options, candidates);

        // Assert: full fidelity dominates priority, so the full satisfier wins
        Assert.Equal("full", result.Selected?.Id);
    }

    /// <summary>
    ///     Proves that among partials the greater satisfied-capability count wins over priority (RankOrdering).
    /// </summary>
    [Fact]
    public void ExtractorSelector_Select_AmongPartials_GreaterSatisfiedCountBeatsPriority()
    {
        // Arrange: text, images, and pages are all required, but no backend provides all three
        var selector = new ExtractorSelector();
        var options = new ExtractionOptions { IncludeEmbeddedImages = true, RenderPages = true };
        var candidates = new[]
        {
            Candidate("two", ExtractorCapabilities.Text | ExtractorCapabilities.EmbeddedImages, priority: 1),
            Candidate("one", ExtractorCapabilities.Text, priority: 100)
        };

        // Act: select among partial satisfiers
        var result = selector.Select(TextDetection(), options, candidates);

        // Assert: satisfying two of three requested capabilities beats a higher priority satisfying one
        Assert.Equal("two", result.Selected?.Id);
    }

    /// <summary>
    ///     Proves that between equal-fidelity, equal-count backends the higher priority wins (RankOrdering).
    /// </summary>
    [Fact]
    public void ExtractorSelector_Select_EqualFidelityAndCount_HigherPriorityWins()
    {
        // Arrange: two full text satisfiers, differing only in priority
        var selector = new ExtractorSelector();
        var candidates = new[]
        {
            Candidate("apple", ExtractorCapabilities.Text, priority: 5),
            Candidate("banana", ExtractorCapabilities.Text, priority: 10)
        };

        // Act: select between the two full satisfiers
        var result = selector.Select(TextDetection(), TextOnlyOptions(), candidates);

        // Assert: priority breaks the tie ahead of identifier ordering
        Assert.Equal("banana", result.Selected?.Id);
    }

    /// <summary>
    ///     Proves that with equal fidelity, count, and priority the lowest identifier wins (DeterministicTieBreak).
    /// </summary>
    [Fact]
    public void ExtractorSelector_Select_EqualPriorityFullSatisfiers_LowerIdentifierWins()
    {
        // Arrange: two identical full satisfiers whose only difference is their identifier
        var selector = new ExtractorSelector();
        var candidates = new[]
        {
            Candidate("zebra", ExtractorCapabilities.Text, priority: 5),
            Candidate("alpha", ExtractorCapabilities.Text, priority: 5)
        };

        // Act: select between the two indistinguishable-but-for-id backends
        var result = selector.Select(TextDetection(), TextOnlyOptions(), candidates);

        // Assert: the ordinal-ascending identifier is the final deterministic tie-break
        Assert.Equal("alpha", result.Selected?.Id);
    }

    /// <summary>
    ///     Proves every candidate receives a verdict, with the selected candidate traced first (CandidateTrace).
    /// </summary>
    [Fact]
    public void ExtractorSelector_Select_MultipleCandidates_TraceHasVerdictPerCandidateSelectedFirst()
    {
        // Arrange: a winner, an outranked full satisfier, and a format mismatch
        var selector = new ExtractorSelector();
        var candidates = new[]
        {
            Candidate("winner", ExtractorCapabilities.Text, priority: 10),
            Candidate("runnerup", ExtractorCapabilities.Text, priority: 1),
            Candidate("wrongformat", ExtractorCapabilities.Text, formats: [DocumentFormat.Pdf])
        };

        // Act: select and inspect the trace
        var result = selector.Select(TextDetection(), TextOnlyOptions(), candidates);

        // Assert: one verdict per candidate, the selected one first, then ordered by identifier
        Assert.Equal(3, result.Trace.Count);
        Assert.Equal(CandidateOutcome.Selected, result.Trace[0].Outcome);
        Assert.Equal("winner", result.Trace[0].ExtractorId);
        Assert.Contains(result.Trace, verdict => verdict.ExtractorId == "runnerup");
        Assert.Contains(result.Trace, verdict => verdict.ExtractorId == "wrongformat");
    }

    /// <summary>
    ///     Proves identical inputs produce identical selection results (Repeatability).
    /// </summary>
    [Fact]
    public void ExtractorSelector_Select_EqualInputsRepeated_ProduceIdenticalResults()
    {
        // Arrange: a fixed selector, detection, options, and candidate set
        var selector = new ExtractorSelector();
        var candidates = new[]
        {
            Candidate("a", ExtractorCapabilities.Text, priority: 1),
            Candidate("b", ExtractorCapabilities.Text, priority: 2)
        };

        // Act: select twice with the same inputs
        var first = selector.Select(TextDetection(), TextOnlyOptions(), candidates);
        var second = selector.Select(TextDetection(), TextOnlyOptions(), candidates);

        // Assert: the pure function yields equal winners and equal ordered traces
        Assert.Equal(first.Selected?.Id, second.Selected?.Id);
        Assert.Equal(first.Trace, second.Trace);
    }

    /// <summary>
    ///     Proves the outcome does not depend on the order the candidates are supplied in (Repeatability).
    /// </summary>
    [Fact]
    public void ExtractorSelector_Select_ShuffledCandidateOrder_ProducesSameWinnerAndTraceOrder()
    {
        // Arrange: the same three candidates presented in two different registration orders
        var selector = new ExtractorSelector();
        var one = Candidate("gamma", ExtractorCapabilities.Text, priority: 5);
        var two = Candidate("alpha", ExtractorCapabilities.Text, priority: 5);
        var three = Candidate("beta", ExtractorCapabilities.Text, priority: 5);
        var forward = new[] { one, two, three };
        var shuffled = new[] { three, one, two };

        // Act: select from both orderings
        var forwardResult = selector.Select(TextDetection(), TextOnlyOptions(), forward);
        var shuffledResult = selector.Select(TextDetection(), TextOnlyOptions(), shuffled);

        // Assert: the winner and the ordered trace are identical regardless of input order
        Assert.Equal(forwardResult.Selected?.Id, shuffledResult.Selected?.Id);
        Assert.Equal(
            forwardResult.Trace.Select(verdict => verdict.ExtractorId),
            shuffledResult.Trace.Select(verdict => verdict.ExtractorId));
    }

    /// <summary>
    ///     Creates an available candidate for the given effective capabilities, priority, and formats.
    /// </summary>
    /// <param name="id">The candidate identifier.</param>
    /// <param name="effective">The effective capabilities used for both the descriptor and availability.</param>
    /// <param name="priority">The ranking priority.</param>
    /// <param name="formats">The supported formats, defaulting to plain text.</param>
    /// <returns>The configured available candidate.</returns>
    /// <remarks>Effective and declared capabilities are set equal so selection reasons only about the effective set.</remarks>
    private static ExtractorCandidate Candidate(
        string id, ExtractorCapabilities effective, int priority = 0, IReadOnlyList<DocumentFormat>? formats = null) =>
        new(
            new ExtractorDescriptor(id, id + " backend", formats ?? [DocumentFormat.Text], effective, priority),
            ExtractorAvailability.Available(effective));

    /// <summary>
    ///     Creates a text-format candidate that reports itself unavailable with the given reason.
    /// </summary>
    /// <param name="id">The candidate identifier.</param>
    /// <param name="reason">The unavailability reason surfaced in the trace.</param>
    /// <param name="priority">The ranking priority.</param>
    /// <returns>The configured unavailable candidate.</returns>
    /// <remarks>Used to exercise the availability filter and the override never-falls-back rule.</remarks>
    private static ExtractorCandidate Unavailable(string id, string reason, int priority = 0) =>
        new(
            new ExtractorDescriptor(id, id + " backend", [DocumentFormat.Text], ExtractorCapabilities.Text, priority),
            ExtractorAvailability.Unavailable(reason));

    /// <summary>
    ///     Creates options whose only required capability is text.
    /// </summary>
    /// <returns>Options with embedded images disabled so the required set is text alone.</returns>
    /// <remarks>Keeps ranking tests focused on fidelity, count, priority, and identifier tie-breaks.</remarks>
    private static ExtractionOptions TextOnlyOptions() => new() { IncludeEmbeddedImages = false };

    /// <summary>
    ///     Creates a text-format detection for selector tests.
    /// </summary>
    /// <returns>A text-format detection by extension.</returns>
    /// <remarks>Selection depends only on the detected format, so a simple extension detection suffices.</remarks>
    private static FormatDetection TextDetection() => new(DocumentFormat.Text, DetectionBasis.Extension, 0.5);
}
