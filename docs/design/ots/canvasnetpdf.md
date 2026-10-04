## CanvasNet.Pdf

### Purpose

`DemaConsulting.CanvasNet.Pdf` is the managed PDF rasterization API `DocDown.Pdf.Rendering` is built
on. It was chosen because it turns a PDF page into pixel data through a small managed API, is fully
managed with no native asset, ships real `net8.0`, `net9.0`, and `net10.0` assets matching this
repository's target frameworks exactly, and is MIT licensed, compatible with this repository's MIT
license. Replacing the previously used PDFtoImage (PDFium/SkiaSharp) backend with `CanvasNet.Pdf`
removed every native asset, runtime-identifier concern, and native thread-safety constraint this
package used to carry, without changing the page-rendering feature it provides.

### Features Used

- `DemaConsulting.CanvasNet.Pdf.PdfDocument.Open(Stream, string?)` — opens a PDF document from an
  in-memory stream, optionally with a password
- `PdfDocument.PageCount` — the page count that drives page-range selection
- `PdfDocument.GetPageInfo(int)` — the rotation-adjusted page width and height in points, used to
  convert the requested DPI into pixel dimensions (`pixels = points * dpi / 72`)
- `PdfDocument.Render(int pageIndex, float dpi)` — rasterizes one page directly at a requested DPI,
  returning a `DemaConsulting.CanvasNet.Canvas.Surface`

No other `CanvasNet.Pdf` surface is used, and no type from it is named anywhere outside
`PageRenderer.cs`.

### Integration Pattern

`CanvasNet.Pdf` is referenced as a real runtime dependency of the `DocDown.Pdf.Rendering` package and
flows to consumers, like PdfPig for `DocDown.Pdf`. Its usage is confined to `PageRenderer`: a
stateless, per-call open-render-dispose sequence with no retained configuration and no process-level
state of this package's own.

**Version pinning.** The package reference is pinned to an exact version range rather than a floating
minimum, for the same reason `DocDown.Pdf` pins PdfPig and additionally because
this is a pre-1.0, beta-labeled release whose API has not yet committed to semantic-versioning
stability — a floating reference could pick up a breaking change between builds.

**Thread-safety.** `CanvasNet.Pdf` does not document its thread-safety either way. Rather than
serializing calls behind a process-wide lock as the previous PDFium-based backend did, `PageRenderer`
opens an independent `PdfDocument` and renders to an independent `Surface` per call, with no field or
static mutable state of its own, so there is nothing a lock would need to protect for concurrent
callers using distinct instances. The one accepted residual risk — `CanvasNet`'s lazily-initialized
system font catalog, used when rasterizing text with a non-embedded font — is recorded as a risk
control measure in the system design document rather than mitigated here.

**Containment as a risk control.** `CanvasNet.Pdf` types appear in exactly one source file —
`PageRenderer.cs` — and in no public signature of the package. A reflection test over the package's
exported types fails the build if any `CanvasNet.Pdf` type reaches the public surface, so a host can
reference the registration seam without those types entering its own compilation.

**Native assets.** None. Unlike the native-carrying package this one replaces, `CanvasNet.Pdf` and its
one dependency, `CanvasNet`, are fully managed. `DocDown.Pdf.Rendering`'s published output contains no
`runtimes/` folder and no `.dll`, `.so`, or `.dylib` native binary, and the package declares no
`<RuntimeIdentifier>`.

### Licensing

`CanvasNet.Pdf` is MIT, published by DEMA Consulting. Its one dependency, `CanvasNet`, is also MIT
from the same maintainer — see *CanvasNet*. No copyleft license appears anywhere in the graph.
