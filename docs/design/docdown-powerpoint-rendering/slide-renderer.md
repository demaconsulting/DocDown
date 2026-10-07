## SlideRenderer

![DocDown.PowerPoint.Rendering Structure](DocDownPowerPointRenderingView.svg)

### Purpose

`SlideRenderer` is the single rasterization seam of this package. Its responsibility is to rasterize
one PPTX slide to a PNG through `DemaConsulting.CanvasNet.Pptx` — a fully-managed PowerPoint
rendering stack — and `DemaConsulting.CanvasNet`'s `PngCodec` for the PNG encode.

Confining every CanvasNet.Pptx/CanvasNet call to this one unit is what keeps the rest of the package,
and the rest of DocDown, free of a dependency on either package's types.

### Data Model

`SlideRenderer` is an `internal static class` with no instance or shared mutable state: each call
opens its own `PptxDocument`, renders into its own `Surface`, and disposes both before returning.

### Key Methods

- **`Render(byte[] pptx, int slideIndexZeroBased, int dpi)`** — opens a `PptxDocument` over the
  supplied bytes and renders the requested slide through
  `PptxDocument.Render(int slideIndex, float dpi)`, which reads the slide's size in EMUs, scales by
  `dpi / 96`, and rounds to the nearest pixel, preserving the slide's aspect ratio. The resulting
  `Surface` is encoded to PNG bytes with `PngCodec.Save`. Rejects a null document up front. Any
  fault during open, render, or encode surfaces as a thrown exception the caller isolates per slide.
- **`GetSlideCount(byte[] pptx)`** — opens a `PptxDocument` over the supplied bytes and returns its
  `SlideCount`. Rejects a null document up front.

### Error Handling

`Render` and `GetSlideCount` deliberately do not catch: they let any fault from opening, rendering, or
encoding propagate so the extractor can isolate it per slide and turn it into a plain note.

### Dependencies

- **CanvasNet.Pptx** (OTS) — `PptxDocument.Open`, `SlideCount`, and `Render`, which rasterize the
  slide into a `Surface`. See *CanvasNet.Pptx* under the OTS integration design.
- **CanvasNet.Charts** (OTS) — used internally by CanvasNet.Pptx to rasterize embedded charts; this
  unit references it only so the dependency resolves, and names no type from it. See
  *CanvasNet.Charts* under the OTS integration design.
- **CanvasNet** (OTS) — `PngCodec.Save`, which encodes the rendered `Surface` to PNG bytes. See
  *CanvasNet* under the OTS integration design.

### Callers

`PowerPointPageRenderingExtractor`, per slide during an extraction, during its slide-count lookup,
and once during its self-test. Nothing outside this package calls it.
