## PDFtoImage

### Purpose

PDFtoImage is the managed rasterization API `DocDown.Pdf.Rendering` is built on. It was chosen
because it turns a PDF page and a DPI into image bytes through a small managed API, ships real
`net8.0`, `net9.0`, and `net10.0` assets matching this repository's target frameworks exactly, and
resolves — with its native dependencies — to complete, automatic runtime-identifier coverage for
every platform DocDown cares about. It is MIT licensed, compatible with this repository's MIT
license.

It wraps two native components delivered transitively: PDFium (the native page renderer) and
SkiaSharp (the native 2D graphics and PNG-encode backend). Because it carries native assets, it is
confined to a separate, opt-in package rather than being part of `DocDown.Pdf`.

### Features Used

- `PDFtoImage.Conversion.SavePng(Stream, byte[], Index page, string? password, RenderOptions)` to
  rasterize a single page and write it to the stream as PNG in one call
- `PDFtoImage.RenderOptions(Dpi: …)` to set the render resolution
- `PDFtoImage.GetPageCount(byte[], string? password)` to read the page count that drives page
  selection, so the count comes from the component that will rasterize the pages
- native-library resolution through the PDFtoImage assembly for the cheap availability probe

No other PDFtoImage surface is used, and no type from either transitive native component is named
anywhere in DocDown. `SavePng` is used in preference to `ToImage` plus a `SkiaSharp.SKBitmap.Encode`
call precisely so that the SkiaSharp API stays an implementation detail of PDFtoImage rather than
becoming a direct dependency of this package. Multi-page and async conversion helpers are not used; this
package renders one page at a time so its own per-page fault isolation and page-range logic stay in
one place.

### Integration Pattern

PDFtoImage is referenced as a real runtime dependency of the `DocDown.Pdf.Rendering` package and
flows to consumers, like PdfPig for `DocDown.Pdf` and unlike the repository's build-time tools. Its
usage is confined to `PageRenderer`: a stateless render-one-page call plus a one-time cached
native-loadability probe. There is no retained configuration and no process-level state of this
package's own.

**Version pinning.** The package reference is pinned to an exact version range (`[5.4.0]`) rather
than a floating minimum, for the same reason `DocDown.Pdf` pins PdfPig and additionally because the
managed binding and the per-runtime native assets must match exactly. A floating reference could
substitute an incompatible API or a different native ABI.

**Thread-safety.** PDFtoImage's own documentation states that PDFium is not thread-safe and that all
calls into it are serialized behind a lock, so only one document can be rasterized at a time.
`PageRenderer` mirrors this with a process-wide lock of its own.

**Containment as a risk control.** PDFtoImage, PDFium, and SkiaSharp types appear in exactly one
source file — `PageRenderer.cs` — and in no public signature of the package. A reflection test over
the package's exported types fails the build if any reaches the public surface, so the native stack
stays confined and replaceable.

**Native assets.** Unlike every other DocDown runtime library, this package intentionally carries
native assets transitively — that is its whole reason for being separate. The consuming project
resolves the correct `runtimes/<rid>/native` asset at restore time; the package itself declares no
`<RuntimeIdentifier>` and stays runtime-identifier agnostic.

### Licensing

PDFtoImage is MIT. Its transitive dependencies are permissive and compatible: the
`bblanchon.PDFium.*` packages carry an Apache-2.0 nuspec over upstream PDFium's BSD-3-Clause code, and
SkiaSharp with its `SkiaSharp.NativeAssets.*` packages is MIT. No copyleft license appears anywhere in
the graph.
