namespace DocDown.Core;

/// <summary>
///     An immutable snapshot of the registered extractors, providing their descriptors, cached
///     availability, and identifier-based resolution.
/// </summary>
/// <remarks>
///     <para>
///         The registry materializes every registered factory <strong>once, eagerly, in its
///         constructor</strong>, so duplicate-identifier detection and descriptor construction happen
///         at build time rather than being deferred to the first extraction. The set of instances and
///         their descriptors is therefore fixed for the registry's lifetime.
///     </para>
///     <para>
///         Availability is <strong>probed lazily and cached per registry instance</strong>: an Office
///         installation does not appear part-way through a run, so re-probing on every extraction
///         would be wasteful and could produce inconsistent decisions within one run.
///         <see cref="RefreshAvailability"/> is the explicit escape hatch that clears the cache so the
///         next query re-probes. Because the cache is populated on demand, access is guarded by a lock
///         so concurrent readers observe a single, consistent probe pass.
///     </para>
///     <para>
///         Core does not trust the extractor contract: <see cref="IDocumentExtractor.ProbeAvailability"/>
///         is invoked inside a <c>try/catch</c>, and any thrown exception (including
///         <see cref="OperationCanceledException"/>, which a probe has no token to justify) is treated
///         as unavailability with a stable reason. Descriptors, instances, and (once computed)
///         candidates are immutable, so the registry is safe for concurrent reads.
///     </para>
/// </remarks>
public sealed class ExtractorRegistry
{
    /// <summary>The gate serializing lazy availability probing and cache invalidation.</summary>
    /// <remarks>Ensures a single consistent probe pass populates the cache even under concurrent access.</remarks>
    private readonly object _availabilityLock = new();

    /// <summary>The materialized extractor instances in registration order.</summary>
    /// <remarks>Created once in the constructor so lifetime and identity are fixed for the registry.</remarks>
    private readonly IReadOnlyList<IDocumentExtractor> _extractors;

    /// <summary>The extractor descriptors in registration order, aligned with <see cref="_extractors"/>.</summary>
    /// <remarks>Snapshots identity and declared abilities so selection never invokes an extractor to read them.</remarks>
    private readonly IReadOnlyList<ExtractorDescriptor> _descriptors;

    /// <summary>The cached candidates (descriptor plus probed availability), or <see langword="null"/> before the first probe.</summary>
    /// <remarks>Guarded by <see cref="_availabilityLock"/>; cleared by <see cref="RefreshAvailability"/>.</remarks>
    private IReadOnlyList<ExtractorCandidate>? _candidates;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ExtractorRegistry"/> class from extractor
    ///     factories.
    /// </summary>
    /// <param name="factories">The extractor factories to materialize, in registration order. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="factories"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when a factory returns <see langword="null"/>, produces an extractor with a null or
    ///     empty identifier, or produces two extractors that share an identifier — identifiers are the
    ///     override key and the manifest key, so their uniqueness is a hard build-time invariant.
    /// </exception>
    /// <remarks>
    ///     Invokes every factory exactly once here so any construction fault or duplicate identifier
    ///     surfaces at build time, not mid-extraction. The constructor does not probe availability;
    ///     that is deferred to first use so building an engine performs no environment inspection.
    ///     <see langword="internal"/> because only <see cref="DocDownBuilder"/> constructs a registry.
    /// </remarks>
    internal ExtractorRegistry(IReadOnlyList<Func<IDocumentExtractor>> factories)
    {
        // A registry must have a factory list to materialize, even if it is empty
        ArgumentNullException.ThrowIfNull(factories);

        var extractors = new List<IDocumentExtractor>(factories.Count);
        var descriptors = new List<ExtractorDescriptor>(factories.Count);
        var seenIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var factory in factories)
        {
            // A null factory cannot produce an extractor and is a registration error
            ArgumentNullException.ThrowIfNull(factory);

            // Materialize once; a factory that returns null violates the registration contract
            var extractor = factory() ??
                throw new ArgumentException("An extractor factory returned null.", nameof(factories));

            // An extractor without a stable identifier cannot be overridden or recorded
            if (string.IsNullOrEmpty(extractor.Id))
            {
                throw new ArgumentException("An extractor was registered with a null or empty identifier.", nameof(factories));
            }

            // Identifiers are the override and manifest key, so a collision is a hard invariant failure
            if (!seenIds.Add(extractor.Id))
            {
                throw new ArgumentException($"Duplicate extractor identifier '{extractor.Id}'.", nameof(factories));
            }

            extractors.Add(extractor);
            descriptors.Add(DescribeExtractor(extractor));
        }

