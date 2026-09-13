## OpenXml Subsystem Verification Design

This document describes the verification strategy for the OpenXml subsystem, the managed Excel extraction
backend: the extractor the engine selects, the reader that turns the package into the model, and the
image, chart, and drawing-text readers that resolve the drawing layer.

### Verification Approach

The OpenXml subsystem is verified through unit tests in `OpenXml/ExcelOpenXmlExtractorTests.cs`,
`OpenXml/ExcelOpenXmlReaderTests.cs`, and `OpenXml/ExcelChartReaderTests.cs`, plus the system-level
scenarios in `DocDownExcelTests.cs`, all in `DemaConsulting.DocDown.Excel.Tests`.

The reader units are exercised against **real generated workbooks** from `TestData/XlsxFixtures.cs`, not a
simulation, because their contract is the faithful translation of a genuine `.xlsx` into the model: the
verbatim cell values, the formulas, the sheet identity, the embedded images and their sheet association,
and the charts' cached series. The extractor's descriptor — its identity, priority, supported format,
declared capabilities, page-rendering non-applicability, and unconditional probe — is asserted directly,
and its self-test cases are run to prove the round-trip case passes and the page-rendering case skips. The
image reader and the drawing-text reader carry no dedicated test class; their behavior is proved through
the reader that drives them and the emitter scenarios that render their output, because their observable
contract is what reaches the model and the sink.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: SpreadsheetML workbooks generated at test time by `TestData/XlsxFixtures.cs`; every value,
  sheet name, chart series, and image in them is synthetic
- **Filesystem**: a per-test `TempScratch` folder for the system-level scenarios; none for the reader
  scenarios, which read a generated workbook from memory
- **Mocking**: none; the SDK is exercised against real workbooks and the sink is Core's real writer through
  the engine
- **Isolation**: each test builds its own workbook and, where needed, its own scratch folder

### Acceptance Criteria

Per IEC 62304 §5.6.2, an OpenXml subsystem test run passes when the extractor's descriptor matches the
deliverable contract with the rendered-pages capability absent and page rendering not applicable, the
probe is unconditional, and the self-tests report a passing round trip and a skipped page-rendering case;
when the reader preserves every cell's value verbatim at full length and precision, keeps a formula
alongside its value and address, returns the worksheets in order with their names, and drops empty cells;
when the image reader yields an embedded image and records its sheet association; and when the chart reader
recovers a chart's titles, axes, and cached series, preserves sparse indices, reads every series, keeps a
series reference when the cache is missing, and reports an automatic title as generated. Any wrong
descriptor field, truncated value, dropped formula, lost sheet association, or misread chart cache is a
failure.

### Test Scenarios

The per-unit scenarios are given in the `ExcelOpenXmlExtractor`, `ExcelOpenXmlReader`,
`ExcelOpenXmlImageReader`, `ExcelChartReader`, and `ExcelDrawingTextReader` unit verification chapters,
each naming the requirement it evidences.
