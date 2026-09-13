## PowerPointOpenXmlExtractor Verification Design

This document describes the unit-level verification strategy for `PowerPointOpenXmlExtractor`, the managed
backend the engine selects and invokes for a `.pptx`.

### Verification Approach

`PowerPointOpenXmlExtractor` is verified through unit tests in `OpenXml/PowerPointOpenXmlExtractorTests.cs`
and the system-level scenario in `DocDownPowerPointTests.cs`, in `DemaConsulting.DocDown.PowerPoint.Tests`.
The descriptor and the probe are asserted directly against the constructed extractor; the self-test cases
are enumerated and run; and the orchestration is proved through a real end-to-end extraction that selects
the backend and produces the contract layout.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: the constructed extractor for the descriptor and probe; a generated deck from
  `TestData/PptxFixtures.cs` for the orchestration scenario
- **Filesystem**: a per-test `TempScratch` folder for the orchestration scenario; none for the descriptor
  and probe
- **Mocking**: none; the SDK and the real sink are exercised
- **Isolation**: each test constructs its own extractor and, where needed, its own scratch folder

### Acceptance Criteria

Per IEC 62304 §5.5.2, a `PowerPointOpenXmlExtractor` unit test run passes when the descriptor's identity is
`powerpoint-openxml`, its priority is 10, the `.pptx` format is supported, the text, embedded-image,
document-metadata, and document-structure capabilities are declared, and the rendered-pages capability is
not; when the probe reports available unconditionally without I/O; when the extractor selects and runs over
a `.pptx` and produces the reconciling contract layout; and when the self-test set reports a passing round
trip and a skipped page-rendering case. Any wrong descriptor field, a throwing or I/O-performing probe, or a
failed round trip is a failure.

### Test Scenarios

#### The descriptor matches the supported contract

**Test**: `PowerPointOpenXmlExtractor_Descriptor_MatchesContract`

Proves the identity, priority, supported format, and declared capabilities match the deliverable set, and —
expressly — that the rendered-pages capability is not declared. Evidence for
`DocDownPowerPoint-OpenXml-PowerPointOpenXmlExtractor-DeclaresCapabilities` and
`DocDownPowerPoint-OpenXml-PowerPointOpenXmlExtractor-DeclaresPageRenderingNotProvided`.

#### The availability probe is unconditional

**Test**: `PowerPointOpenXmlExtractor_ProbeAvailability_AlwaysAvailable`

Proves the probe reports available with the full declared capability set, performing no I/O, because the SDK
is a managed assembly shipped inside the package. Evidence for
`DocDownPowerPoint-OpenXml-PowerPointOpenXmlExtractor-ProbesUnconditionally`.

#### The backend orchestrates a real extraction

**Test**: `DocDownPowerPoint_Extract_Pptx_SelectsOpenXml`

Proves the extractor is selected for a `.pptx`, records its environment facts, reads the deck, and delegates
emission, producing the reconciling contract layout. Evidence for
`DocDownPowerPoint-OpenXml-PowerPointOpenXmlExtractor-OrchestratesExtraction`.

#### The self-test cases run and report honestly

**Test**: `PowerPointOpenXmlExtractor_SelfTestCases_RoundTripPassesAndRenderingSkipped`

Proves the round-trip case genuinely passes in the environment under test and the page-rendering case the
managed backend does not claim reports a skip with a reason rather than a failure. Evidence for
`DocDownPowerPoint-OpenXml-PowerPointOpenXmlExtractor-ContributesSelfTests`.