        _extractors = extractors;
        _descriptors = descriptors;
    }

    /// <summary>
    ///     Gets the registered extractor descriptors in registration order.
    /// </summary>
    /// <remarks>Immutable and probe-free, so reading it never inspects the environment.</remarks>
    public IReadOnlyList<ExtractorDescriptor> Descriptors => _descriptors;

    /// <summary>
    ///     Gets the materialized extractor instances in registration order.
    /// </summary>
    /// <remarks>
    ///     Exposed so the engine can enumerate backends for self-test contribution; the list is fixed
    ///     for the registry's lifetime.
    /// </remarks>
    public IReadOnlyList<IDocumentExtractor> Extractors => _extractors;

    /// <summary>
    ///     Gets the candidates (descriptor plus cached availability) for every registered extractor.
    /// </summary>
    /// <returns>The candidates in registration order, using cached availability.</returns>
    /// <remarks>
    ///     Triggers a lazy probe on first access, then returns the cached result until
    ///     <see cref="RefreshAvailability"/> is called. The returned list is immutable and safe to hand
    ///     to <see cref="ExtractorSelector"/>, which must not re-probe.
    /// </remarks>
    public IReadOnlyList<ExtractorCandidate> GetCandidates()
    {
        EnsureProbed();
        return _candidates!;
    }

    /// <summary>
    ///     Resolves a registered extractor instance by identifier.
    /// </summary>
    /// <param name="id">The extractor identifier to resolve. Must not be null or empty.</param>
    /// <returns>The extractor instance whose identifier equals <paramref name="id"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> is null or empty.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when no registered extractor has the given identifier.</exception>
    /// <remarks>
    ///     Throws <see cref="KeyNotFoundException"/> for an unknown identifier so a lookup bug fails
    ///     loudly; the engine never relies on that throwing path, because it only resolves an
    ///     identifier that selection already confirmed exists. Probe-free and thread-safe.
    /// </remarks>
    public IDocumentExtractor Resolve(string id)
    {
        // An identifier is required to resolve anything
        ArgumentException.ThrowIfNullOrEmpty(id);

        // A linear scan is adequate for the small, fixed extractor set and keeps identity semantics simple
        var extractor = _extractors.FirstOrDefault(candidate => string.Equals(candidate.Id, id, StringComparison.Ordinal));
        return extractor ?? throw new KeyNotFoundException($"No registered extractor has the identifier '{id}'.");
    }

    /// <summary>
    ///     Discards the cached availability so the next query re-probes every extractor.
    /// </summary>
    /// <remarks>
    ///     The explicit escape hatch for the rare case where the environment genuinely changed (for
    ///     example a backend was installed) during a long-lived process. Thread-safe: the cache is
    ///     cleared under the same lock that populates it, so a concurrent query either sees the old
    ///     cache or performs a fresh probe, never a torn state.
    /// </remarks>
    public void RefreshAvailability()
    {
        // Clear under the lock so a concurrent EnsureProbed observes a consistent transition
        lock (_availabilityLock)
        {
            _candidates = null;
        }
    }

    /// <summary>
    ///     Ensures availability has been probed and the candidate and diagnostic caches are populated.
    /// </summary>
    /// <remarks>
    ///     Double-checks the cache under the lock so exactly one probe pass runs even when several
    ///     threads race to the first query. Each extractor's probe is contained so one misbehaving
    ///     backend cannot fault the pass. Side effect: populates the caches; performs the extractors'
    ///     (contractually cheap) probes.
    /// </remarks>
    private void EnsureProbed()
    {
        // Fast path: a populated cache needs no lock to read an immutable reference
        if (_candidates is not null)
        {
            return;
        }

        lock (_availabilityLock)
        {
            // Re-check inside the lock so only the first thread through performs the probe pass
            if (_candidates is not null)
            {
                return;
            }

            var candidates = new List<ExtractorCandidate>(_extractors.Count);

            for (var index = 0; index < _extractors.Count; index++)
            {
                var extractor = _extractors[index];
                var descriptor = _descriptors[index];
                var availability = ProbeSafely(extractor);
                candidates.Add(new ExtractorCandidate(descriptor, availability));
            }

            _candidates = candidates;
        }
    }

    /// <summary>
    ///     Probes one extractor's availability, containing any fault.
    /// </summary>
    /// <param name="extractor">The extractor to probe.</param>
    /// <returns>
    ///     The extractor's reported availability, or a synthesized unavailable result when the probe
    ///     threw or returned nothing.
    /// </returns>
    /// <remarks>
    ///     Core does not trust the interface contract, so every outcome of the probe is handled here: a
    ///     thrown exception (any type, including <see cref="OperationCanceledException"/>, since a probe
    ///     has no cancellation token to justify one) and a null result both become unavailability with
    ///     a stable reason. The reason surfaces later as an environment fact. Contains the extractor's
    ///     side effects but performs none of its own.
    /// </remarks>
    private static ExtractorAvailability ProbeSafely(IDocumentExtractor extractor)
    {
        ExtractorAvailability? availability;
        try
        {
            // Trust nothing: a conforming probe must not throw, but Core defends against one that does
            availability = extractor.ProbeAvailability();
        }
#pragma warning disable CA1031 // A probe is untrusted; any exception type must be contained as unavailability
        catch (Exception exception)
#pragma warning restore CA1031
        {
            // Treat a throwing probe as a contract violation, surfaced as unavailability with a reason
            return ExtractorAvailability.Unavailable($"availability probe failed: {exception.GetType().Name}");
        }

        // A null result is also a contract violation; treat it as an explained unavailability
        return availability ?? ExtractorAvailability.Unavailable("availability probe failed: returned null");
    }

    /// <summary>
    ///     Builds an immutable descriptor snapshot from a live extractor.
    /// </summary>
    /// <param name="extractor">The extractor to describe.</param>
    /// <returns>The descriptor capturing the extractor's identity and supported formats.</returns>
    /// <remarks>
    ///     Copies the supported-format collection into a fixed list so the descriptor is detached from
    ///     the extractor and cannot change if the extractor later mutates its own collection. Pure.
    /// </remarks>
    private static ExtractorDescriptor DescribeExtractor(IDocumentExtractor extractor) => new(
        extractor.Id,
        extractor.DisplayName,
        extractor.SupportedFormats.ToList(),
        extractor.Priority,
        extractor.PageRenderingApplicable);
}
