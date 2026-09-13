## WordOpenXmlImageReader Verification Design

This document describes the unit-level verification strategy for `WordOpenXmlImageReader`, the unit
that resolves a document's embedded image parts and yields each one's bytes with an honest
transform hint.

### Verification Approach

`WordOpenXmlImageReader` is verified through unit tests in
`OpenXml/WordOpenXmlImageReaderTests.cs` in `DemaConsulting.DocDown.Word.Tests`, with method names
beginning with `WordOpenXmlImageReader_`.

The unit is driven directly against an opened `WordprocessingDocument`, because its contract is the
sequence of image records and their hints — the level at which a passthrough claim can be checked
against the true stored bytes. The passthrough scenario asserts byte identity against the fixture's
retained source PNG (`DocxFixtures.PngBytes()`), so an image reader that silently re-encoded while
still reporting a passthrough would fail on the bytes, not on the label. The vector scenario
asserts the media type carries `emf` case-insensitively, so downstream reporting records the image
type truthfully.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: WordprocessingML documents generated at test time, one carrying a PNG part and one
  carrying an EMF part, both from `TestData/DocxFixtures.cs`
- **Filesystem**: none; each fixture is opened from a `MemoryStream`
- **Mocking**: none; the SDK is exercised against real documents
- **Isolation**: each test opens its own document

### Acceptance Criteria

Per IEC 62304 §5.5.2, a `WordOpenXmlImageReader` test run passes when a PNG image part is yielded
once with the passthrough transform, unstated width and height, the `image/png` media type, and
bytes byte-identical to the fixture's retained source PNG; and when an EMF image part is yielded
with a media type identifying the vector metafile truthfully.

### Test Scenarios

#### A PNG image is a passthrough with unstated dimensions

**Test**: `WordOpenXmlImageReader_Read_Png_YieldsPassthroughWithNullDimensions`

Proves the reader returns one image whose transform hint is `Passthrough`, whose `WidthPx` and
`HeightPx` are `null`, whose media type is `image/png`, and whose bytes are byte-identical to
`DocxFixtures.PngBytes()`. Evidence for
`DocDownWord-OpenXml-WordOpenXmlImageReader-YieldsPassthroughImages` and
`DocDownWord-OpenXml-WordOpenXmlImageReader-RecordsPassthroughProvenance`.

#### An EMF image reports a vector media type

**Test**: `WordOpenXmlImageReader_Read_Emf_ReportsVectorMediaType`

Proves the reader returns one image whose media type contains `emf` case-insensitively. Evidence
for `DocDownWord-OpenXml-WordOpenXmlImageReader-ReportsVectorMediaType`.
