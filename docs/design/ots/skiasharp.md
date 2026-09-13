## SkiaSharp

### Purpose

SkiaSharp is the 2D graphics library that holds the rasterized page bitmap and encodes it to PNG. It
reaches `DocDown.Pdf.Rendering` transitively through PDFtoImage at version `4.150.1`, with its native
backends delivered by the `SkiaSharp.NativeAssets.{Win32,Linux.NoDependencies,macOS}` packages.

It was accepted as part of confirming PDFtoImage, which uses SkiaSharp as its image type and encoder.
This package uses SkiaSharp only to receive the bitmap PDFtoImage produces and to encode it to the
PNG bytes the sink expects.

### Features Used

- `SkiaSharp.SKBitmap` as the in-memory image type PDFtoImage returns
- `SKBitmap.Encode(Stream, SKEncodedImageFormat.Png, quality)` to produce the PNG bytes
- `SkiaSharp.SKEncodedImageFormat.Png` to select the output format

No drawing, transform, or color-management surface is used; the page is already rasterized by PDFium,
so SkiaSharp's role here is bitmap ownership and PNG encoding only.

### Integration Pattern

SkiaSharp is referenced transitively as a runtime dependency and confined to `PageRenderer`. Its
native backends arrive as `runtimes/<rid>/native` assets resolved by the consuming project. The PNG
signature, header, and byte-for-byte reproducibility of a rendered page all originate in SkiaSharp's
encode step, which is why the determinism and PNG-validity tests are the evidence for this item.

**Runtime-identifier coverage.** The `SkiaSharp.NativeAssets.*` packages provide native backends for
the Windows, Linux (via the NoDependencies variant), and macOS runtime identifiers this repository
cares about, resolved automatically alongside PDFium.

### Licensing

SkiaSharp and its `SkiaSharp.NativeAssets.*` packages are MIT licensed, compatible with this
repository's MIT license.
