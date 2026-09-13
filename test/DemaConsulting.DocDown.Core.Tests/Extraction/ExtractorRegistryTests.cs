using DemaConsulting.DocDown.TestSupport;
using DocDown.Core;

namespace DemaConsulting.DocDown.Core.Tests.Extraction;

/// <summary>
///     Unit tests for <see cref="ExtractorRegistry"/>, exercising descriptor exposure, identifier
///     lookup, lazy availability probing with caching, throwing-probe containment, explicit
///     re-probing, and candidate enumeration.
/// </summary>
/// <remarks>
///     These tests construct the registry directly through its internal constructor (visible via
///     <c>InternalsVisibleTo</c>). Each is named for the unit requirement it evidences: descriptor
///     exposure, lookup by id, probe caching, probe-failure containment, refresh availability, and
///     candidate enumeration.
/// </remarks>
public class ExtractorRegistryTests
{
    /// <summary>
    ///     Proves the registry exposes a descriptor per extractor in registration order (DescriptorExposure).
    /// </summary>
    [Fact]
    public void ExtractorRegistry_Descriptors_TwoExtractors_ExposedInRegistrationOrder()
    {
        // Arrange: a registry built from two extractors with distinct identities and abilities
        var registry = new ExtractorRegistry(Factories(
            StubExtractor.Available("alpha", [DocumentFormat.Text], ExtractorCapabilities.Text),
            new StubExtractor
            {
                Id = "beta",
                DisplayName = "Beta backend",
                SupportedFormats = [DocumentFormat.Pdf],
                Capabilities = ExtractorCapabilities.Text | ExtractorCapabilities.RenderedPages,
                Priority = 7
            }));

        // Act: read the exposed descriptors
        var descriptors = registry.Descriptors;

        // Assert: both descriptors appear in registration order with their declared identity and abilities
        Assert.Equal(2, descriptors.Count);
        Assert.Equal("alpha", descriptors[0].Id);
        Assert.Equal("beta", descriptors[1].Id);
        Assert.Equal("Beta backend", descriptors[1].DisplayName);
        Assert.Equal(7, descriptors[1].Priority);
        Assert.Contains(DocumentFormat.Pdf, descriptors[1].SupportedFormats);
    }

    /// <summary>
    ///     Proves a registered extractor resolves to its instance by identifier (LookupById).
    /// </summary>
    [Fact]
    public void ExtractorRegistry_Resolve_KnownId_ReturnsRegisteredInstance()
    {
        // Arrange: a registry with a single named extractor
        var extractor = StubExtractor.Available("target", [DocumentFormat.Text], ExtractorCapabilities.Text);
        var registry = new ExtractorRegistry(Factories(extractor));

        // Act: resolve the extractor by its identifier
        var resolved = registry.Resolve("target");

        // Assert: the exact registered instance is returned
        Assert.Same(extractor, resolved);
    }

    /// <summary>
    ///     Proves resolving an unknown identifier throws a key-not-found exception (LookupById boundary).
    /// </summary>
    [Fact]
    public void ExtractorRegistry_Resolve_UnknownId_ThrowsKeyNotFoundException()
    {
        // Arrange: a registry with one unrelated extractor
        var registry = new ExtractorRegistry(Factories(
            StubExtractor.Available("known", [DocumentFormat.Text], ExtractorCapabilities.Text)));

        // Act + Assert: an unknown identifier fails loudly
        Assert.Throws<KeyNotFoundException>(() => registry.Resolve("missing"));
    }

    /// <summary>
    ///     Proves resolving a null or empty identifier is rejected (LookupById boundary).
    /// </summary>
    [Fact]
    public void ExtractorRegistry_Resolve_EmptyId_ThrowsArgumentException()
    {
        // Arrange: a registry with one extractor
        var registry = new ExtractorRegistry(Factories(
            StubExtractor.Available("known", [DocumentFormat.Text], ExtractorCapabilities.Text)));

        // Act + Assert: an empty identifier resolves nothing and is a caller error
        Assert.Throws<ArgumentException>(() => registry.Resolve(string.Empty));
    }

