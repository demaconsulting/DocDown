## ExcelChartReader

![DocDown.Excel Structure](DocDownExcelView.svg)

### Purpose

`ExcelChartReader` reads the charts a worksheet shows into the model, recovering each chart's cached
data series — the numbers as last plotted — rather than the formulas that produced them. Its single
responsibility is to make a chart usable as data: a hundred cached points describe a curve that no
rendered picture of the same chart would yield.

### Data Model

`ExcelChartReader` is an `internal static class`. It returns a list of `ExcelChartModel`, each carrying
a chart's part URI, its sheet context, and either its `ExcelChartData` cache (title and whether it is
automatic, plot types, axis titles, category labels, and the series with their cached points) or the
reason the part could not be read — exactly one of the two. Chart XML is read with `XDocument` rather
than the typed chart classes, because every plot type spells its series the same way (`c:ser` with
`c:cat` or `c:val` caches), so a name-driven walk covers plot types this product has never seen.

### Key Methods

- **`IReadOnlyList<ExcelChartModel> Collect(WorksheetPart? worksheetPart, string sheetName)`** — walks
  the worksheet's drawing for graphic frames so charts come out in the laid-out order, reads each
  referenced chart part, and then collects any chart part no frame references so an unreferenced chart
  is reported rather than lost. A sheet with no drawing part shows no chart.
- **`BuildChart`** (private) — parses one chart part into its cache, or returns the failure reason; a
  chart drawn with the newer extended chart grammar is returned with a named reason rather than thrown.
- The series walk recovers each series' name reference, source reference, number format, declared point
  count, and cached points by declared index — preserving a sparse cache honestly, because a cache omits
  a point whose source cell was empty — and reads category and value caches the same way. An automatic
  title derived from the sole series name is flagged as automatic.

### Error Handling

A chart part that cannot be parsed is returned as an `ExcelChartModel` whose data is null and whose
failure reason states why, so the caller can keep the chart visible in the extracted content and record
a short note naming the attempted read that did not complete. There is no thrown-exception path for an
adverse chart; any package-level fault propagates to the reader and Core.

### Dependencies

- **DocumentFormat.OpenXml** (OTS) — the packaging and spreadsheet-drawing types.
- **System.Xml** — the `XDocument` name-driven walk of the chart part.
- **ExcelChartModel** and its cached-data records — the model it populates. See the *Markdown*
  subsystem design.

### Callers

`ExcelOpenXmlReader.Read` calls `Collect` for each worksheet, so each sheet carries its charts. Nothing
else calls it.
