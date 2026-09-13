### ContentWriter Verification Design

This document describes the unit-level verification strategy for `ContentWriter`, which finalizes the
Markdown content: a single flow with preserved page markers, or an index over split parts, and honestly
reports empty content.

#### Verification Approach

`ContentWriter` is verified in isolation through unit tests in `ContentWriterTests.cs` under the `Output`
folder of `DemaConsulting.DocDown.Core.Tests`, with method names beginning with `ContentWriter_`.

Nothing is mocked. The writer's documented source of buffered content and parts is `ExtractionSink`, so
tests drive a **real** `ExtractionSink` over a real `ScratchFolder` prepared on a per-test `TempScratch`
folder: the test buffers content or adds parts through the sink, then calls `ContentWriter.WriteAsync`
and reconciles the resulting `content.md`, `parts/` files, and returned result against disk. Using the
real sink rather than a stub is correct because the writer's contract is precisely to turn what the sink
buffered into the final on-disk layout; a mock sink would not evidence that the bytes and paths match.

#### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Filesystem**: a per-test `TempScratch` folder backs the real `ScratchFolder` and `ExtractionSink`
- **Mocking**: none; the real sink supplies buffered content and parts
- **Isolation**: each test owns its scratch folder; no shared state

#### Acceptance Criteria

Per IEC 62304 §5.5.2, a `ContentWriter` unit test run passes when a single buffered flow is written to
`content.md` verbatim; when a multi-part document produces an index plus per-part files; when the caller's
split mode selects a concatenated document or a part index; when backend page markers are preserved in a
single flow; and when an empty flow still writes the content file but reports it absent. Any altered
content, wrong layout for the split mode, lost page marker, or misreported presence is a failure.

#### Test Scenarios

##### A single flow is written verbatim

**Test**: `ContentWriter_WriteAsync_SingleBufferedFlow_WritesContentVerbatim`

Proves a single buffered flow is written to `content.md` exactly as the backend produced it, present and
with no part files. Evidence for `DocDownCore-Output-ContentWriter-SingleFlowDocument`.

##### Multiple parts produce an index and part files

**Test**: `ContentWriter_WriteAsync_MultipleParts_ProducesIndexAndPartFiles`

Proves a split document produces a navigable index linking to each ordered part file. Evidence for
`DocDownCore-Output-ContentWriter-PartIndex`.

##### The split mode is honored

**Tests**: `ContentWriter_WriteAsync_SingleMode_ConcatenatesPartsWithoutPartsFolder`,
`ContentWriter_WriteAsync_PerPartMode_ProducesIndexWithPartFiles`

Proves the caller's split mode is honored: single mode concatenates the parts into one document with no
parts folder, while per-part mode produces an index with per-part files. Evidence for
`DocDownCore-Output-ContentWriter-SplitModeHonored`.

##### Page markers are preserved

**Test**: `ContentWriter_WriteAsync_SingleFlowWithPageMarkers_PreservesMarkers`

Proves backend-supplied page markers are passed through untouched in a single-flow document, keeping the
page correspondence available. Evidence for `DocDownCore-Output-ContentWriter-PageMarkers`.

##### An empty flow is reported absent

**Test**: `ContentWriter_WriteAsync_EmptyFlow_WritesFileButReportsAbsent`

Proves an empty flow still writes the content file but reports the content absent, so an empty file is not
mistaken for a delivered document. Evidence for `DocDownCore-Output-ContentWriter-AbsenceExplained`.

##### A null sink is rejected

**Test**: `ContentWriter_WriteAsync_NullSink_ThrowsArgumentNullException`

Proves a null sink is rejected at entry with the documented exception. This is a defensive test with no
linked requirement.
