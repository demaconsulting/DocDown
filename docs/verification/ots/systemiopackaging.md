## System.IO.Packaging Verification

This document provides the verification evidence for the System.IO.Packaging OTS software item.
Requirements for this OTS item are defined in the System.IO.Packaging OTS Software Requirements
document.

### Required Functionality

System.IO.Packaging is the Open Packaging Conventions (OPC) container implementation the Open XML
SDK sits on: it is the Zip-based package reader that opens a `.docx` and exposes its parts and
relationships. It must open a WordprocessingML package without native binaries and expose the parts
and relationships the SDK reads to recover a document's structure.

### Verification Approach

**System.IO.Packaging is verified by transitive evidence from the `DocDown.Word` test suite.** Per
the software-items standard, a dedicated OTS test project is required only *if no other
verification evidence is available*. That is not the case: System.IO.Packaging is a fully managed
runtime library on the critical path of every `DocDown.Word` extraction, reached transitively
through the Open XML SDK. The extraction and reader tests named in the scenarios below each open
a real generated package end to end, on three target frameworks, in every CI matrix combination.

The evidence is limited to the two tests named below. The claim is transitive, not
comprehensive: each scenario proves the package reader opens a `.docx` container and exposes its
parts and relationships to the SDK, not that every part of the packaging API is exercised.

A dedicated `test/OtsSoftwareTests/` project would therefore re-test the same read path the
extraction suite already exercises against the packages this repository actually has to handle.
The extraction suite answers the question directly, so no such project is created.

### Test Environment

The evidence is produced by the standard `DocDown.Word` test run: xUnit v3 under the .NET SDK,
targeting net8.0, net9.0, and net10.0, across the CI operating-system matrix. Every fixture is a
WordprocessingML package generated at test time by the Open XML SDK's writer, so the evidence
depends on no committed binary and no network access.

### Test Scenarios

#### A WordprocessingML package is opened and its parts are exposed

**Tests**: `DocDownWord_Extract_GeneratedDocx_ProducesContractLayout`,
`WordOpenXmlReader_Read_HeadingsListsAndTables_ProducesStructuredBlocks`

The first proves the package reader opens a generated `.docx` — an OPC package of XML parts joined
by relationships — end to end through the engine and produces the full four-artifact layout with
a real GFM table and a Document Control heading. The second proves the SDK resolves the package's
main-document relationship and reads its part into the backend-neutral model: a heading block, at
least one bulleted list item, and a Table block whose model has its first-row-is-header flag set
and three rows. Each requires the packaging layer to open the container and expose the parts and
relationships the SDK then reads; neither passes if the package cannot be opened. Evidence for
`DocDown-OTS-SystemIoPackaging`.
