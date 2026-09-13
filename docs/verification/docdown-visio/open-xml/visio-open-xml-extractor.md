## VisioOpenXmlExtractor Verification Design

This document describes the unit-level verification strategy for `VisioOpenXmlExtractor`, the managed
backend the engine selects.

### Verification Approach

`VisioOpenXmlExtractor` is verified through unit tests in `OpenXml/VisioOpenXmlExtractorTests.cs` in
`DemaConsulting.DocDown.Visio.Tests`, and end to end through the system-level integration tests. The unit
tests assert the descriptor, the unconditional availability, and the self-test contribution; the integration
tests prove the orchestration produces the full output contract.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: the descriptor and probe directly; a drawing built at test time by `VisioPackageBuilder` for
  the self-test round-trip; the system integration drawings for orchestration
- **Mocking**: none; the extractor runs against a real package and a recording context
- **Isolation**: each test owns its inputs

### Acceptance Criteria

Per IEC 62304 §5.5.2, a `VisioOpenXmlExtractor` unit test run passes when the descriptor declares the modern
Visio formats, the higher priority, and the text, embedded-image, document-metadata, and document-structure
capabilities and not rendered-pages; the availability probe reports available unconditionally without I/O;
the orchestration records the environment facts, reads the model, and delegates emission; and the self-test
cases include a topology round-trip that passes and a page-rendering case that reports skipped with a reason.

### Test Scenarios

#### The descriptor declares exactly the deliverable capabilities

**Test**: `VisioOpenXmlExtractor_Descriptor_MatchesContract`

Proves the identity, priority, supported formats, and declared capabilities, and — as an express counterpart
— that rendered-pages is not declared. Evidence for
`DocDownVisio-OpenXml-VisioOpenXmlExtractor-DeclaresCapabilities` and
`DocDownVisio-OpenXml-VisioOpenXmlExtractor-DeclaresPageRenderingNotProvided`.

#### The probe is unconditional

**Test**: `VisioOpenXmlExtractor_ProbeAvailability_AlwaysAvailable`

Proves the availability probe reports available everywhere the package loads, performing no I/O. Evidence for
`DocDownVisio-OpenXml-VisioOpenXmlExtractor-ProbesUnconditionally`.

#### The extraction is orchestrated end to end

**Test**: `DocDownVisio_Extract_Vsdx_SelectsOpenXml`

Proves the extractor records its facts, reads the model, delegates emission, and produces the full output
contract that reconciles cleanly. Evidence for
`DocDownVisio-OpenXml-VisioOpenXmlExtractor-OrchestratesExtraction`.

#### The self-test round-trip passes and rendering skips

**Test**: `VisioOpenXmlExtractor_SelfTestCases_RoundTripPassesAndRenderingSkipped`

Proves the topology round-trip case genuinely passes in the environment under test and the page-rendering
case reports a skip with a reason rather than a failure. Evidence for
`DocDownVisio-OpenXml-VisioOpenXmlExtractor-ContributesSelfTests`.
