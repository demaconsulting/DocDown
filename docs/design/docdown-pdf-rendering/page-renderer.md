## PageRenderer

![DocDown.Pdf.Rendering Structure](DocDownPdfRenderingView.svg)

### Purpose

`PageRenderer` is the single rasterization seam of this package. Its responsibility is to rasterize
one PDF page to a PNG through `DemaConsulting.CanvasNet.Pdf` — a fully-managed PDF rendering stack —
and `DemaConsulting.CanvasNet`'s `PngCodec` for the PNG encode.

Confining every CanvasNet.Pdf/CanvasNet call to this one unit is what keeps the rest of the package,
and the rest of DocDown, free of a dependency on either package's types.

### Data Model

`PageRenderer` is an `internal static class` with no instance or shared mutable state: each call
opens its own `PdfDocument`, renders into its own `Surface`, and disposes both before returning.

### Key Methods

- **`Render(byte[] pdf, int pageIndexZeroBased, int dpi)`** — opens a `PdfDocument` over the
  supplied bytes, renders the requested page at the requested DPI through CanvasNet.Pdf's
  `Render(pageIndex, dpi)` overload (which computes pixel width/height from the page's point size
  using the standard `pixels = points * dpi / 72` conversion), and encodes the resulting `Surface`
  to PNG bytes with `PngCodec.Save`. Rejects a null document up front. Any fault during open,
  render, or encode surfaces as a thrown exception the caller isolates per page.
- **`GetPageCount(byte[] pdf)`** — opens a `PdfDocument` over the supplied bytes and returns its
  `PageCount`. Rejects a null document up front.

### Error Handling

`Render` and `GetPageCount` deliberately do not catch: they let any fault from opening, rendering, or
encoding propagate so the extractor can isolate it per page and turn it into a plain note.

### Dependencies

- **CanvasNet.Pdf** (OTS) — `PdfDocument.Open`, `GetPageInfo`, and `Render`, which rasterize the page
  into a `Surface`. See *CanvasNet.Pdf* under the OTS integration design.
- **CanvasNet** (OTS) — `PngCodec.Save`, which encodes the rendered `Surface` to PNG bytes. See
  *CanvasNet* under the OTS integration design.

### Callers

`PdfPageRenderingExtractor`, per page during an extraction, during its page-count lookup, and once
during its self-test. Nothing outside this package calls it.
