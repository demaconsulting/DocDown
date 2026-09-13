## Extraction Subsystem

![Extraction Structure](ExtractionView.svg)

The Extraction subsystem owns the backend model and the pipeline: it registers extractors, probes
their availability, negotiates capabilities, selects the best available backend by a deterministic
ranking, and orchestrates one extraction from a source document to the invariant output layout.

### Overview

Extraction turns a detected format and a set of registered backends into a running extraction. It is
built around four software units:

- **DocDownBuilder** — the fluent configuration stage that collects extractor registrations and
  default options and produces an engine. See _DocDownBuilder Design_.
- **ExtractorRegistry** — the immutable snapshot of registered extractors, their descriptors, and
  their cached availability. See _ExtractorRegistry Design_.
- **ExtractorSelector** — a pure ranking function that chooses the best extractor for a format and
  records a complete, auditable decision. See _ExtractorSelector Design_.
- **DocDownEngine** — the public facade that runs the fixed pipeline end to end and returns a
  structured result rather than throwing. See _DocDownEngine Design_.

Two principles shape the subsystem. First, **backends are co-equal**: two extractors for the same
format are differentiated only by capabilities and an integer priority tie-break, never by a
hard-coded primary/fallback hierarchy. Second, **an override is a command, not a hint**: when a caller
names a preferred extractor, selection uses that backend or fails — it never silently falls back to a
different one.

### Interfaces

The subsystem exposes the public backend contract and the engine facade:

- **`IDocumentExtractor`** — implemented by external backend packages; declares identity, supported
  formats, capabilities, an availability probe, and an async `ExtractAsync`.
- **`DocDownBuilder`** — `AddExtractor`, `ConfigureDefaults`, `Build`.
- **`DocDownEngine`** — `ExtractAsync` (two overloads), `GetBackendStatus`, `RefreshAvailability`,
  `GetSelfTestCases`, and the `Extractors` descriptor list.
- **`ExtractorSelector.Select`** — the pure selection function, exposed for direct testing.

It consumes the Detection subsystem's `FormatDetection` and the Output subsystem's `ExtractionSink`,
`ContentWriter`, `ManifestWriter`, and `SummaryWriter`. Backends receive only a `DocumentSource` and an
`IExtractionContext`; they never see a scratch path.

### Design

`DocDownBuilder` accumulates registrations (instances are wrapped as factories so both registration
forms share one materialization path) and clones the default options at `Build`. `ExtractorRegistry`
materializes every factory once, eagerly, in its constructor — so duplicate-identifier detection and
descriptor construction happen at build time — and probes availability lazily, caching the result
until `RefreshAvailability`. `DocDownEngine` runs the fixed pipeline and delegates ranking to the pure
`ExtractorSelector`.

#### The ranked selection policy

`ExtractorSelector.Select` is a pure function of exactly three arguments — the `FormatDetection`, the
`ExtractionOptions`, and the candidate list — performing no I/O, reading no clock, and holding no
mutable state, so repeated calls with equal inputs always yield equal results. It applies seven steps:

1. **Format filter** — keep only candidates whose descriptor lists the detected format (matched by
   format `Id`, ordinal); every excluded candidate is recorded as `FormatNotSupported`. If none
   remain, selection fails with `NoExtractorForFormat` (`DD0402`).
2. **Caller override** — if `options.PreferredExtractorId` is set, reduce the field to that single
   named backend. Every other in-format candidate is recorded as `ExcludedByOverride`. If the named
   backend is absent for this format, fail with `RequestedExtractorNotApplicable` (`DD0405`); the
   override **never** falls back to another backend.
3. **Availability filter** — in automatic mode, drop unavailable candidates (each recorded as
   `Unavailable` with its reason); if none remain, fail with `NoAvailableExtractor` (`DD0403`). In
   override mode, a named-but-unavailable backend fails with `RequestedExtractorUnavailable`
   (`DD0406`) rather than degrading.
4. **Required-capability computation** — `Text` is always required; `| EmbeddedImages` when
   `IncludeEmbeddedImages`; `| RenderedPages` when `RenderPages`; `| options.RequireCapabilities`.
5. **Hard-requirement check** — when `RequireCapabilities` is set and no available candidate is a
   _full_ satisfier (its effective capabilities include every required bit), selection **fails** with
   `RequiredCapabilitiesUnavailable` (`DD0404`) rather than degrading. This is the difference between a
   hard requirement and a soft preference.
6. **Ranking** — order the remaining candidates by a total ordering (below) and take the head.
7. **Verdicts** — record the head as `Selected`, each other full satisfier as
   `OutrankedByHigherFidelity`, and each partial satisfier as `CapabilitiesInsufficient`, so every
   candidate — winner or loser — carries a reason.

The **total ordering** used in step 6 has four levels, applied in sequence:

1. Full satisfiers before partial satisfiers.
2. Descending population count of `EffectiveCapabilities & Required` (a backend that satisfies more of
   the request ranks higher).
3. Descending `Priority` (the integer tie-break).
4. Ascending extractor `Id` (ordinal), the final deterministic tie-break.

Because the final tie-break is the identifier, the ranking — and the trace, which lists the selected
candidate first and the rest by `Id` — is independent of registration order and stable across calls.
A successful selection reports both the `RequiredCapabilities` and the `SatisfiedCapabilities`, so a
_degraded_ selection (fewer satisfied than required) is distinguishable from a full one.

#### Supporting types

The following types are defined by the Extraction subsystem and documented here because they have no
dedicated unit design of their own. Except where noted they are immutable and thread-safe.

