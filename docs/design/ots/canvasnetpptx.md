## CanvasNet.Pptx

### Purpose

`DemaConsulting.CanvasNet.Pptx` is the managed PowerPoint rasterization API
`DemaConsulting.DocDown.PowerPoint.Rendering` is built on. It was chosen because it turns a PPTX slide into pixel
data through a small managed API, is fully managed with no native asset, ships real `net8.0`,
`net9.0`, and `net10.0` assets matching this repository's target frameworks exactly, and is MIT
licensed, compatible with this repository's MIT license. Adopting it lets this package add slide
rendering without introducing a native, runtime-identifier-specific dependency of its own.

### Features Used

- `DemaConsulting.CanvasNet.Pptx.PptxDocument.Open(Stream)` — opens a PPTX presentation from an
  in-memory stream
- `PptxDocument.SlideCount` — the slide count that drives page-range selection
- `PptxDocument.Render(int slideIndex, float dpi)` — rasterizes one slide directly at a requested
  DPI, reading the slide's size in EMUs and scaling by `dpi / 96`, returning a
  `DemaConsulting.CanvasNet.Canvas.Surface`

No other `CanvasNet.Pptx` surface is used, and no type from it is named anywhere outside
`SlideRenderer.cs`.

### Integration Pattern

`CanvasNet.Pptx` is referenced as a real runtime dependency of the `DemaConsulting.DocDown.PowerPoint.Rendering`
package. Its usage is confined to `SlideRenderer`: a stateless, per-call open-render-dispose
sequence with no retained configuration and no process-level state of this package's own.

**Version pinning.** The package reference is pinned to an exact version range rather than a
floating minimum, for the same reason `DemaConsulting.DocDown.Pdf.Rendering` pins `CanvasNet.Pdf`, and because this
is a pre-1.0, beta-labeled release whose API has not yet committed to semantic-versioning stability —
a floating reference could pick up a breaking change between builds.

**Thread-safety.** `CanvasNet.Pptx` does not document its thread-safety either way. `SlideRenderer`
opens an independent `PptxDocument` and renders to an independent `Surface` per call, with no field
or static mutable state of its own, so there is nothing a lock would need to protect for concurrent
callers using distinct instances.

**Containment as a risk control.** `CanvasNet.Pptx` types appear in exactly one source file —
`SlideRenderer.cs` — and in no public signature of the package. A reflection test over the package's
exported types fails the build if any `CanvasNet.Pptx` type reaches the public surface, so a host can
reference the registration seam without those types entering its own compilation.

**Native assets.** None. `CanvasNet.Pptx` and its dependencies, `CanvasNet` and `CanvasNet.Charts`,
are fully managed. `DemaConsulting.DocDown.PowerPoint.Rendering`'s published output contains no `runtimes/` folder
and no `.dll`, `.so`, or `.dylib` native binary, and the package declares no `<RuntimeIdentifier>`.

### Licensing

`CanvasNet.Pptx` is MIT, published by DEMA Consulting. Its dependencies, `CanvasNet` and
`CanvasNet.Charts`, are also MIT from the same maintainer — see *CanvasNet* and *CanvasNet.Charts*.
No copyleft license appears anywhere in the graph.
