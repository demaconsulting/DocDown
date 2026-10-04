## CanvasNet

### Purpose

`DemaConsulting.CanvasNet` is the managed canvas and codec library `DocDown.Pdf.Rendering` uses to
encode a rasterized page as PNG. It provides a mutable, span-based 32-bit RGBA pixel buffer (`Surface`)
and codecs for common image formats; this package uses only the `Surface` type `CanvasNet.Pdf`
returns and the `PngCodec` it saves through. It was chosen because it is fully managed with no
native asset, MIT-licensed and compatible with this repository's MIT license, and is the library
`CanvasNet.Pdf` itself rasterizes onto, so using it directly for PNG encoding keeps the whole
rasterization path — decode, draw, and encode — on one dependency rather than introducing a second,
unrelated image-encoding library.

### Two related items, not one

`CanvasNet` and `CanvasNet.Pdf` are recorded as **two** OTS items, not one, even though both are
published by the same maintainer (DEMA Consulting) and pinned to the same exact version. The
governing rule is *one OTS item per independently-sourced component*, and unlike
`DocumentFormat.OpenXml` and its `Framework` companion — which are one component with an
implementation seam, never referenced independently — `CanvasNet` is a general-purpose, standalone
canvas and codec library with its own purpose (bitmap I/O for BMP, PNG, TIFF, JPEG, and GIF) that
`DocDown.Pdf.Rendering` references **directly** for `PngCodec.Save`, not merely transitively through
`CanvasNet.Pdf`. Two packages that are each referenced directly, for genuinely different features, are
recorded as two items even when they ship from the same maintainer in lockstep.

### Features Used

- `DemaConsulting.CanvasNet.Canvas.Surface` — the pixel buffer type `CanvasNet.Pdf.Render` returns,
  passed through unmodified to the codec
- `DemaConsulting.CanvasNet.Codecs.PngCodec.Save(Surface, Stream, PngColorType)` — encodes a surface
  as PNG bytes, the final step that turns a rasterized page into the file this package writes

No other `CanvasNet` surface is used: no BMP, TIFF, JPEG, GIF, or SVG codec, and no drawing or
compositing API, because this package only ever encodes a page `CanvasNet.Pdf` already rasterized.

### Integration Pattern

`CanvasNet` is referenced as a real runtime dependency of the `DocDown.Pdf.Rendering` package and
flows to consumers, like `CanvasNet.Pdf` and `PdfPig`. Its usage is confined to `PageRenderer`: one
call to `PngCodec.Save` per rendered page, with no retained configuration and no process-level state
of this package's own.

**Version pinning.** The package reference is pinned to an exact version range rather than a floating
minimum, for the same reproducibility reason `DocDown.Pdf` pins PdfPig, and additionally because
`CanvasNet.Pdf`'s own `Surface` and `CanvasNet`'s `PngCodec` must agree on the same pixel-buffer
layout — a floating reference on either package independently could desynchronize the pair.

**Containment as a risk control.** `CanvasNet` types appear in exactly one source file —
`PageRenderer.cs` — and in no public signature of the package. A reflection test over the package's
exported types fails the build if `Surface`, `PngCodec`, or any other `CanvasNet` type reaches the
public surface, so a host can reference the registration seam without those types entering its own
compilation.

**Native assets.** None. `CanvasNet` is fully managed: its own only dependency is
`System.Numerics.Tensors`, and neither ships a `runtimes/` asset. `DocDown.Pdf.Rendering`'s published
output contains no native binary of any kind.

### Licensing

`CanvasNet` is MIT, published by DEMA Consulting — the same license this repository uses, from the
same maintainer as DocDown itself. Its one dependency, `System.Numerics.Tensors`, is MIT from
Microsoft. No copyleft license appears anywhere in the graph.
