using DemaConsulting.DocDown.TestSupport;
using DocDown.Core;

namespace DemaConsulting.DocDown.Core.Tests.Extraction;

/// <summary>
///     Unit tests for <see cref="ExtractorRegistry"/>, exercising descriptor exposure, identifier
///     lookup, lazy availability probing with caching, probe-failure containment, explicit
///     re-probing, and candidate enumeration.
/// </summary>
/// <remarks>
///     These tests construct the registry directly through its internal constructor and assert the
///     new reduced candidate surface: descriptor plus availability only.
/// </remarks>
public class ExtractorRegistryTests
{
    /// <summary>
    ///     Proves the registry exposes one descriptor per extractor in registration order.
    /// </summary>
    [Fact]
    public void ExtractorRegistry_Descriptors_TwoExtractors_AppearInRegistrationOrder()
    {
        // Arrange: a registry built from two distinct extractors
        var registry = new ExtractorRegistry(Factories(
            StubExtractor.Available("alpha", [DocumentFormat.Text]),
            new StubExtractor
            {
                Id = "beta",
                DisplayName = "Beta backend",
                SupportedFormats = [DocumentFormat.Pdf],
                ProvidesRenderedPages = true,
                Priority = 7
            }));

        // Act: read the exposed descriptors
        var descriptors = registry.Descriptors;

        // Assert: both descriptors appear with their identity and ranking data intact
        Assert.Equal(2, descriptors.Count);
        Assert.Equal("alpha", descriptors[0].Id);
        Assert.Equal("beta", descriptors[1].Id);
        Assert.Equal("Beta backend", descriptors[1].DisplayName);
        Assert.Equal(7, descriptors[1].Priority);
        Assert.Contains(DocumentFormat.Pdf, descriptors[1].SupportedFormats);
    }

    /// <summary>
    ///     Proves a registered extractor resolves to its original instance by identifier.
    /// </summary>
    [Fact]
    public void ExtractorRegistry_Resolve_KnownId_ReturnsRegisteredInstance()
    {
        // Arrange: a registry with one named extractor
        var extractor = StubExtractor.Available("target", [DocumentFormat.Text]);
        var registry = new ExtractorRegistry(Factories(extractor));

        // Act: resolve by identifier
        var resolved = registry.Resolve("target");

        // Assert: the exact registered instance is returned
        Assert.Same(extractor, resolved);
    }

    /// <summary>
    ///     Proves resolving an unknown identifier fails loudly.
    /// </summary>
    [Fact]
    public void ExtractorRegistry_Resolve_UnknownId_ThrowsKeyNotFoundException()
    {
        // Arrange: a registry with one unrelated extractor
        var registry = new ExtractorRegistry(Factories(StubExtractor.Available("known", [DocumentFormat.Text])));

        // Act / Assert: the missing identifier is rejected
        Assert.Throws<KeyNotFoundException>(() => registry.Resolve("missing"));
    }

    /// <summary>
    ///     Proves resolving an empty identifier is rejected as a caller error.
    /// </summary>
    [Fact]
    public void ExtractorRegistry_Resolve_EmptyId_ThrowsArgumentException()
    {
        // Arrange: a registry with one extractor
        var registry = new ExtractorRegistry(Factories(StubExtractor.Available("known", [DocumentFormat.Text])));

        // Act / Assert: an empty identifier is invalid
        Assert.Throws<ArgumentException>(() => registry.Resolve(string.Empty));
    }

    /// <summary>
    ///     Proves resolving a null identifier is rejected as a caller error.
    /// </summary>
    [Fact]
    public void ExtractorRegistry_Resolve_NullId_ThrowsArgumentNullException()
    {
        // Arrange: a registry with one extractor
        var registry = new ExtractorRegistry(Factories(StubExtractor.Available("known", [DocumentFormat.Text])));

        // Act / Assert: the null guard is distinct from the empty-string guard
        Assert.Throws<ArgumentNullException>(() => registry.Resolve(null!));
    }

    /// <summary>
    ///     Proves availability is probed once and cached across repeated candidate enumerations.
    /// </summary>
    [Fact]
    public void ExtractorRegistry_GetCandidates_RepeatedCalls_ProbesOnce()
    {
        // Arrange: a registry whose extractor counts its availability probes
        var counting = new CountingProbeExtractor("counter");
        var registry = new ExtractorRegistry(Factories(counting));

        // Act: enumerate candidates several times
        registry.GetCandidates();
        registry.GetCandidates();

        // Assert: the probe ran only once because the result was cached
        Assert.Equal(1, counting.ProbeCount);
    }

    /// <summary>
    ///     Proves a throwing availability probe is contained as an unavailable candidate.
    /// </summary>
    [Fact]
    public void ExtractorRegistry_GetCandidates_ThrowingProbe_ContainedAsUnavailable()
    {
        // Arrange: a registry with a backend whose availability probe throws
        var registry = new ExtractorRegistry(Factories(
            StubExtractor.ThrowingProbe("throwing", [DocumentFormat.Text])));

        // Act: enumerate candidates, forcing the contained probe pass
        var candidate = Assert.Single(registry.GetCandidates());

        // Assert: the throwing probe is surfaced as ordinary unavailability with a stable reason
        Assert.False(candidate.Availability.IsAvailable);
        Assert.Equal("availability probe failed: InvalidOperationException", candidate.Availability.UnavailableReason);
    }

