## ExcelDrawingTextReader

![DocDown.Excel Structure](ExcelView.svg)

### Purpose

`ExcelDrawingTextReader` reads the text carried by the drawing shapes floating over a worksheet — the
callouts, labels, and annotations that live in the drawing layer and in no cell. Its single responsibility
is to recover that text, because a backend that read cells and pictures alone would drop it while
reporting a complete extraction — the same failure mode as a dropped chart, in a quieter place.

### Data Model

`ExcelDrawingTextReader` is an `internal static class`, stateless and thread-safe. It returns the
non-empty shape texts of a worksheet's drawing, in drawing order.

### Key Methods

- **`IReadOnlyList<string> Collect(WorksheetPart? worksheetPart)`** — reads the text of every shape in the
  worksheet's drawing, in drawing order, including shapes nested inside groups, and drops a shape that
  carries no text. A worksheet with no drawing part yields an empty list. Group membership is flattened
  because a grouped callout is read no differently by a person looking at the sheet, and preserving the
  grouping would add structure a reader cannot act on.
- **`TextOf`** (private) — reads one shape's text, concatenating the runs within a paragraph so a label
  split across formatting runs is not torn apart, and joining paragraphs with a space so a multi-line
  callout stays one readable annotation rather than fragments a reader must reassemble.

### Error Handling

There is no thrown-exception path: a shape with no text body yields an empty string, and reading is
read-only over an already-open package. Any package-level fault propagates to the reader and Core as a
structured failure.

### Dependencies

- **DocumentFormat.OpenXml** (OTS) — the packaging and spreadsheet-drawing types and the DrawingML text
  types.

### Callers

`ExcelOpenXmlReader.Read` calls `Collect` for each worksheet, so each sheet carries its shape annotations.
Nothing else calls it.
