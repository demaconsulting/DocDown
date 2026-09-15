## PageRenderer

![DocDown.Pdf.Rendering Structure](DocDownPdfRenderingView.svg)

### Purpose

`PageRenderer` is the single native-interop seam of this package. Its responsibility is to rasterize
one PDF page to a PNG through PDFtoImage — PDFium for the raster, SkiaSharp for the encode — and to
answer, cheaply and without throwing, whether the native stack can load at all in this environment.

Confining every native call to this one unit is what keeps the rest of the package, and the rest of
DocDown, fully managed and reasoned about without the native stack in view.

### Data Model

`PageRenderer` is an `internal static class`: PDFium's state is a property of the process-global
native renderer, not of any instance, so instance state would be misleading. It holds two static
locks — one that serializes rasterization and one that guards the one-time availability probe — plus
the cached probe result. Its companion `NativeProbeResult` is an internal immutable record carrying
`IsAvailable` and a reason, with a shared `Available` value and an `Unavailable(reason)` factory that
refuses an empty reason.

### Key Methods

- **`Render(byte[] pdf, int pageIndexZeroBased, int dpi)`** — runs the PDFtoImage rasterization and
  PNG encode under a process-wide lock, returning the PNG bytes. Rejects a null document up
  front. Any native or memory fault surfaces as a thrown exception the caller isolates per page.
- **`ProbeAvailability()`** — loads the PDFium native once through the PDFtoImage assembly's own
  native-resolution path (honoring the package graph's `runtimes/<rid>/native` asset), caches the
  outcome, and returns `NativeProbeResult.Available` or `NativeProbeResult.Unavailable(reason)`.
  Never throws and never rasterizes.

### Error Handling

The availability probe catches every load fault and converts it into an unavailable result naming the
runtime identifier, so it can never throw. `Render` deliberately does not catch: it lets a native or
memory fault propagate so the extractor can isolate it per page and turn it into a plain note. The
`CA1416` platform-support warning on the PDFtoImage call is suppressed with justification because
PDFtoImage is supported on every platform DocDown targets.

### Dependencies

- **PDFtoImage** (OTS) — `Conversion.SavePng` and `RenderOptions`, which rasterize the page and write
  its PNG bytes in one call. No type from the transitive native stack is named here. See *PDFtoImage*
  under the OTS integration design.

### Callers

`PdfPageRenderingExtractor`, per page during an extraction and once during its availability probe and
self-test. Nothing outside this package calls it.
