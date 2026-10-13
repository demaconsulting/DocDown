### ImageDimensions

![Output Structure](OutputView.svg)

#### Purpose

`ImageDimensions` reads the pixel width and height of a raster image directly from its header bytes. The
Open XML backends ship no imaging library, yet they must honor `ExtractionOptions.MaxImageDimensionPx` and
record honest `WidthPx` and `HeightPx` values, so the size is read from the fixed header field of each
common format without decoding pixel data.

#### Data Model

`ImageDimensions` is a `static class` with no state, safe for concurrent use. The caller supplies the
complete image bytes and receives two `int` out values.

#### Key Methods

- **`bool TryRead(byte[] bytes, out int width, out int height)`** — tries PNG, JPEG, GIF, and BMP in that
  order and returns `true` with the dimensions for the first format whose header parses. PNG is read from
  the `IHDR` chunk, JPEG from its start-of-frame marker, GIF from the logical screen descriptor, and BMP
  from the little-endian fields at offsets 18 and 22, where a negative (top-down) height is returned as its
  absolute value. A format is accepted only when both dimensions are positive. Any other input, including
  vector metafiles, truncated data, and a BMP whose stored height is `int.MinValue`, returns `false`. Pure.

#### Error Handling

A null array is rejected with `ArgumentNullException`. Unrecognized or malformed content is never an
exception: it is reported as unknown by returning `false`, so a caller learns the size only when it is a
recorded fact.

#### Dependencies

None beyond the base class library.

#### Callers

`EmbeddedImageWriter.WriteAsync` is the only production caller. It uses `TryRead` for the dimension limit and
the recorded pixel size. See _EmbeddedImageWriter Design_.
