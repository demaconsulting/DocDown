## ExcelChartReader Verification Design

This document describes the unit-level verification strategy for `ExcelChartReader`, which recovers a
chart's cached data series from its chart part.

### Verification Approach

`ExcelChartReader` is verified through unit tests in `OpenXml/ExcelChartReaderTests.cs` in
`DemaConsulting.DocDown.Excel.Tests`, exercised against **real generated chart parts** so the name-driven
`XDocument` walk is proved against genuine chart XML rather than a simulation. Every series name, category,
and value in the fixtures is synthetic.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: generated worksheets carrying single-series, multi-series, scatter, automatic-title,
  deleted-title, sparse-index, literal-value, and cache-missing charts, plus a worksheet with no chart
- **Filesystem**: none; the reader reads a generated workbook from memory
- **Mocking**: none; the SDK and real chart XML are exercised
- **Isolation**: each test builds its own workbook

### Acceptance Criteria

Per IEC 62304 §5.5.2, an `ExcelChartReader` unit test run passes when a single-series chart yields its
title, axis titles, and cached values; when sparse indices are preserved; when every series of a
multi-series chart is read; when a chart caching no values keeps its source reference and yields no points;
and when an automatic title is reported as generated. A misread cache, an assumed-dense index, a dropped
series, or an authored-looking automatic title is a failure.

### Test Scenarios

#### A single-series chart yields titles, axes, and values

**Test**: `ExcelChartReader_ReadChartData_SingleSeries_ReadsTitlesAxesAndValues`

Proves a cached single-series chart yields its title, axis titles, and plotted values. Evidence for
`DocDownExcel-OpenXml-ExcelChartReader-ReadsCachedSeries`. The companion
`ExcelChartReader_ReadChartData_ScatterSeries_ReadsXAndYValues` and
`ExcelChartReader_ReadChartData_LiteralValues_ReadsPoints` cover the scatter and literal-value shapes.

#### Sparse indices are preserved

**Test**: `ExcelChartReader_ReadChartData_SparseIndices_PreservesDeclaredIndices`

Proves a cache that omits a point keeps its declared indices rather than assuming them dense. Evidence for
`DocDownExcel-OpenXml-ExcelChartReader-PreservesSparseIndices`.

#### Every series of a multi-series chart is read

**Test**: `ExcelChartReader_ReadChartData_MultipleSeries_ReadsEverySeries`

Proves every series a chart plots is read, not just the first. Evidence for
`DocDownExcel-OpenXml-ExcelChartReader-ReadsEverySeries`.

#### A missing cache keeps the reference and yields no points

**Test**: `ExcelChartReader_ReadChartData_MissingCache_KeepsReferenceAndYieldsNoPoints`

Proves a chart saved without a value cache keeps its source reference and yields no points, so the emitter
can report the missing cache honestly. Evidence for
`DocDownExcel-OpenXml-ExcelChartReader-KeepsReferenceWhenCacheMissing`.

#### An automatic title is reported as generated

**Test**: `ExcelChartReader_ReadChartData_AutomaticTitle_ReportsSeriesNameAsGenerated`

Proves a title the host application derives from the sole series name is flagged automatic, so the output
never implies an author wrote it. The companion
`ExcelChartReader_ReadChartData_DeletedAutoTitle_ReportsNoTitle` and
`ExcelChartReader_ReadChartData_NoChartElement_YieldsEmptyModel` cover the deleted-title and no-chart
shapes. Evidence for `DocDownExcel-OpenXml-ExcelChartReader-ReportsAutomaticTitle`.
