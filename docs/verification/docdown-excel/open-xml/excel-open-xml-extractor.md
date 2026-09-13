## ExcelOpenXmlExtractor Verification Design

This document describes the unit-level verification strategy for `ExcelOpenXmlExtractor`, the managed
backend the engine selects and invokes for an `.xlsx`.

### Verification Approach

`ExcelOpenXmlExtractor` is verified through unit tests in `OpenXml/ExcelOpenXmlExtractorTests.cs` and the
system-level scenario in `DocDownExcelTests.cs`, in `DemaConsulting.DocDown.Excel.Tests`. The descriptor
and the probe are asserted directly against the constructed extractor; the self-test cases are enumerated
and run; and the orchestration is proved through a real end-to-end extraction that selects the backend and
produces the contract layout.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: the constructed extractor for the descriptor and probe; a generated workbook from
  `TestData/XlsxFixtures.cs` for the orchestration scenario
- **Filesystem**: a per-test `TempScratch` folder for the orchestration scenario; none for the descriptor
  and probe
- **Mocking**: none; the SDK and the real sink are exercised
- **Isolation**: each test constructs its own extractor and, where needed, its own scratch folder

### Acceptance Criteria

Per IEC 62304 §5.5.2, an `ExcelOpenXmlExtractor` unit test run passes when the descriptor's identity is
`excel-openxml`, its priority is 10, the `.xlsx` format is supported, the text, embedded-image,
document-metadata, and document-structure capabilities are declared, the rendered-pages capability is not,
and page rendering is reported not applicable; when the probe reports available unconditionally without
I/O; when the extractor selects and runs over an `.xlsx` and produces the reconciling contract layout; and
when the self-test set reports a passing round trip and a skipped page-rendering case. Any wrong descriptor
field, a throwing or I/O-performing probe, or a failed round trip is a failure.

### Test Scenarios

#### The descriptor matches the supported contract

**Test**: `ExcelOpenXmlExtractor_Descriptor_MatchesContract`

Proves the identity, priority, supported format, and declared capabilities match the deliverable set, and
— expressly — that the rendered-pages capability is not declared and page rendering is not applicable.
Evidence for `DocDownExcel-OpenXml-ExcelOpenXmlExtractor-DeclaresCapabilities` and
`DocDownExcel-OpenXml-ExcelOpenXmlExtractor-DeclaresPageRenderingNotApplicable`.

#### The availability probe is unconditional

**Test**: `ExcelOpenXmlExtractor_ProbeAvailability_AlwaysAvailable`

Proves the probe reports available with the full declared capability set, performing no I/O, because the
SDK is a managed assembly shipped inside the package. Evidence for
`DocDownExcel-OpenXml-ExcelOpenXmlExtractor-ProbesUnconditionally`.

#### The backend orchestrates a real extraction

**Test**: `DocDownExcel_Extract_Xlsx_SelectsOpenXml`

Proves the extractor is selected for an `.xlsx`, records its environment facts, reads the workbook, and
delegates emission, producing the reconciling contract layout. Evidence for
`DocDownExcel-OpenXml-ExcelOpenXmlExtractor-OrchestratesExtraction`.

#### The self-test cases run and report honestly

**Test**: `ExcelOpenXmlExtractor_SelfTestCases_RoundTripPassesAndRenderingSkipped`

Proves the round-trip case genuinely passes in the environment under test and the page-rendering case the
backend does not claim reports a skip with a reason rather than a failure. Evidence for
`DocDownExcel-OpenXml-ExcelOpenXmlExtractor-ContributesSelfTests`.
