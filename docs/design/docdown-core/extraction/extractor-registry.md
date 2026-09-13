### ExtractorRegistry

![Extraction Structure](ExtractionView.svg)

#### Purpose

`ExtractorRegistry` is the immutable snapshot of the registered extractors. It materializes the
registered factories once, exposes descriptors, resolves an extractor by identifier, and caches each
backend's current availability.

#### Data Model

`ExtractorRegistry` is a `sealed class`. The extractor set and descriptor set are fixed for the
registry lifetime; only the cached candidate list is mutable, and that cache is protected by a lock.

- **`_availabilityLock`** (`object`) — serializes lazy probing and cache invalidation.
- **`_extractors`** (`IReadOnlyList<IDocumentExtractor>`) — materialized backend instances in
  registration order.
- **`_descriptors`** (`IReadOnlyList<ExtractorDescriptor>`) — immutable snapshots aligned with the
  backend instances.
- **`_candidates`** (`IReadOnlyList<ExtractorCandidate>?`) — the cached descriptor-plus-availability
  list.

#### Key Methods

- **`ExtractorRegistry(IReadOnlyList<Func<IDocumentExtractor>> factories)`** — materializes each
  factory exactly once, rejects null results and duplicate identifiers, and builds the descriptors.
- **`Descriptors`** — the registered descriptors in registration order.
- **`Extractors`** — the materialized extractor instances, used for self-test enumeration.
- **`GetCandidates()`** — lazily probes availability on first use, caches the result, and returns
  one candidate per registered extractor.
- **`Resolve(string id)`** — resolves a registered backend by identifier.
- **`RefreshAvailability()`** — clears the cached candidates so the next query re-probes.

`ProbeSafely` contains every outcome of `IDocumentExtractor.ProbeAvailability()`: a null result or
any thrown exception is converted into `ExtractorAvailability.Unavailable(...)` with a stable reason.

#### Error Handling

The constructor rejects null factories, null factory results, null or empty identifiers, and
identifier collisions. `Resolve` throws `KeyNotFoundException` for an unknown identifier. Availability
probing never faults the pass: an extractor whose probe throws or returns null is treated as
unavailable and carried forward as such.

#### Dependencies

- **`IDocumentExtractor`** — the backend contract the registry materializes.
- **`ExtractorDescriptor`**, **`ExtractorCandidate`**, and **`ExtractorAvailability`** — supporting
  value types from the Extraction subsystem.

#### Callers

`DocDownBuilder.Build()` constructs the registry. `DocDownEngine` calls `GetCandidates`, `Resolve`,
`RefreshAvailability`, `Descriptors`, and `Extractors` during extraction and self-test enumeration.
