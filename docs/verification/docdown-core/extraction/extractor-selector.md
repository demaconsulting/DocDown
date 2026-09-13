### ExtractorSelector Verification Design

This document describes the unit-level verification strategy for `ExtractorSelector`, the pure ranking
function that chooses a backend for a detected format from a candidate list, honoring caller overrides,
availability, and capabilities, and producing a full decision trace.

#### Verification Approach

`ExtractorSelector` is verified in isolation through unit tests in `ExtractorSelectorTests.cs` under the
`Extraction` folder of `DemaConsulting.DocDown.Core.Tests`, with method names beginning with
`ExtractorSelector_`.

Nothing is mocked and no backend is involved. Selection is a pure function of its three arguments — the
detected format, the options, and the candidate list — so every scenario is expressed with hand-built
`ExtractorCandidate` value objects carrying a descriptor, availability, and effective capabilities. The
selector binds only to these immutable value types, so there is no filesystem, no availability probing,
and no `StubExtractor`: the candidates already encode the availability and capability state each scenario
needs. This is the ideal isolation level, because the ranking table, the override semantics, the failure
kinds, and the deterministic tie-break can all be driven directly by the candidate inputs.

#### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: hand-built immutable `ExtractorCandidate`, descriptor, availability, and option values
- **Mocking**: none; a pure function over value types
- **Isolation**: each test constructs its own candidates and selector; no shared state

#### Acceptance Criteria

Per IEC 62304 §5.5.2, an `ExtractorSelector` unit test run passes when only format-supporting candidates
are considered and an unsupported format fails with the correct code; when a caller override is honored
and never silently substituted; when unavailable candidates are excluded with a reason in the trace; when
caller-required capabilities are folded into the match and an unsatisfiable requirement fails hard; when
candidates are ranked full-satisfier first, then by satisfied count and priority, with ties broken by
identifier; when the trace carries a verdict for every candidate with the selected one first; and when
equal inputs — including a shuffled candidate order — produce identical results and trace order. Any
non-deterministic outcome or missing verdict is a failure.

#### Test Scenarios

##### Only format-supporting candidates are considered

**Tests**: `ExtractorSelector_Select_CandidateForOtherFormat_ClassifiedFormatNotSupported`,
`ExtractorSelector_Select_NoCandidateSupportsFormat_FailsNoExtractorForFormat`

Proves a candidate for another format is classified out and, when none supports the format, selection
fails with the no-extractor-for-format code. Evidence for
`DocDownCore-Extraction-ExtractorSelector-FormatFilter`.

##### The missing-backend remedy names the package that provides the extractor

**Tests**: `ExtractorSelector_Select_DocxWithNoMatchingExtractor_RemedyNamesWordPackage`,
`ExtractorSelector_Select_LegacyFormatWithNoBackend_RemedyStatesFormatUnsupported`,
`ExtractorSelector_Select_EveryLegacyFormat_RemedyStatesFormatUnsupported`,
`ExtractorSelector_Select_UnmappedFormatWithNoMatchingExtractor_UsesGenericRemedy`,
`ExtractorSelector_Select_WellKnownFormatWithNoBackend_RemedyNeverInstructsInstallation`

Proves that a well-known format with no registered backend produces a remedy naming both the format
and the DocDown package that provides its extractor, while a format absent from the table falls back
to the generic wording rather than inventing a package name. Proves that each of the four legacy
binary formats instead states plainly that DocDown does not support them — naming no package, no
environment precondition, and no install verb, because none of those would be true. Proves
further, across all six
well-known formats, that the remedy states where the capability lives instead of instructing an
installation — it carries no "install", "download", "nuget" or `PackageReference` wording, since
those packages are not published and such an instruction could not succeed — and carries no
expiring claim such as "not yet available", so the text stays true once the packages ship. Evidence
for `DocDownCore-Extraction-ExtractorSelector-PackageHint`.

##### A caller override is honored

**Test**: `ExtractorSelector_Select_OverrideNamesLowerPriority_SelectsNamedBackend`

Proves a caller-named backend is selected even over a higher-priority alternative. Evidence for
`DocDownCore-Extraction-ExtractorSelector-OverrideHonored`.

##### An override never falls back

**Tests**: `ExtractorSelector_Select_OverrideNamesUnavailable_FailsAndNeverFallsBack`,
`ExtractorSelector_Select_OverrideNamesFormatMismatch_FailsRequestedExtractorNotApplicable`

Proves that when a named override cannot run — because it is unavailable or does not support the format —
selection fails rather than substituting another backend. Evidence for
`DocDownCore-Extraction-ExtractorSelector-OverrideNeverFallsBack`.

##### Unavailable candidates are excluded with a reason

**Tests**: `ExtractorSelector_Select_UnavailableCandidate_ExcludedWithReasonInTrace`,
`ExtractorSelector_Select_AllCandidatesUnavailable_FailsNoAvailableExtractor`

Proves an unavailable candidate is excluded with its reason in the trace, and that all-unavailable
candidates fail with the no-available-extractor kind. Evidence for
`DocDownCore-Extraction-ExtractorSelector-AvailabilityFilter`.

##### Required capabilities are incorporated

**Test**: `ExtractorSelector_Select_RequireCapabilitiesSatisfied_AddsToRequiredAndSelects`

Proves caller-required capabilities are folded into the match so the chosen backend can deliver what was
demanded. Evidence for `DocDownCore-Extraction-ExtractorSelector-RequiredCapabilities`.

##### An unsatisfiable required capability fails hard

**Test**: `ExtractorSelector_Select_RequireCapabilitiesUnsatisfiable_FailsRequiredCapabilitiesUnavailable`

Proves selection fails when no candidate can satisfy a caller-required capability, rather than returning a
result that omits it. Evidence for `DocDownCore-Extraction-ExtractorSelector-HardCapabilityFailure`.

##### Candidates are ranked by fidelity

**Tests**: `ExtractorSelector_Select_FullSatisfierVersusHigherPriorityPartial_SelectsFullSatisfier`,
`ExtractorSelector_Select_AmongPartials_GreaterSatisfiedCountBeatsPriority`,
`ExtractorSelector_Select_EqualFidelityAndCount_HigherPriorityWins`

Proves the ranking prefers a full satisfier over a higher-priority partial one, then a greater satisfied
count over priority, then higher priority when fidelity and count are equal. Evidence for
`DocDownCore-Extraction-ExtractorSelector-RankOrdering`.

##### Ties break deterministically by identifier

**Test**: `ExtractorSelector_Select_EqualPriorityFullSatisfiers_LowerIdentifierWins`

Proves two equally ranked backends yield one stable choice, the lower identifier, so the winner is the
same on every run. Evidence for `DocDownCore-Extraction-ExtractorSelector-DeterministicTieBreak`.

##### The trace has a verdict per candidate, selected first

**Test**: `ExtractorSelector_Select_MultipleCandidates_TraceHasVerdictPerCandidateSelectedFirst`

Proves the trace carries a verdict for every candidate in a stable order with the selected one first,
making the decision transparent. Evidence for `DocDownCore-Extraction-ExtractorSelector-CandidateTrace`.

##### Equal inputs are repeatable

**Tests**: `ExtractorSelector_Select_EqualInputsRepeated_ProduceIdenticalResults`,
`ExtractorSelector_Select_ShuffledCandidateOrder_ProducesSameWinnerAndTraceOrder`

Proves the pure function produces identical results and trace order for equal inputs, independent of the
order candidates are supplied. Evidence for `DocDownCore-Extraction-ExtractorSelector-Repeatability`.