    /// <summary>
    ///     Proves an ordinarily unavailable backend keeps its reason on the candidate surface.
    /// </summary>
    [Fact]
    public void ExtractorRegistry_GetCandidates_UnavailableBackend_PreservesReason()
    {
        // Arrange: a registry with one backend that reports itself unavailable
        const string reason = "the native library is not installed";
        var registry = new ExtractorRegistry(Factories(
            StubExtractor.Unavailable("offline", [DocumentFormat.Text], reason)));

        // Act: enumerate the candidates
        var candidate = Assert.Single(registry.GetCandidates());

        // Assert: the candidate stays unavailable and carries its reason verbatim
        Assert.False(candidate.Availability.IsAvailable);
        Assert.Equal(reason, candidate.Availability.UnavailableReason);
    }

    /// <summary>
    ///     Proves refreshing availability discards the cache so the next enumeration re-probes.
    /// </summary>
    [Fact]
    public void ExtractorRegistry_RefreshAvailability_AfterCaching_ReprobesOnNextEnumeration()
    {
        // Arrange: a registry whose extractor counts probes
        var counting = new CountingProbeExtractor("counter");
        var registry = new ExtractorRegistry(Factories(counting));

        // Act: probe once, refresh, then probe again
        registry.GetCandidates();
        registry.RefreshAvailability();
        registry.GetCandidates();

        // Assert: the second enumeration performed a fresh probe
        Assert.Equal(2, counting.ProbeCount);
    }

    /// <summary>
    ///     Proves candidate enumeration yields one candidate per extractor with its availability.
    /// </summary>
    [Fact]
    public void ExtractorRegistry_GetCandidates_MixedAvailability_YieldsOneCandidatePerExtractor()
    {
        // Arrange: one available and one unavailable backend
        var registry = new ExtractorRegistry(Factories(
            StubExtractor.Available("up", [DocumentFormat.Text]),
            StubExtractor.Unavailable("down", [DocumentFormat.Text], "offline")));

        // Act: enumerate the candidates
        var candidates = registry.GetCandidates();

        // Assert: each backend is represented once with its own availability
        Assert.Equal(2, candidates.Count);
        Assert.True(candidates.Single(candidate => candidate.Descriptor.Id == "up").Availability.IsAvailable);
        Assert.False(candidates.Single(candidate => candidate.Descriptor.Id == "down").Availability.IsAvailable);
    }

    /// <summary>
    ///     Proves duplicate extractor identifiers are rejected at construction time.
    /// </summary>
    [Fact]
    public void ExtractorRegistry_Construct_DuplicateIds_ThrowsArgumentException()
    {
        // Arrange: two factories producing the same identifier
        var factories = Factories(
            StubExtractor.Available("dup", [DocumentFormat.Text]),
            StubExtractor.Available("dup", [DocumentFormat.Pdf]));

        // Act / Assert: identifier uniqueness is a hard invariant
        Assert.Throws<ArgumentException>(() => new ExtractorRegistry(factories));
    }

    /// <summary>
    ///     Wraps ready extractor instances as the factory list the registry constructor expects.
    /// </summary>
    /// <param name="extractors">The extractor instances to wrap.</param>
    /// <returns>The registration-ordered factory list.</returns>
    private static IReadOnlyList<Func<IDocumentExtractor>> Factories(params IDocumentExtractor[] extractors) =>
        extractors.Select<IDocumentExtractor, Func<IDocumentExtractor>>(extractor => () => extractor).ToList();

    /// <summary>
    ///     An extractor that counts how many times its availability probe is invoked.
    /// </summary>
    /// <remarks>
    ///     Used to prove the registry probes lazily, caches the result, and re-probes only after an
    ///     explicit refresh.
    /// </remarks>
    private sealed class CountingProbeExtractor : IDocumentExtractor
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="CountingProbeExtractor"/> class.
        /// </summary>
        /// <param name="id">The extractor identifier.</param>
        public CountingProbeExtractor(string id) => Id = id;

        /// <summary>Gets the number of times <see cref="ProbeAvailability"/> has been called.</summary>
        public int ProbeCount { get; private set; }

        /// <inheritdoc />
        public string Id { get; }

        /// <inheritdoc />
        public string DisplayName => Id + " (counting)";

        /// <inheritdoc />
        public IReadOnlyCollection<DocumentFormat> SupportedFormats => [DocumentFormat.Text];

        /// <inheritdoc />
        public int Priority => 0;

        /// <inheritdoc />
        public ExtractorAvailability ProbeAvailability()
        {
            ProbeCount++;
            return ExtractorAvailability.Available();
        }

        /// <inheritdoc />
        public ValueTask<ExtractionOutcome> ExtractAsync(DocumentSource source, IExtractionContext context) =>
            ValueTask.FromResult(ExtractionOutcome.Produced);
    }
}
