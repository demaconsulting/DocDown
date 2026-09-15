## System.IO.Packaging Verification

This document provides the verification evidence for the System.IO.Packaging OTS software item.
Requirements for this OTS item are defined in the System.IO.Packaging OTS Software Requirements
document.

### Required Functionality

System.IO.Packaging is the Open Packaging Conventions (OPC) container implementation: the Zip-based
package reader that opens an Office package and exposes its parts and relationships. DocDown calls it
directly, not only through the Open XML SDK — the Visio backend reads a `.vsdx` with `Package.Open`
and resolves each part it needs by relationship, because the SDK does not read Visio drawings. It
must open an OPC package without native binaries and expose those parts and relationships.

### Verification Approach

**System.IO.Packaging is verified by direct evidence from the Visio package-reader tests, and by
transitive evidence from the Word suite.** Per the software-items standard, a dedicated OTS test
project is required only *if no other verification evidence is available*. That is not the case:
`VisioPackageReader` names the packaging API in its own source, so the Visio reader tests exercise
`Package.Open`, `PackagePart` lookup, and relationship resolution directly, on three target
frameworks, in every CI matrix combination. The Word path reaches the same library transitively
through the SDK.

The evidence is limited to the tests named below. The claim is bounded, not comprehensive: each
scenario proves the package reader opens a container and exposes the parts and relationships the
caller asks for, not that every part of the packaging API is exercised.

A dedicated `test/OtsSoftwareTests/` project would therefore re-test the same read path the Visio
and Word suites already exercise against the packages this repository actually has to handle. Those
suites answer the question directly, so no such project is created.

### Test Environment

The evidence is produced by the standard `DocDown.Office` test run: xUnit v3 under the .NET SDK,
targeting net8.0, net9.0, and net10.0, across the CI operating-system matrix. Every fixture is an
OPC package generated at test time, so the evidence depends on no network access.

### Test Scenarios

#### An OPC package is opened directly and its parts and relationships are resolved

**Tests**: `VisioPackageReader_Read_WashSystem_SurfacesPageName`,
`VisioPackageReader_Read_TwoPages_ReturnsPagesInOrder`

`VisioPackageReader` opens the drawing with `Package.Open`, locates the pages part through its
relationship, and reads each page part. The first proves a page's name is recovered from the opened
package; the second proves two page parts are resolved and returned in document order. Neither
passes unless the container opens and its parts and relationships resolve, and neither involves the
Open XML SDK at any point — this is DocDown's own call into the packaging API. Evidence for
`DocDown-OTS-SystemIoPackaging`.

#### A WordprocessingML package is opened through the SDK

**Test**: `DocDownWord_Extract_GeneratedDocx_ProducesContractLayout`

Proves the same packaging layer serves the SDK path: a generated `.docx` — an OPC package of XML
parts joined by relationships — is opened end to end through the engine and produces the full
four-artifact layout with a real GFM table and a Document Control heading. This evidence is
transitive, since the SDK performs the packaging calls here rather than DocDown. Evidence for
`DocDown-OTS-SystemIoPackaging`.
