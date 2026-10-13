## ExcelChartReader Verification Design

This document describes the unit-level verification strategy for `ExcelChartReader`, which recovers a
chart's cached data series from its chart part.

### Verification Approach

`ExcelChartReader` is verified through unit tests in `OpenXml/ExcelChartReaderTests.cs` in
`DemaConsulting.DocDown.Office.Tests`. The parsing rules of `ReadChartData` are exercised against
chart XML written by hand in the test, with no package around it, and `Collect` is exercised against
workbooks that `TestData/XlsxFixtures.cs` builds with the Open XML SDK, whose chart parts hold the same
kind of hand-written XML; no chart part comes from a spreadsheet application. The name-driven `XDocument`
walk is therefore proved against XML shaped like the chart grammar, not against files a spreadsheet
application saved. Every series name, category, and value in the fixtures is synthetic.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: hand-written chart XML strings for single-series, multi-series, scatter, automatic-title,
  deleted-title, sparse-index, literal-value, and cache-missing charts; generated workbooks for a worksheet
  with one chart, with none, and with several chart parts laid out under chosen graphic frames, including a
  malformed part, an extended chart part, and a frame citing an undeclared relationship
- **Filesystem**: none; the reader reads chart XML and generated workbooks from memory
- **Mocking**: none; the SDK and hand-written chart XML are exercised
- **Isolation**: each test builds its own workbook

### Acceptance Criteria

Per IEC 62304 §5.5.2, an `ExcelChartReader` unit test run passes when a single-series chart yields its
title, axis titles, and cached values; when sparse indices are preserved; when every series of a
multi-series chart is read; when a chart caching no values keeps its source reference and yields no points;
and when an automatic title is reported as generated; when a malformed chart part and an extended chart part
are each kept with no data and a stated reason; and when charts are reported in frame order, once each, with
an unreferenced chart part after them and an undeclared relationship ignored. A misread cache, an assumed-dense
index, a dropped series, an authored-looking automatic title, a dropped or thrown unreadable chart, or an
out-of-order chart is a failure.

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

#### A worksheet's charts are collected with their cached data

**Test**: `ExcelChartReader_Collect_ChartWorkbook_ReturnsChartWithCachedData`

Proves a worksheet anchoring a chart yields it with its sheet name and cached title, and that a missing
part or a worksheet with no drawing yields none. Additional evidence for
`DocDownExcel-OpenXml-ExcelChartReader-ReadsCachedSeries`.

#### A malformed chart part is kept with a stated reason

**Test**: `ExcelChartReader_Collect_MalformedChartPart_RecordsReasonWithoutThrowing`

Proves a chart part whose XML is not well-formed yields a chart with no data and a reason that begins
"The chart part is not well-formed XML:", without throwing, while the sheet's other chart is still read.
The reason for a failure to read the part from the package is not covered by a test. Evidence for
`DocDownExcel-OpenXml-ExcelChartReader-ReportsUnreadableChart`.

#### An extended chart is kept with the extended-chart reason

**Test**: `ExcelChartReader_Collect_ExtendedChartPart_RecordsExtendedChartReason`

Proves a frame citing an extended chart part yields a chart with no data and the named
`ExcelChartReader.ExtendedChartReason`. Evidence for
`DocDownExcel-OpenXml-ExcelChartReader-ReportsExtendedChart`.

#### Charts follow frame order, once each, with orphans last

**Tests**: `ExcelChartReader_Collect_FramesInReverseOrder_ReturnsChartsInFrameOrder`,
`ExcelChartReader_Collect_TwoFramesCitingOnePart_ReturnsChartOnce`,
`ExcelChartReader_Collect_UnframedChartPart_IsReportedAfterFramedCharts`,
`ExcelChartReader_Collect_FrameCitingUndeclaredRelationship_IsIgnored`

Prove two parts framed in reverse relationship order come out in frame order, two frames citing one part
yield one chart, a part no frame cites is reported after the framed chart, and a frame naming an undeclared
relationship is ignored while the real part is still reported. Evidence for
`DocDownExcel-OpenXml-ExcelChartReader-OrdersByFrameAndSweepsOrphans`.

#### Literal values are read

**Test**: `ExcelChartReader_ReadChartData_LiteralValues_ReadsPoints`

Proves a series whose values are literals rather than a cached range yields its points with no source
reference. Evidence for `DocDownExcel-OpenXml-ExcelChartReader-ReadsLiteralValues`.
