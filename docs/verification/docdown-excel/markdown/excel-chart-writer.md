## ExcelChartWriter Verification Design

This document describes the unit-level verification strategy for `ExcelChartWriter`, which renders a
chart's cached data as markdown.

### Verification Approach

`ExcelChartWriter` is verified through unit tests in `Markdown/ExcelChartWriterTests.cs` in
`DemaConsulting.DocDown.Excel.Tests`. The writer is a pure function, so it is exercised from **hand-built
`ExcelChartModel` instances** and its markdown and returned accounting are asserted directly. Every series
name, category, and value in the models is synthetic.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: hand-built chart models with cached, sparse, multi-series, unnamed-series, over-bound,
  at-bound, uncached, and unreadable shapes
- **Filesystem**: none; the writer is pure
- **Mocking**: none
- **Isolation**: each test constructs its own chart model

### Acceptance Criteria

Per IEC 62304 §5.5.2, an `ExcelChartWriter` unit test run passes when a single-series chart renders its
labeling and a table of categories against values; when sparse indices render with empty cells rather than
shifted values; when a multi-series chart renders one column per series; when an unnamed series is labeled
by position; when a chart beyond the bound truncates and states what it dropped and one exactly at the
bound does not; when a chart caching no points states its absence and reports no data; when an unreadable
chart states its reason; when a pipe in a label is escaped; and when the description names a readable chart
and its part. A shifted value, a silent truncation, or a corrupted column is a failure.

### Test Scenarios

#### A single-series chart renders labeling and a table

**Test**: `ExcelChartWriter_Render_SingleSeries_RendersLabelingAndTable`

Proves a cached single-series chart renders its context and a table of categories against values. Evidence
for `DocDownExcel-Markdown-ExcelChartWriter-RendersCachedSeries`. The companion
`ExcelChartWriter_Render_MultipleSeries_RendersOneColumnPerSeries`,
`ExcelChartWriter_Render_SparseIndices_RendersDeclaredIndicesWithEmptyCells`,
`ExcelChartWriter_Render_UnnamedSeries_LabelsByPosition`, and
`ExcelChartWriter_Render_PipeInLabel_EscapesIt` prove the multi-series, sparse, unnamed, and escaping cases.

#### An unreadable or uncached chart is stated in the part

**Test**: `ExcelChartWriter_Render_UnreadableChart_StatesTheReason`

Proves a chart whose part could not be read states its reason in the part;
`ExcelChartWriter_Render_NoCachedPoints_StatesAbsenceAndReportsNoData` proves a chart caching no points
states its absence and reports no data. Evidence for
`DocDownExcel-Markdown-ExcelChartWriter-StatesUnreadableAndUncached`.

#### The plotted-point bound truncates and is stated

**Test**: `ExcelChartWriter_Render_BeyondBound_TruncatesAndStatesIt`

Proves a chart caching more than the bound truncates and states how many points it dropped, while
`ExcelChartWriter_Render_ExactlyAtBound_DoesNotTruncate` proves a chart exactly at the bound does not.
Evidence for `DocDownExcel-Markdown-ExcelChartWriter-BoundsPlottedPoints`.

#### The description names a chart and its part

**Test**: `ExcelChartWriter_Describe_ReadableChart_NamesChartAndItsPart`

Proves the one-line description names a readable chart and points at its own part. Evidence for
`DocDownExcel-Markdown-ExcelChartWriter-DescribesChartUnderSheet`.
