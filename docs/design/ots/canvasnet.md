## CanvasNet

### Purpose

`DemaConsulting.CanvasNet` is the managed canvas and codec library both `DemaConsulting.DocDown.Pdf.Rendering` and
`DemaConsulting.DocDown.PowerPoint.Rendering` use to encode a rasterized page or slide as PNG. It provides a
mutable, span-based 32-bit RGBA pixel buffer (`Surface`) and codecs for common image formats; these
packages use only the `Surface` type `CanvasNet.Pdf`/`CanvasNet.Pptx` return and the `PngCodec` they
save through. It was chosen because it is fully managed with no native asset, MIT-licensed and
compatible with this repository's MIT license, and is the library `CanvasNet.Pdf` and `CanvasNet.Pptx`
themselves rasterize onto, so using it directly for PNG encoding keeps the whole rasterization path
— decode, draw, and encode — on one dependency rather than introducing a second, unrelated
image-encoding library.

### Related items, not one

`CanvasNet`, `CanvasNet.Pdf`, and `CanvasNet.Pptx` are recorded as **separate** OTS items, not one,
even though all three are published by the same maintainer (DEMA Consulting) and pinned to the same
exact version. The governing rule is *one OTS item per independently-sourced component*, and unlike
`DocumentFormat.OpenXml` and its `Framework` companion — which are one component with an
implementation seam, never referenced independently — `CanvasNet` is a general-purpose, standalone
canvas and codec library with its own purpose (bitmap I/O for BMP, PNG, TIFF, JPEG, and GIF) that
`DemaConsulting.DocDown.Pdf.Rendering` and `DemaConsulting.DocDown.PowerPoint.Rendering` each reference **directly** for
`PngCodec.Save`, not merely transitively through `CanvasNet.Pdf` or `CanvasNet.Pptx`. Packages that
are each referenced directly, for genuinely different features, are recorded as separate items even
when they ship from the same maintainer in lockstep.

### Features Used

- `DemaConsulting.CanvasNet.Canvas.Surface` — the pixel buffer type `CanvasNet.Pdf.Render` and
  `CanvasNet.Pptx.PptxDocument.Render` return, passed through unmodified to the codec
- `DemaConsulting.CanvasNet.Codecs.PngCodec.Save(Surface, Stream, PngColorType)` — encodes a surface
  as PNG bytes, the final step that turns a rasterized page or slide into the file these packages
  write

No other `CanvasNet` surface is used: no BMP, TIFF, JPEG, GIF, or SVG codec, and no drawing or
compositing API, because these packages only ever encode a page or slide `CanvasNet.Pdf` or
`CanvasNet.Pptx` already rasterized.

### Integration Pattern

`CanvasNet` is referenced as a real runtime dependency of both the `DemaConsulting.DocDown.Pdf.Rendering` and
`DemaConsulting.DocDown.PowerPoint.Rendering` packages. Its usage is confined to `PageRenderer` and `SlideRenderer`
respectively: one call to `PngCodec.Save` per rendered page or slide, with no retained configuration
and no process-level state of either package's own.

**Version pinning.** The package reference is pinned to an exact version range rather than a floating
minimum, for the same reproducibility reason `DemaConsulting.DocDown.Pdf` pins PdfPig, and additionally because
`CanvasNet.Pdf`/`CanvasNet.Pptx`'s own `Surface` and `CanvasNet`'s `PngCodec` must agree on the same
pixel-buffer layout — a floating reference on any of these packages independently could desynchronize
the set.

**Containment as a risk control.** `CanvasNet` types appear in exactly one source file per package —
`PageRenderer.cs` in `DemaConsulting.DocDown.Pdf.Rendering`,
`SlideRenderer.cs` in `DemaConsulting.DocDown.PowerPoint.Rendering` —
and in no public signature of either package. A reflection test over each package's exported types
fails the build if `Surface`, `PngCodec`, or any other `CanvasNet` type reaches the public surface,
so a host can reference the registration seam without those types entering its own compilation.

**Native assets.** None. `CanvasNet` is fully managed: its own only dependency is
`System.Numerics.Tensors`, and neither ships a `runtimes/` asset. Neither `DemaConsulting.DocDown.Pdf.Rendering`'s
nor `DemaConsulting.DocDown.PowerPoint.Rendering`'s published output contains a native binary of any kind.

### Licensing

`CanvasNet` is MIT, published by DEMA Consulting — the same license this repository uses, from the
same maintainer as DocDown itself. Its one dependency, `System.Numerics.Tensors`, is MIT from
Microsoft. No copyleft license appears anywhere in the graph.
