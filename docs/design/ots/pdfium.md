## PDFium

### Purpose

PDFium is the native page renderer that actually turns a PDF page's geometry into pixels. It reaches
`DocDown.Pdf.Rendering` transitively through PDFtoImage, delivered by the `bblanchon.PDFium.*`
native-asset packages (Win32, Linux, macOS) at version `152.0.7961`. It is the only native binary
DocDown depends on.

It was not chosen directly; it is the renderer PDFtoImage is built on, and it was accepted as part of
confirming PDFtoImage. It provides the raster of a page at a requested resolution, which SkiaSharp
then encodes to PNG.

### Features Used

PDFium is not called directly by this package — it is driven entirely through PDFtoImage's managed
API. The features that matter are therefore the native capabilities PDFtoImage exposes:

- rasterization of a single page to a bitmap at a requested DPI
- loading of the correct native binary for the current runtime identifier, used both to render and,
  through `PageRenderer`'s cheap probe, to report availability without throwing

### Integration Pattern

PDFium arrives as a set of `runtimes/<rid>/native` assets in the package graph and is resolved by the
consuming project at restore and run time. This package never P/Invokes it directly; the only
interaction is through PDFtoImage inside `PageRenderer`, under the process-wide serialization lock
that PDFium's thread-unsafety requires.

**Thread-safety.** PDFium is not thread-safe. This is the root cause of the process-wide lock in
`PageRenderer` and of PDFtoImage's own internal serialization.

**Runtime-identifier coverage.** The `bblanchon.PDFium.*` packages provide native binaries for the
Windows, Linux (including musl), and macOS runtime identifiers this repository cares about, resolved
automatically without hand-curation.

**Native asset consequence.** Because PDFium is a native binary, a self-contained single-file publish
of the `docdown` tool is runtime-identifier specific and requires `dotnet publish -r <rid>`; a
framework-dependent install resolves the correct native asset at run time. See the
`DocDown.Pdf.Rendering` system design's native-binary consequences.

### Licensing

Upstream PDFium is BSD-3-Clause. The `bblanchon.PDFium.*` redistribution packages carry an Apache-2.0
nuspec. Both are permissive and compatible with this repository's MIT license; no copyleft applies.
