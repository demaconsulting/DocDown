## VisioOpenXmlExtractor Verification Design

This document describes the unit-level verification strategy for `VisioOpenXmlExtractor`, the
managed backend the engine selects.

### Verification Approach

`VisioOpenXmlExtractor` is verified through unit tests in `OpenXml/VisioOpenXmlExtractorTests.cs`
and end to end through the system-level integration tests. The unit tests assert the extractor's
selection identity, always-available probe result, and self-test contribution; the integration tests
prove the orchestration produces the full output contract.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: the descriptor and probe directly; a drawing built at test time by
  `VisioPackageBuilder` (test project) for the self-test round-trip; the system integration drawings for
  orchestration
- **Mocking**: none; the extractor runs against a real package and a recording context
- **Isolation**: each test owns its inputs

### Acceptance Criteria

Per IEC 62304 §5.5.2, a `VisioOpenXmlExtractor` unit test run passes when the extractor identifies
`.vsdx` and `.vsdm`, carries the managed backend's higher priority, leaves page rendering
applicable for Visio drawings, reports `Available()` without rendered-page support, records the
managed-path environment facts, reads the model, delegates emission, and contributes self-test cases
covering a topology round-trip and a rendering skip.

### Test Scenarios

#### The extractor participates in selection as the managed backend

**Test**: `VisioOpenXmlExtractor_Descriptor_MatchesContract`

Proves the identity, priority, supported formats, and the fact that page rendering remains a
meaningful request for Visio drawings. Evidence for
`DocDownVisio-OpenXml-VisioOpenXmlExtractor-ParticipatesInSelection`.

#### The probe is unconditional

**Test**: `VisioOpenXmlExtractor_ProbeAvailability_AlwaysAvailable`

Proves the availability probe reports available everywhere the package loads, without rendered-page
support and without I/O. Evidence for
`DocDownVisio-OpenXml-VisioOpenXmlExtractor-ProbesUnconditionally`.

#### The extraction is orchestrated end to end

**Test**: `DocDownVisio_Extract_Vsdx_SelectsOpenXml`

Proves the extractor records its facts, reads the model, delegates emission, and produces the full
output contract that reconciles cleanly. Evidence for
`DocDownVisio-OpenXml-VisioOpenXmlExtractor-OrchestratesExtraction`.

#### The self-test round-trip passes and rendering skips

**Test**: `VisioOpenXmlExtractor_SelfTestCases_RoundTripPassesAndRenderingSkipped`

Proves the topology round-trip case genuinely passes in the environment under test and the
page-rendering case reports a skip with a reason rather than a failure. Evidence for
`DocDownVisio-OpenXml-VisioOpenXmlExtractor-ContributesSelfTests`.
