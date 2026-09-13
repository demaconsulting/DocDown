## ExcelOpenXmlImageReader

![DocDown.Excel Structure](DocDownExcelView.svg)

### Purpose

`ExcelOpenXmlImageReader` resolves a workbook's embedded images to their bytes and provenance, from the
drawing part attached to each worksheet. Its single responsibility is to yield each image's stored bytes
unchanged and to record which worksheet references each image, so the sheet association survives
extraction and the emitter can link each picture inline under its sheet.

### Data Model

`ExcelOpenXmlImageReader` is an `internal static class`. It returns an `ExcelImageCollection`: the distinct
embedded images, each carrying its complete bytes, content type, naming candidates, and every referring
worksheet's tab index; and a map from a 1-based worksheet tab index to the images that sheet shows, in
drawing order. A private `PartAccumulator` gathers one distinct part's referrers while walking the
package.

### Key Methods

- **`ExcelImageCollection Collect(WorkbookPart workbookPart)`** — walks each worksheet's drawing part in
  workbook (tab) order — the same order the cell reader uses — records every image part each drawing
  references, deduplicates by package-part identity, and builds the distinct images in first-occurrence
  order with every referrer and the per-worksheet ordered occurrences. A worksheet without a drawing part
  contributes nothing; a part already seen keeps its first sheet's naming candidates and gains the new
  referrer.
- **`OrderedPictureUris`** (private) — reads the ordered, distinct image-part URIs a drawing's pictures
  reference, in drawing order, so the emitter links each at its point of occurrence.
- **`EmbedIds`** (private) — reads every `r:embed` relationship id a picture carries across its raster
  blip and its scalable-graphic blip, so a picture that carries both is fully resolved.
- **`BuildImage`** (private) — reads a part's bytes and builds the `EmbeddedImage` with its naming
  candidates and the full referrer set as its source pages (a workbook has no template concept, so no
  image is ever flagged template-referenced).

### Error Handling

A null workbook part is rejected with `ArgumentNullException`. There is no other local error path: reading
a part is read-only I/O over an already-open package, and any package fault propagates to the reader and
thence to Core as a structured failure.

### Dependencies

- **DocDown.Core** — `EmbeddedImage`, `ImageTextCandidate`, `ImageTextSource`.
- **DocumentFormat.OpenXml** (OTS) — the packaging and spreadsheet-drawing types.

### Callers

`ExcelOpenXmlReader.Read` calls `Collect` once per workbook, before walking the worksheets, so each sheet
can attach its image references. Nothing else calls it.
