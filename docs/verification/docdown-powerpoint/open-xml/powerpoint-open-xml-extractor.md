### PowerPointOpenXmlExtractor Verification Design

This document describes the unit-level verification strategy for `PowerPointOpenXmlExtractor`, the
managed backend the engine selects and invokes for a `.pptx`.

### Verification Approach

`PowerPointOpenXmlExtractor` is verified through unit tests in
`OpenXml/PowerPointOpenXmlExtractorTests.cs` and the system-level scenario in
`DocDownPowerPointTests.cs`, in `DemaConsulting.DocDown.PowerPoint.Tests`. The selection surface and
the probe are asserted directly against the constructed extractor; the self-test cases are enumerated
and run; and the orchestration is proved through a real end-to-end extraction that selects the backend
and produces the contract layout.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: the constructed extractor for the descriptor and probe; a generated deck from
  `TestData/PptxFixtures.cs` for the orchestration scenario
- **Filesystem**: a per-test `TempScratch` folder for the orchestration scenario; none for the
  descriptor and probe
- **Mocking**: none; the SDK and the real sink are exercised
- **Isolation**: each test constructs its own extractor and, where needed, its own scratch folder

### Acceptance Criteria

Per IEC 62304 §5.5.2, a `PowerPointOpenXmlExtractor` unit test run passes when the extractor exposes
the expected backend identity, priority, supported format, and page-rendering applicability; when the
probe reports available unconditionally and leaves rendered-page support false; when the extractor
selects and runs over a `.pptx`, records its environment facts, and produces the reconciling contract
layout; and when the self-test set reports a passing round trip and a skipped page-rendering case. Any
wrong descriptor field, a throwing or I/O-performing probe, or a failed round trip is a failure.

### Test Scenarios

#### The selection surface matches the supported contract

**Test**: `PowerPointOpenXmlExtractor_Descriptor_MatchesContract`

Proves the identity, priority, supported format, and page-rendering applicability match the supported
contract. Evidence for
`DocDownPowerPoint-OpenXml-PowerPointOpenXmlExtractor-DescribesSelectionSurface`.

#### The availability probe is unconditional

**Test**: `PowerPointOpenXmlExtractor_ProbeAvailability_AlwaysAvailable`

Proves the probe reports available with rendered-page support left false, performing no I/O. Evidence
for `DocDownPowerPoint-OpenXml-PowerPointOpenXmlExtractor-ReportsPageRenderingNotProvided` and
`DocDownPowerPoint-OpenXml-PowerPointOpenXmlExtractor-ProbesUnconditionally`.

#### The backend orchestrates a real extraction

**Test**: `DocDownPowerPoint_Extract_Pptx_SelectsOpenXml`

Proves the extractor is selected for a `.pptx`, records its environment facts, reads the deck, and
delegates emission, producing the reconciling contract layout. Evidence for
`DocDownPowerPoint-OpenXml-PowerPointOpenXmlExtractor-OrchestratesExtraction`.

#### The self-test cases run and report honestly

**Test**: `PowerPointOpenXmlExtractor_SelfTestCases_RoundTripPassesAndRenderingSkipped`

Proves the round-trip case genuinely passes in the environment under test and the page-rendering case
reports a skip with a reason rather than a failure. Evidence for
`DocDownPowerPoint-OpenXml-PowerPointOpenXmlExtractor-ContributesSelfTests`.
