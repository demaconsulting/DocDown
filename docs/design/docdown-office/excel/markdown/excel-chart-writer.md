## ExcelChartWriter

![DocDown.Excel Structure](DocDownExcelView.svg)

### Purpose

`ExcelChartWriter` renders one chart's cached data as markdown: the labeling that makes the numbers mean
something, then a table of categories against series values. Its single responsibility is to turn a
chart's cache — the only form in which a chart's information survives into a text extraction — into
something a reader can consume with no knowledge of the chart grammar at all.

### Data Model

`ExcelChartWriter` is an `internal static class`, pure and performing no I/O. An internal constant pins
the maximum number of plotted points rendered into a table, and a private constant names the fallback
category-column label. `Render` returns the chart part markdown as a string.

### Key Methods

- **`string Render(ExcelChartModel chart)`** — renders a chart. A chart whose part could not be read
  yields a chart part stating why; a chart read but caching no points yields a chart part stating that
  no data table could be produced; otherwise it appends the context (plot type, axis titles, a
  generated-title note when the title is automatic), the per-series inventory (name, cached-point
  count, declared-versus-stored count, number format, source reference), and a data table. The table
  has a leading point-index column, the category column, then one column per series, pipe characters
  escaped and line breaks flattened. It bounds the table at the plotted-point maximum and states in the
  part itself how many points were omitted.
- **`string Describe(ExcelChartModel chart)`** — produces the one-line description a worksheet part uses
  to name a chart and point at its own part, so a reader who opens only the worksheet still learns that
  the chart exists.
- **`CollectIndices`** (private) — the union of every point index the categories or any series declare,
  ascending, so a combination chart with differently indexed series drops nothing and a sparse cache
  stays sparse.
- **`Escape`** (private) — escapes the pipe and flattens line breaks so a cached label cannot break the
  column structure; cached values are never shortened.

### Error Handling

A null chart is rejected with `ArgumentNullException`. There is no other error path: the writer is pure,
reads only the model it was handed, and turns an unreadable chart, a chart with no cached data, or a
bounded table into stated markdown rather than an exception.

### Dependencies

- **ExcelChartModel** and its cached-data records — the chart it renders. See *ExcelChartReader Design*
  and the *Markdown* subsystem design.

### Callers

`ExcelContentEmitter` calls `Render` for each chart it writes as a `Chart` part, and `Describe` for each
chart it names under a worksheet. Nothing else calls it.
