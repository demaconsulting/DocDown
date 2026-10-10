## PageRenderer

![DemaConsulting.DocDown.Visio.Rendering Structure](DocDownVisioRenderingView.svg)

### Purpose

`PageRenderer` is the single rasterization seam of this package. Its responsibility is to rasterize
one Visio page to a PNG through `DemaConsulting.CanvasNet.Vsdx` — a fully-managed Visio
rendering stack — and `DemaConsulting.CanvasNet`'s `PngCodec` for the PNG encode.

Confining every CanvasNet.Vsdx/CanvasNet call to this one unit is what keeps the rest of the package,
and the rest of DocDown, free of a dependency on either package's types.

### Data Model

`PageRenderer` is an `internal static class` with no instance or shared mutable state: each call
opens its own `VsdxDocument`, renders into its own `Surface`, and disposes both before returning.

### Key Methods

- **`Render(byte[] vsdx, int pageIndexZeroBased, int dpi)`** — opens a `VsdxDocument` over the
  supplied bytes and renders the requested page through
  `VsdxDocument.Render(int pageIndex, int dpi, VsdxRenderOptions?)`, which reads the page's size in
  EMUs, scales by `dpi / 96`, and rounds to the nearest pixel, preserving the page's aspect ratio.
  The resulting `Surface` is encoded to PNG bytes with `PngCodec.Save`. Rejects a null document up
  front. The DPI overload deliberately accepts `dpi` as an `int`, not a `float`, unlike
  `CanvasNet.Pptx.PptxDocument`'s own float-DPI overload used by
  `DemaConsulting.DocDown.PowerPoint.Rendering`'s `SlideRenderer` — this is a documented API
  deviation in CanvasNet.Vsdx, passed through unchanged here rather than cast. Any fault during open,
  render, or encode surfaces as a thrown exception the caller isolates per page.
- **`GetPageCount(byte[] vsdx)`** — opens a `VsdxDocument` over the supplied bytes and returns its
  `PageCount`. Rejects a null document up front. Kept here rather than in the caller so this type
  stays the package's single rasterization seam: the count comes from the same component that will
  rasterize the pages.

### Error Handling

`Render` and `GetPageCount` deliberately do not catch: they let any fault from opening, rendering, or
encoding propagate so the extractor can isolate it per page and turn it into a plain note.

### Dependencies

- **CanvasNet.Vsdx** (OTS) — `VsdxDocument.Open`, `PageCount`, and `Render`, which rasterize the
  page into a `Surface`. See *CanvasNet.Vsdx* under the OTS integration design.
- **CanvasNet** (OTS) — `PngCodec.Save`, which encodes the rendered `Surface` to PNG bytes. See
  *CanvasNet* under the OTS integration design.

Unlike `DemaConsulting.DocDown.PowerPoint.Rendering`'s `SlideRenderer`, this unit has no
`CanvasNet.Charts`-equivalent dependency to carry, since CanvasNet.Vsdx has no chart-rasterization
dependency of its own.

### Callers

`VisioPageRenderingExtractor`, per page during an extraction, during its page-count lookup, and once
during its self-test. Nothing outside this package calls it.
