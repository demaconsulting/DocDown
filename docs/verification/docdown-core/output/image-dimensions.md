### ImageDimensions Verification Design

This document describes the unit-level verification strategy for `ImageDimensions`, the header-only reader of
raster pixel dimensions.

#### Verification Approach

`ImageDimensions` is verified in isolation in `ImageDimensionsTests.cs` under the `Output` folder of
`DemaConsulting.DocDown.Core.Tests`, with method names beginning with `ImageDimensions_TryRead_`. The tests
build minimal PNG, JPEG, GIF, and BMP headers in memory and call the real `TryRead`. Nothing is mocked because
the unit is a pure function with no collaborators.

#### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: header byte arrays built by the test
- **Filesystem**: N/A
- **Isolation**: each test is independent with no shared state

#### Acceptance Criteria

Per IEC 62304 §5.5.2, an `ImageDimensions` unit test run passes when the width and height are read from PNG,
JPEG, GIF, and BMP headers; when a BMP height stored as a negative number is read as its absolute value; and
when a BMP whose height is `int.MinValue`, a non-raster payload, and a truncated payload are each reported as
unknown with `false` and without an exception.

#### Test Scenarios

##### Raster headers are read

**Tests**: `ImageDimensions_TryRead_Png_ReadsWidthAndHeight`, `ImageDimensions_TryRead_Jpeg_ReadsWidthAndHeight`,
`ImageDimensions_TryRead_Gif_ReadsWidthAndHeight`

Proves each of the three formats yields its width and height. Evidence for
`DocDownCore-Output-ImageDimensions-ReadsHeaders`.

##### A BMP height is read as an absolute value

**Test**: `ImageDimensions_TryRead_Bmp_ReadsWidthAndAbsoluteHeight`

Proves a bottom-up (positive) and a top-down (negative) stored height both read as the same positive height.
Evidence for `DocDownCore-Output-ImageDimensions-ReadsHeaders`.

##### Hostile or unknown input is reported unknown

**Tests**: `ImageDimensions_TryRead_BmpMinValueHeight_ReturnsFalseWithoutThrowing`,
`ImageDimensions_TryRead_NonRaster_ReturnsFalse`, `ImageDimensions_TryRead_Truncated_ReturnsFalse`

Proves a minimum-integer BMP height returns `false` without an overflow exception, an EMF-like payload returns
`false` with both values zero, and a truncated PNG signature returns `false`. Evidence for
`DocDownCore-Output-ImageDimensions-ReadsHeaders`.
