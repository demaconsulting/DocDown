### EmbeddedImageWriter Verification Design

This document describes the unit-level verification strategy for `EmbeddedImageWriter`, the shared
sink-write pipeline the Open XML backends use for embedded images.

#### Verification Approach

`EmbeddedImageWriter` is verified in isolation in `EmbeddedImageWriterTests.cs` under the `Output` folder of
`DemaConsulting.DocDown.Core.Tests`, with method names beginning with `EmbeddedImageWriter_`. Each test calls
the real `WriteAsync` with `EmbeddedImage` values over a `RecordingSink` from the shared test support project,
which records every image the writer passes to the sink. The image-size limits are real `ExtractionOptions`
values, and a PNG header is built in the test where a pixel size is needed. Nothing else is mocked, because the
unit's value is the accounting and the calls it makes to the sink.

#### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Sink**: `RecordingSink`, an in-memory `IExtractionSink`
- **Filesystem**: N/A - the tests are fully in memory
- **Isolation**: each test builds its own sink and image list

#### Acceptance Criteria

Per IEC 62304 §5.5.2, an `EmbeddedImageWriter` unit test run passes when two byte-identical images yield one
found image, one written image, and one shared path for both source references; when the page referrers and
template flag of identical content are merged into the single written image; when an image beyond the byte or
the dimension limit is counted as skipped and not written; and when a written EMF is counted in the vector
tally.

#### Test Scenarios

##### Identical content is written once

**Test**: `EmbeddedImageWriter_Write_DuplicateContent_CountsOnce`

Proves two images with equal bytes report one found and one written image, the sink receives one image, and
both source references resolve to the same path. Evidence for
`DocDownCore-Output-EmbeddedImageWriter-WritesDeduplicated`.

##### Referrers of identical content are merged

**Test**: `EmbeddedImageWriter_Write_DuplicateContent_MergesReferrerSets`

Proves two byte-identical images referenced from pages 3 and 1, one of them through a template, are written as
one image whose hint lists pages `[1, 3]`, whose scalar page is 1, and whose template flag is set. Evidence for
`DocDownCore-Output-EmbeddedImageWriter-MergesReferrers`.

##### A vector metafile is counted

**Test**: `EmbeddedImageWriter_Write_VectorMetafile_CountedAsVector`

Proves an `image/x-emf` image is written and counted once in the vector tally. Evidence for
`DocDownCore-Output-EmbeddedImageWriter-CountsVectorMetafiles`.

##### Oversized images are skipped and counted

**Tests**: `EmbeddedImageWriter_Write_ExceedsByteLimit_SkipsForSize`,
`EmbeddedImageWriter_Write_ExceedsDimensionLimit_SkipsForSize`

Proves an image above `MaxImageBytes` is counted as size-skipped and never reaches the sink, and that a PNG above
`MaxImageDimensionPx` is counted as size-skipped with a written count of zero. Evidence for
`DocDownCore-Output-EmbeddedImageWriter-SkipsOversized`.
