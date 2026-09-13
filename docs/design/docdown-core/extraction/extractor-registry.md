### ExtractorRegistry

![Extraction Structure](ExtractionView.svg)

#### Purpose

`ExtractorRegistry` is the immutable snapshot of the registered extractors. It materializes the
registered factories once, exposes their descriptors, resolves an extractor by identifier, and probes
and caches each backend's availability. Its single responsibility is to be the authoritative, fixed
inventory of backends and their current usability for one engine.

#### Data Model

`ExtractorRegistry` is a `sealed class`. Its instance set and descriptors are fixed for its lifetime;
only the availability cache is mutable and it is guarded by a lock.

- **`_availabilityLock`** (`object`) — Serializes lazy availability probing and cache invalidation.
- **`_extractors`** (`IReadOnlyList<IDocumentExtractor>`) — The materialized instances in registration
  order.
- **`_descriptors`** (`IReadOnlyList<ExtractorDescriptor>`) — The descriptors, aligned index-for-index
  with `_extractors`.
- **`_candidates`** (`IReadOnlyList<ExtractorCandidate>?`) — The cached descriptor-plus-availability
  list, or null before the first probe.
- **`_availabilityDiagnostics`** (`IReadOnlyList<ExtractionDiagnostic>?`) — The cached diagnostics from
  the last probe pass.

*Invariant*: `_extractors` and `_descriptors` are the same length and index-aligned. *Invariant*:
identifiers are unique (enforced in the constructor). Descriptors and instances are immutable, so
reads are safe for concurrent access.

#### Key Methods

- **`internal ExtractorRegistry(IReadOnlyList<Func<IDocumentExtractor>> factories)`** — materializes
  every factory exactly once, building the descriptor list and enforcing that each extractor has a
  non-empty identifier and that no two share one. Internal because only `DocDownBuilder` constructs a
  registry. Does *not* probe availability — building an engine performs no environment inspection.
- **`IReadOnlyList<ExtractorDescriptor> Descriptors`** — the descriptors; probe-free.
- **`IReadOnlyList<IDocumentExtractor> Extractors`** — the instances, for self-test enumeration.
- **`IReadOnlyList<ExtractorCandidate> GetCandidates()`** — triggers a lazy probe on first access, then
  returns the cached candidates; safe to hand to `ExtractorSelector`, which must not re-probe.
- **`IReadOnlyList<ExtractionDiagnostic> AvailabilityDiagnostics`** — the probe-pass diagnostics: one
  `DD0601` (info) per candidate reported unavailable, one `DD0602` (warning) per candidate whose probe
  threw or returned null.
- **`IDocumentExtractor Resolve(string id)`** — a linear lookup by identifier; throws
  `KeyNotFoundException` for an unknown id (a path the engine never relies on, because it only resolves
  an identifier selection already confirmed).
- **`void RefreshAvailability()`** — clears the cache under the lock so the next query re-probes.

The private `EnsureProbed` double-checks the cache under the lock so exactly one probe pass runs even
when threads race to the first query. `ProbeSafely` contains each extractor's probe (see below), and
`DescribeExtractor` copies a backend's supported-format collection into a fixed list so the descriptor
is detached from the live instance.

#### Error Handling

Core does not trust the extractor contract. The constructor rejects a null factory, a factory that
returns null, an empty identifier, and a duplicate identifier — each an `ArgumentException` (or
`ArgumentNullException`) surfaced at build time, not mid-extraction. During probing, `ProbeSafely`
catches *any* exception thrown by `ProbeAvailability` (including `OperationCanceledException`, since a
probe has no token to justify one) and treats it as unavailability with reason
`availability probe failed: {ExceptionType}` and a `DD0602` warning; a null probe result is handled the
same way; an ordinary unavailable result records a `DD0601` info. Availability probing therefore cannot
fault the pass, and no untrusted exception escapes the registry. `Resolve` throws `KeyNotFoundException`
for an unknown identifier so a lookup bug fails loudly rather than silently.

#### Dependencies

- **IDocumentExtractor** (supporting contract) — the backends materialized and probed.
- **ExtractorDescriptor**, **ExtractorCandidate**, **ExtractorAvailability**, **ExtractionDiagnostic**,
  **DiagnosticCodes** (supporting types; see *Extraction Subsystem Design*).

#### Callers

`DocDownBuilder.Build()` constructs the registry. `DocDownEngine` calls `GetCandidates`,
`AvailabilityDiagnostics`, `Resolve`, `Extractors`, and `RefreshAvailability` throughout the pipeline
and for `GetBackendStatus`/`GetSelfTestCases`. See *DocDownBuilder Design* and *DocDownEngine Design*.