- **`IDocumentExtractor`** (`interface`) — The backend contract: `Id`, `DisplayName`,
  `SupportedFormats`, `Capabilities`, `Priority`, `ProbeAvailability()`, and
  `ExtractAsync(source, context)`.
- **`IExtractionContext`** (`interface`) — What a backend sees during extraction: the `Options`, the
  `Sink`, the `DetectedFormat`, the `SelectedExtractor`, the `Environment`, and a `CancellationToken`
  — but no output path.
- **`ExtractionContext`** (`internal sealed`) — The concrete `IExtractionContext` the engine
  constructs per run. Internal because only the engine creates one.
- **`ExtractorCapabilities`** (`[Flags] enum`) — The capabilities a backend can provide: `Text`,
  `EmbeddedImages`, `RenderedPages`, `DocumentMetadata`, `DocumentStructure`.
- **`ExtractorAvailability`** (`sealed record`) — Whether a backend is usable now, an optional reason,
  and its `EffectiveCapabilities` (which may be narrower than declared). Factory helpers `Available`
  and `Unavailable`.
- **`ExtractorDescriptor`** (`sealed record`) — An immutable snapshot of a backend's identity and
  declared abilities, detached from the live instance.
- **`ExtractorCandidate`** (`sealed record`) — A `descriptor` paired with its probed `availability`;
  the unit of input to selection.
- **`BackendStatus`** (`sealed record`) — A flattened backend status (declared and effective
  capabilities, availability, reason) for reporting via `GetBackendStatus`.
- **`SelectionMode`** (`enum`) — `Automatic` or `CallerOverride`.
- **`CandidateOutcome`** (`enum`) — The per-candidate classification: `Selected`,
  `FormatNotSupported`, `Unavailable`, `CapabilitiesInsufficient`, `OutrankedByHigherFidelity`,
  `ExcludedByOverride`.
- **`CandidateVerdict`** (`sealed record`) — One candidate's identifier, display name, priority,
  outcome, and a human-readable detail.
- **`SelectionResult`** (`sealed record`) — The selection outcome: the `Selected` descriptor (or
  null), the mode, required and satisfied capabilities, the full `Trace`, and any `Failure`.
- **`ExtractionOutcome`** (`enum`) — `Succeeded`, `Degraded`, or `Failed`.
- **`ExtractionFailureKind`** (`enum`) — The nine failure kinds (format not recognized, no extractor,
  none available, capabilities unavailable, override not applicable/unavailable, source unreadable,
  scratch refused, extractor failed).
- **`ExtractionFailure`** (`sealed record`) — A structured failure: kind, code, summary, a multi-line
  displayable `Explanation`, the candidate list, and an optional remedy.
- **`ExtractionDiagnostic`** (`sealed record`) — A coded, severity-tagged, optionally-located
  diagnostic message.
- **`DiagnosticSeverity`** (`enum`) — `Info`, `Warning`, `Error`.
- **`DiagnosticCodes`** (`internal static class`) — The Core diagnostic-code constants
  (`DD01xx`–`DD07xx`). Internal because it is an implementation vocabulary.
- **`ExtractionOptions`** (`sealed class`, mutable) — The request configuration: page rendering and
  range, image inclusion and limits, DPI, scratch-folder mode, content split, preferred extractor,
  required capabilities, and `TimestampUtc`. Has public setters and a `Clone()`; see the thread-safety
  note below.
- **`ScratchFolderMode`** (`enum`) — `RequireEmpty`, `CleanIfDocDownFolder`, `Overwrite`,
  `CreateUnique`.
- **`ContentSplitMode`** (`enum`) — `Auto`, `Single`, `PerPart`.
- **`ImageOutputMode`** (`enum`) — `Preserve`, `ForcePng`.
- **`PageRange`** (`readonly record struct`) — An inclusive `First`–`Last` page range with
  `Contains(page)` and `Count`.
- **`DocumentSource`** (`sealed class`) — A file- or stream-backed document input;
  `FromFile`/`FromStream` factories, `FileName`, optional `Path` and `SizeBytes`, and `OpenRead()`.
- **`DocumentInfo`** (`sealed record`) — Extractor-reported metadata: optional title, author, page
  count, part count.
- **`ExtractionResult`** (`sealed class`) — The full outcome returned to the caller: outcome, all
  output paths, detected format, selected extractor and trace, failure, diagnostics, `IsComplete`,
  environment, gaps, and the artifact ledger.
- **`ISelfValidating`** (`interface`) — Opt-in: a backend implementing it contributes `SelfTestCase`s.
- **`SelfTestCase`** (`sealed record`) — A named, categorized, runnable self-test case.
- **`SelfTestContext`** (`sealed class`) — The working folder and cancellation token handed to a
  self-test.
- **`SelfTestResult`** (`sealed record`) — The status, optional message, and duration of a self-test;
  `Passed`/`Failed`/`Skipped` factories.
- **`SelfTestStatus`** (`enum`) — `Passed`, `Failed`, `Skipped`.

##### Options thread safety (Correction C5)

`ExtractionOptions` is the one mutable configuration type in the subsystem. Because a caller may hold
and later mutate the same instance, `DocDownBuilder.Build()` clones the configured defaults into the
engine, and `DocDownEngine.ExtractAsync` clones the effective options **again** on entry
(`options?.Clone() ?? _defaults.Clone()`) before any other work. A caller therefore cannot affect an
in-flight or subsequent extraction by mutating its options object after the call. Each extraction runs
against a private snapshot. See _DocDownEngine Design_ and the system-level _Design Constraints_.
