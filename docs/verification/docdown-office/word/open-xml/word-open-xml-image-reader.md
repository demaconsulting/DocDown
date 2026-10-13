## WordOpenXmlImageReader Verification Design

This document describes the unit-level verification strategy for `WordOpenXmlImageReader`, the unit
that resolves a document's embedded image parts to their bytes and provenance.

### Verification Approach

`WordOpenXmlImageReader` is verified through unit tests in
`OpenXml/WordOpenXmlImageReaderTests.cs` in `DemaConsulting.DocDown.Office.Tests`, with method names
beginning with `WordOpenXmlImageReader_`.

The unit is driven directly against an opened `WordprocessingDocument`: the tests call `Resolve()`
with the relationship id of the first image part, then pass the returned reference to the emitter's
`HintFor()` to obtain the passthrough hint — the two calls a real extraction makes. The hint
assertions (transform, dimensions, media type) therefore observe `WordContentEmitter.HintFor()` as much
as the image reader; only the byte-identity assertion is exclusively about this unit's output. The
passthrough scenario asserts byte identity against the fixture's
retained source PNG (`DocxFixtures.PngBytes()`), so an image reader that silently re-encoded while
still reporting a passthrough would fail on the bytes, not on the label. The vector scenario
asserts the media type carries `emf` case-insensitively, so downstream reporting records the image
type truthfully. The tests supply no text candidates, so the naming and negative paths (null or
unresolvable relationship ids) are not exercised at unit level.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: WordprocessingML documents generated at test time, one carrying a PNG part and one
  carrying an EMF part, both from `TestData/DocxFixtures.cs`
- **Filesystem**: none; each fixture is opened from a `MemoryStream`
- **Mocking**: none; the SDK is exercised against real documents
- **Isolation**: each test opens its own document

### Acceptance Criteria

Per IEC 62304 §5.5.2, a `WordOpenXmlImageReader` test run passes when a PNG image part resolves
once with the passthrough transform (as built by `HintFor()`), unstated width and height, the
`image/png` media type, and bytes byte-identical to the fixture's retained source PNG; and when an
EMF image part resolves with a media type identifying the vector metafile truthfully.

### Test Scenarios

#### A PNG image is a passthrough with unstated dimensions

**Test**: `WordOpenXmlImageReader_Read_Png_YieldsPassthroughWithNullDimensions`

Proves the resolved reference's bytes are byte-identical to `DocxFixtures.PngBytes()`, and that the
hint `HintFor()` builds from it has transform `Passthrough`, `null` `WidthPx` and `HeightPx`, and
media type `image/png`. Evidence for
`DocDownWord-OpenXml-WordOpenXmlImageReader-YieldsPassthroughImages` and
`DocDownWord-OpenXml-WordOpenXmlImageReader-RecordsPassthroughProvenance`.

#### An EMF image reports a vector media type

**Test**: `WordOpenXmlImageReader_Read_Emf_ReportsVectorMediaType`

Proves the hint built for a resolved EMF image carries a media type containing `emf`
case-insensitively. Evidence
for `DocDownWord-OpenXml-WordOpenXmlImageReader-ReportsVectorMediaType`.
