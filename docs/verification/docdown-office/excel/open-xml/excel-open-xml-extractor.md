## ExcelOpenXmlExtractor Verification Design

This document describes the unit-level verification strategy for `ExcelOpenXmlExtractor`, the managed
backend the engine selects and invokes for an `.xlsx`.

### Verification Approach

`ExcelOpenXmlExtractor` is verified through unit tests in `OpenXml/ExcelOpenXmlExtractorTests.cs` and
the system-level scenario in `DocDownExcelTests.cs`, in `DemaConsulting.DocDown.Office.Tests`. The
extractor surface and the probe are asserted directly against the constructed extractor; the self-test
cases are enumerated and run; and the orchestration is proved through a real end-to-end extraction that
selects the backend and produces the contract layout.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: the constructed extractor for the supported surface and probe; a generated workbook from
  `TestData/XlsxFixtures.cs` for the orchestration scenario
- **Filesystem**: a per-test `TempScratch` folder for the orchestration scenario; none for the
  supported-surface and probe scenarios
- **Mocking**: none; the SDK and the real sink are exercised
- **Isolation**: each test constructs its own extractor and, where needed, its own scratch folder

### Acceptance Criteria

Per IEC 62304 §5.5.2, an `ExcelOpenXmlExtractor` unit test run passes when the extractor's identity is
`excel-openxml`, its display name is `Excel (Open XML SDK)`, its priority is 10, the `.xlsx` format is
supported, and `PageRenderingApplicable` is `false`; when the probe reports available unconditionally,
returns no unavailable reason, and reports no rendered pages; when the extractor selects and runs over
an `.xlsx` and produces the reconciling contract layout; and when the self-test set reports a passing
round trip and a skipped page-rendering case. Any wrong surface field, a throwing or I/O-performing
probe, or a failed round trip is a failure.

### Test Scenarios

#### The supported surface matches the contract

**Test**: `ExcelOpenXmlExtractor_Descriptor_MatchesContract`

Proves the identity, display name, supported format, priority, and page-rendering flag match the
supported surface. Evidence for `DocDownExcel-OpenXml-ExcelOpenXmlExtractor-ReportsSupportedSurface` and
`DocDownExcel-OpenXml-ExcelOpenXmlExtractor-ReportsPageRenderingNotApplicable`.

#### The availability probe is unconditional

**Test**: `ExcelOpenXmlExtractor_ProbeAvailability_AlwaysAvailable`

Proves the probe reports available, gives no unavailable reason, and claims no rendered pages,
performing no I/O, because the SDK is a managed assembly shipped inside the package. Evidence for
`DocDownExcel-OpenXml-ExcelOpenXmlExtractor-ProbesUnconditionally`.

#### The backend orchestrates a real extraction

**Test**: `DocDownExcel_Extract_Xlsx_SelectsOpenXml`

Proves the extractor is selected for an `.xlsx`, records its environment facts, reads the workbook, and
delegates emission, producing the reconciling contract layout. Evidence for
`DocDownExcel-OpenXml-ExcelOpenXmlExtractor-OrchestratesExtraction`.

#### The self-test cases run and report honestly

**Test**: `ExcelOpenXmlExtractor_SelfTestCases_RoundTripPassesAndRenderingSkipped`

Proves the round-trip case genuinely passes in the environment under test and the page-rendering case
reports a skip with a reason rather than a failure. Evidence for
`DocDownExcel-OpenXml-ExcelOpenXmlExtractor-ContributesSelfTests`.