    /// <summary>
    ///     Proves resolving a null identifier is rejected (LookupById boundary).
    /// </summary>
    [Fact]
    public void ExtractorRegistry_Resolve_NullId_ThrowsArgumentNullException()
    {
        // Arrange: a registry with one extractor
        var registry = new ExtractorRegistry(Factories(
            StubExtractor.Available("known", [DocumentFormat.Text], ExtractorCapabilities.Text)));

        // Act + Assert: the null branch of the guard is a distinct code path from the empty branch,
        // and the requirement covers both, so it is asserted separately
        Assert.Throws<ArgumentNullException>(() => registry.Resolve(null!));
    }

    /// <summary>
    ///     Proves availability is probed once and cached across repeated candidate enumerations (ProbeCaching).
    /// </summary>
    [Fact]
    public void ExtractorRegistry_GetCandidates_RepeatedCalls_ProbesOnce()
    {
        // Arrange: a registry whose extractor counts how many times it is probed
        var counting = new CountingProbeExtractor("counter");
        var registry = new ExtractorRegistry(Factories(counting));

        // Act: enumerate candidates several times and read the diagnostics
        registry.GetCandidates();
        registry.GetCandidates();
        _ = registry.AvailabilityDiagnostics;

        // Assert: the availability probe ran exactly once because the result is cached
        Assert.Equal(1, counting.ProbeCount);
    }

    /// <summary>
    ///     Proves a throwing availability probe is contained as unavailable with a DD0602 diagnostic (ProbeFailureContainment).
    /// </summary>
    [Fact]
    public void ExtractorRegistry_GetCandidates_ThrowingProbe_ContainedAsUnavailableWithDiagnostic()
    {
        // Arrange: a registry with a backend whose availability probe throws
        var registry = new ExtractorRegistry(Factories(
            StubExtractor.ThrowingProbe("throwing", [DocumentFormat.Text])));

        // Act: enumerate candidates, forcing the contained probe pass
        var candidate = Assert.Single(registry.GetCandidates());
        var diagnostic = Assert.Single(registry.AvailabilityDiagnostics);

        // Assert: the throwing probe is unavailable, the reason names the exception type, and DD0602 is recorded
        Assert.False(candidate.Availability.IsAvailable);
        Assert.Equal("availability probe failed: InvalidOperationException", candidate.Availability.UnavailableReason);
        Assert.Equal("DD0602", diagnostic.Code);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
    }

