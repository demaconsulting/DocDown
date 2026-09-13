### ExtractorSelector Verification Design

This document describes the unit-level verification strategy for `ExtractorSelector`.

#### Verification Approach

`ExtractorSelector` is verified in `ExtractorSelectorTests.cs` using hand-built
`ExtractorCandidate` values. No mocking or filesystem access is required because selection is a pure
function of detected format, effective options, and candidate list.

#### Test Environment

- **Framework**: xUnit v3 under the .NET SDK
- **Targets**: net8.0, net9.0, and net10.0
- **Inputs**: hand-built `FormatDetection`, `ExtractionOptions`, and `ExtractorCandidate` values
- **Filesystem**: N/A - selector tests are fully in memory

#### Acceptance Criteria

Per IEC 62304 §5.5.2, `ExtractorSelector` passes when it selects only format-matched candidates,
returns honest package or unsupported-format prose when no candidate can run, prefers rendered-page
providers only when pages were requested, uses priority and identifier as deterministic tie-breaks,
and returns the same winner for equal logical inputs regardless of candidate order.

#### Test Scenarios

##### A format-matched candidate is selected

**Test**: `ExtractorSelector_Select_FormatMatchedCandidate_SelectsMatchingDescriptor`

##### Missing-backend prose names the package or states unsupported legacy formats

**Tests**: `ExtractorSelector_Select_DocxWithoutMatchingCandidate_ReturnsPackageHint`,
`ExtractorSelector_Select_LegacyFormatWithoutMatchingCandidate_ReturnsLegacyBinaryWording`,
`ExtractorSelector_Select_CustomFormatWithoutMatchingCandidate_ReturnsGenericWording`

##### All matching candidates unavailable returns failure

**Test**: `ExtractorSelector_Select_AllMatchingCandidatesUnavailable_ReturnsAvailabilityFailure`

##### Requested rendered pages prefer a renderer and otherwise fall back to priority

**Tests**: `ExtractorSelector_Select_RenderPagesRequested_PrefersCandidateProvidingRenderedPages`,
`ExtractorSelector_Select_RenderPagesRequestedWithoutRenderer_FallsBackToPriority`

##### Higher priority wins when otherwise equal

**Test**: `ExtractorSelector_Select_EqualCandidates_HigherPriorityWins`

##### Equal priority ties break by lower identifier

**Test**: `ExtractorSelector_Select_EqualPriorityCandidates_LowerIdentifierWins`

##### Shuffled candidates still produce the same winner

**Test**: `ExtractorSelector_Select_ShuffledCandidates_ProducesSameWinner`
