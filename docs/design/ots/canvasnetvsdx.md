## CanvasNet.Vsdx

### Purpose

`DemaConsulting.CanvasNet.Vsdx` is the managed Visio rasterization API
`DemaConsulting.DocDown.Visio.Rendering` is built on. It was chosen because it turns a VSDX/VSDM page
into pixel data through a small managed API, is fully managed with no native asset, ships real
`net8.0`, `net9.0`, and `net10.0` assets matching this repository's target frameworks exactly, and is
MIT licensed, compatible with this repository's MIT license. Adopting it lets this package add page
rendering without introducing a native, runtime-identifier-specific dependency of its own.

### Features Used

- `DemaConsulting.CanvasNet.Vsdx.VsdxDocument.Open(Stream)` — opens a Visio drawing from an
  in-memory stream
- `VsdxDocument.PageCount` — the page count that drives page-range selection
- `VsdxDocument.Render(int pageIndex, int dpi, VsdxRenderOptions?)` — rasterizes one page directly at
  a requested DPI, reading the page's size in EMUs and scaling by `dpi / 96`, returning a
  `DemaConsulting.CanvasNet.Canvas.Surface`

**Notable deviation: `dpi` is an `int`, not a `float`.** Unlike `CanvasNet.Pptx.PptxDocument.Render`,
which `DemaConsulting.DocDown.PowerPoint.Rendering` calls with a `float` DPI, CanvasNet.Vsdx's
`Render` overload accepts `dpi` as an `int`. `PageRenderer` passes the caller's DPI through
unchanged rather than casting, so this API deviation is documented rather than silently absorbed.

No other `CanvasNet.Vsdx` surface is used, and no type from it is named anywhere outside
`PageRenderer.cs`.

### Integration Pattern

`CanvasNet.Vsdx` is referenced as a real runtime dependency of the `DemaConsulting.DocDown.Visio.Rendering`
package. Its usage is confined to `PageRenderer`: a stateless, per-call open-render-dispose
sequence with no retained configuration and no process-level state of this package's own.

**Version pinning.** The package reference is pinned to an exact version range rather than a
floating minimum, for the same reason `DemaConsulting.DocDown.PowerPoint.Rendering` pins
`CanvasNet.Pptx`, and because this is a pre-1.0, beta-labeled release whose API has not yet committed
to semantic-versioning stability — a floating reference could pick up a breaking change between
builds.

**No `CanvasNet.Charts`-equivalent dependency.** Unlike `CanvasNet.Pptx`, which depends on
`CanvasNet.Charts` to rasterize embedded charts on a slide, `CanvasNet.Vsdx` has no comparable
dependency: Visio drawings have no embedded chart content to render, so this package's dependency
graph is `CanvasNet.Vsdx` and `CanvasNet` only.

**Thread-safety.** `CanvasNet.Vsdx` does not document its thread-safety either way. `PageRenderer`
opens an independent `VsdxDocument` and renders to an independent `Surface` per call, with no field
or static mutable state of its own, so there is nothing a lock would need to protect for concurrent
callers using distinct instances.

**Containment as a risk control.** `CanvasNet.Vsdx` types appear in exactly one source file —
`PageRenderer.cs` — and in no public signature of the package. A reflection test over the package's
exported types fails the build if any `CanvasNet.Vsdx` type reaches the public surface, so a host can
reference the registration seam without those types entering its own compilation.

**Native assets.** None. `CanvasNet.Vsdx` and its one dependency, `CanvasNet`, are fully managed.
`DemaConsulting.DocDown.Visio.Rendering`'s published output contains no `runtimes/` folder
and no `.dll`, `.so`, or `.dylib` native binary, and the package declares no `<RuntimeIdentifier>`.

**Known fidelity gaps.** Spline and bezier curve geometry and ellipse shapes have deferred support,
the arrow-style table is incomplete, and theme resolution is simplified — the same category of
rasterization-fidelity gap documented for `CanvasNet.Pptx`. A page relying on one of these features
may rasterize with reduced fidelity rather than with a reported fault.

**`.vsdm` support is untested extrapolation.** `CanvasNet.Vsdx` reads the same Open Packaging
Conventions container for both `.vsdx` and `.vsdm` — the macro-enabled variant differs only in its
content type — so macro-enabled drawings are expected to rasterize identically. This codebase has not
independently verified that expectation against a real `.vsdm` sample.

### Licensing

`CanvasNet.Vsdx` is MIT, published by DEMA Consulting. Its one dependency, `CanvasNet`, is also MIT
from the same maintainer — see *CanvasNet*. No copyleft license appears anywhere in the graph.