    /// <summary>
    ///     Proves an ordinarily unavailable backend records a DD0601 informational diagnostic (ProbeFailureContainment).
    /// </summary>
    [Fact]
    public void ExtractorRegistry_GetCandidates_UnavailableBackend_RecordsInfoDiagnostic()
    {
        // Arrange: a registry with a backend that reports itself unavailable with a reason
        const string reason = "the native library is not installed";
        var registry = new ExtractorRegistry(Factories(
            StubExtractor.Unavailable("offline", [DocumentFormat.Text], reason)));

        // Act: enumerate candidates and read the diagnostic
        var candidate = Assert.Single(registry.GetCandidates());
        var diagnostic = Assert.Single(registry.AvailabilityDiagnostics);

        // Assert: the backend is unavailable with its reason and the exclusion is recorded as DD0601 info
        Assert.False(candidate.Availability.IsAvailable);
        Assert.Equal("DD0601", diagnostic.Code);
        Assert.Equal(DiagnosticSeverity.Info, diagnostic.Severity);
        Assert.Contains(reason, diagnostic.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves RefreshAvailability discards the cache so the next enumeration re-probes (RefreshAvailability).
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

        // Assert: the refresh forced a second probe pass
        Assert.Equal(2, counting.ProbeCount);
    }

    /// <summary>
    ///     Proves candidate enumeration yields one candidate per extractor with its availability (CandidateEnumeration).
    /// </summary>
    [Fact]
    public void ExtractorRegistry_GetCandidates_MixedAvailability_YieldsOneCandidatePerExtractor()
    {
        // Arrange: a registry with one available and one unavailable backend
        var registry = new ExtractorRegistry(Factories(
            StubExtractor.Available("up", [DocumentFormat.Text], ExtractorCapabilities.Text),
            StubExtractor.Unavailable("down", [DocumentFormat.Text], "offline")));

        // Act: enumerate the candidates
        var candidates = registry.GetCandidates();

        // Assert: each extractor is represented once with its own availability
        Assert.Equal(2, candidates.Count);
        Assert.True(candidates.Single(candidate => candidate.Descriptor.Id == "up").Availability.IsAvailable);
        Assert.False(candidates.Single(candidate => candidate.Descriptor.Id == "down").Availability.IsAvailable);
    }

    /// <summary>
    ///     Proves the constructor rejects a duplicate identifier across extractors (build-time invariant).
    /// </summary>
    [Fact]
    public void ExtractorRegistry_Construct_DuplicateIds_ThrowsArgumentException()
    {
        // Arrange: two factories producing extractors that share an identifier
        var factories = Factories(
            StubExtractor.Available("dup", [DocumentFormat.Text], ExtractorCapabilities.Text),
            StubExtractor.Available("dup", [DocumentFormat.Pdf], ExtractorCapabilities.Text));

        // Act + Assert: identifier uniqueness is a hard build-time invariant
        Assert.Throws<ArgumentException>(() => new ExtractorRegistry(factories));
    }

    /// <summary>
    ///     Wraps ready extractor instances as the factory list the registry constructor expects.
    /// </summary>
    /// <param name="extractors">The extractor instances to wrap.</param>
    /// <returns>A registration-ordered list of factories returning those instances.</returns>
    /// <remarks>Keeps each test declarative by hiding the instance-to-factory wrapping.</remarks>
    private static IReadOnlyList<Func<IDocumentExtractor>> Factories(params IDocumentExtractor[] extractors) =>
        extractors.Select<IDocumentExtractor, Func<IDocumentExtractor>>(extractor => () => extractor).ToList();

    /// <summary>
    ///     An extractor that counts how many times its availability probe is invoked.
    /// </summary>
    /// <remarks>
    ///     Used to prove the registry probes availability lazily and caches the result, re-probing
    ///     only after an explicit refresh. Never used for a real extraction.
    /// </remarks>
    private sealed class CountingProbeExtractor : IDocumentExtractor
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="CountingProbeExtractor"/> class.
        /// </summary>
        /// <param name="id">The extractor identifier.</param>
        /// <remarks>The identifier is the only configurable trait these tests need.</remarks>
        public CountingProbeExtractor(string id) => Id = id;

        /// <summary>Gets the number of times <see cref="ProbeAvailability"/> has been called.</summary>
        /// <remarks>Asserted by the caching and refresh tests to prove the probe cadence.</remarks>
        public int ProbeCount { get; private set; }

        /// <inheritdoc/>
        public string Id { get; }

        /// <inheritdoc/>
        public string DisplayName => Id + " (counting)";

        /// <inheritdoc/>
        public IReadOnlyCollection<DocumentFormat> SupportedFormats => [DocumentFormat.Text];

        /// <inheritdoc/>
        public ExtractorCapabilities Capabilities => ExtractorCapabilities.Text;

        /// <inheritdoc/>
        public int Priority => 0;

        /// <inheritdoc/>
        public ExtractorAvailability ProbeAvailability()
        {
            ProbeCount++;
            return ExtractorAvailability.Available(ExtractorCapabilities.Text);
        }

        /// <inheritdoc/>
        public ValueTask<ExtractionOutcome> ExtractAsync(DocumentSource source, IExtractionContext context) =>
            ValueTask.FromResult(ExtractionOutcome.Succeeded);
    }
}
