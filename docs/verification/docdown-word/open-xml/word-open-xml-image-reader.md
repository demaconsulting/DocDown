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
against the true stored bytes. The passthrough scenario asserts **byte identity against the
fixture's retained source PNG** (`DocxFixtures.PngBytes()`), so an image reader that silently
re-encoded while still reporting a passthrough would fail on the bytes, not on the label. The
vector scenario asserts the media type carries `emf` (case-insensitive), so the caller has the
information it needs to raise the readability caveat.

The pixel dimensions are asserted as `null` because the stored extent in a `.docx` is a display
size in EMUs, not a pixel count; reporting the display size as pixel dimensions would be false. The
assertion is what turns that decision into a machine-checked contract rather than a documentation
claim.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: WordprocessingML documents generated at test time, one carrying a PNG part and one
  carrying an EMF part, both from `TestData/DocxFixtures.cs`
- **Filesystem**: none; each fixture is opened from a `MemoryStream`
- **Mocking**: none; the SDK is exercised against real documents
- **Isolation**: each test opens its own document

### Acceptance Criteria

Per IEC 62304 §5.5.2, a `WordOpenXmlImageReader` unit test run passes when a PNG image part is
yielded once with the passthrough transform, unstated width and height, the `image/png` media type,
and bytes byte-identical to the fixture's retained source PNG; and when an EMF image part is
yielded with a media type identifying the vector metafile so the extractor can add its readability
caveat. Any image duplicated, any pixel dimension reported for a stored EMU extent, any
re-encoding under a passthrough label, or a media type that does not identify a vector metafile is
a failure.

### Test Scenarios

#### A PNG image is a passthrough with unstated dimensions

**Test**: `WordOpenXmlImageReader_Read_Png_YieldsPassthroughWithNullDimensions`

Proves the reader returns one image whose transform hint is `Passthrough`, whose `WidthPx` and
`HeightPx` are `null` because the stored extent is a display size in EMUs, whose media type is
`image/png`, and whose bytes are byte-identical to `DocxFixtures.PngBytes()`. The byte comparison
is the load-bearing part: without it the passthrough label would be a tautology. Evidence for
`DocDownWord-OpenXml-WordOpenXmlImageReader-YieldsPassthroughImages` and
`DocDownWord-OpenXml-WordOpenXmlImageReader-RecordsPassthroughProvenance`.

#### An EMF image reports a vector media type

**Test**: `WordOpenXmlImageReader_Read_Emf_ReportsVectorMediaType`

Proves the reader returns one image whose media type contains `emf` (case-insensitive), so the
extractor has the information it needs to raise the readability caveat honestly rather than
silently writing a file whose format many viewers cannot render. Evidence for
`DocDownWord-OpenXml-WordOpenXmlImageReader-ReportsVectorMediaType`.
