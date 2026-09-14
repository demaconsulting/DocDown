## VisioImageReader Verification Design

This document describes the unit-level verification strategy for `VisioImageReader`, the embedded-image
resolver.

### Verification Approach

`VisioImageReader` is verified through unit tests in `OpenXml/VisioImageReaderTests.cs` in
`DemaConsulting.DocDown.Office.Tests`, reading packages built at test time with page, master, and thumbnail
image relationships and asserting the resolved images and their provenance. The image reader's contract —
what reaches the model and how each image is associated — is proved directly here and reinforced by the
emitter and integration tests that write and link the images.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: Visio Open Packaging drawings synthesized in memory with a page image plus a thumbnail, and a
  master image, built by `VisioPackageBuilder`
- **Mocking**: none; the image reader is pure over the package
- **Isolation**: each test builds its own package

### Acceptance Criteria

Per IEC 62304 §5.5.2, a `VisioImageReader` unit test run passes when the reader yields each distinct embedded
image once with its bytes unchanged, records every page that references an image, flags an image reached only
through a master as template furniture without a page, and excludes the package thumbnail. Any duplicated
image, missing page association, fabricated page for template furniture, or thumbnail reported as content is
a failure.

### Test Scenarios

#### A page image is yielded and the thumbnail is excluded

**Test**: `VisioImageReader_Collect_PageImageAndThumbnail_YieldsImageOnlyWithPage`

Proves a page's embedded image is yielded once with its page association recorded, its bytes unchanged, and
the package thumbnail excluded. Evidence for `DocDownVisio-OpenXml-VisioImageReader-YieldsEmbeddedImages`,
`DocDownVisio-OpenXml-VisioImageReader-RecordsPageAssociation`, and
`DocDownVisio-OpenXml-VisioImageReader-ExcludesThumbnail`.

#### A master image is flagged as template furniture

**Test**: `VisioImageReader_Collect_MasterImage_FlagsTemplateWithoutPage`

Proves an image reached only through a master is flagged as template-referenced rather than given a
fabricated page. Evidence for `DocDownVisio-OpenXml-VisioImageReader-FlagsTemplateFurniture`.
