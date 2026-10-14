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
type truthfully. The four `Resolve_*` scenarios drive `Resolve()` directly: one confirms null arguments are rejected,
one that a missing or non-image relationship resolves to nothing, one that an image scoped to a header
part resolves against that part and yields the fixture PNG bytes, and one that naming candidates are
ranked with alt text gated. The passthrough and vector scenarios supply no text candidates.

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
`image/png` media type, and bytes byte-identical to the fixture's retained source PNG; when an
EMF image part resolves with a media type identifying the vector metafile truthfully; when null arguments are
rejected; when a missing or non-image relationship resolves to no reference; when a header-scoped image resolves
against its header part; and when naming candidates are ranked and alt text is asserted only for an authored
description.

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

#### Null arguments are rejected

**Test**: `WordOpenXmlImageReader_Resolve_NullArguments_Throw`

Proves a null container or a null candidate list makes `Resolve()` throw `ArgumentNullException`.
Evidence for `DocDownWord-ArgumentAndCancellationHandling`.

#### A missing or non-image relationship resolves to nothing

**Test**: `WordOpenXmlImageReader_Resolve_MissingOrNonImageRelationship_ReturnsNull`

Proves a null id, an empty id, and an id naming a header part (not an image) each yield no
reference. Evidence for `DocDownWord-OpenXml-WordOpenXmlImageReader-IgnoresInvalidRelationships`
and `DocDownWord-ImageRelationshipValidation`.

#### A header-scoped image resolves against the header part

**Test**: `WordOpenXmlImageReader_Resolve_HeaderScopedImage_ResolvesAgainstHeaderPart`

Proves an image related to a header part resolves through that part and carries the fixture PNG
bytes. Evidence for `DocDownWord-OpenXml-WordOpenXmlImageReader-ResolvesPartScopedImages` and
`DocDownWord-ImageResolution`.

#### Naming candidates are ranked and alt text is gated

**Test**: `WordOpenXmlImageReader_Resolve_Candidates_RankAndGateAltText`

Proves a description candidate becomes the alt text and description, a heading candidate names the file
but leaves the alt text null with a `heading` description source, and a call with no candidates falls
back to a media-derived name with no description and the part URI as the source reference. Evidence for
`DocDownWord-OpenXml-WordOpenXmlImageReader-RanksNamingCandidates` and
`DocDownWord-ImageNamingCandidateRanking`.
