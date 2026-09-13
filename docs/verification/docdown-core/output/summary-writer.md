### SummaryWriter Verification Design

This document describes the unit-level verification strategy for `SummaryWriter`.

#### Verification Approach

`SummaryWriter` is verified through direct unit tests in `SummaryWriterTests.cs` and text-level
golden tests in `SummaryWriterGoldenTests.cs`. The tests drive the real writer over a real
`ScratchFolder` because the output text itself is the artifact under review.

#### Test Environment

- **Framework**: xUnit v3 under the .NET SDK
- **Targets**: net8.0, net9.0, and net10.0
- **Filesystem**: a real per-test `TempScratch` folder
- **Inputs**: real report, sink, and content values assembled per scenario

#### Acceptance Criteria

Per IEC 62304 §5.5.2, `SummaryWriter` passes when it emits the fixed reduced section set, states the
absolute scratch path, names the selected backend, reports runtime and environment facts, lists notes
under `Could not read`, reproduces unreadable failure prose verbatim, remains byte-identical for the
same content, opens with a format-sensitive gist when one is justified, outlines counted content
features including looked-for zero counts, aggregates image reporting, omits a gist for an unknown
format, and collapses unused available candidates into one counted summary line.

#### Test Scenarios

##### Every mandatory section is present

**Test**: `SummaryWriter_WriteAsync_CleanRun_ContainsEveryMandatorySection`

##### The header states the absolute scratch path

**Test**: `SummaryWriter_WriteAsync_HeaderBlock_ContainsAbsoluteScratchPath`

##### A successful run names the selected backend

**Test**: `SummaryWriter_WriteAsync_SuccessfulRun_NamesSelectedBackend`

##### Runtime and reported environment facts are shown

**Test**: `SummaryWriter_WriteAsync_WithEnvironmentFacts_ReportsRuntimeAndFacts`

##### Notes are listed under Could not read

**Test**: `SummaryWriter_WriteAsync_ProducedRun_ListsNotesUnderCouldNotRead`

##### An unreadable failure is reproduced verbatim

**Test**: `SummaryWriter_WriteAsync_UnreadableRun_ReproducesFailureExplanationVerbatim`

##### Summary text is deterministic

**Test**: `SummaryWriter_WriteAsync_SameContentTwice_ProducesByteIdenticalSummary`

##### Authored metadata is inlined correctly

**Tests**: `SummaryWriter_WriteAsync_PdfShapedMetadata_InlinesHumanAuthorNotApplication`,
`SummaryWriter_WriteAsync_OpcShapedMetadata_FallsBackToCreatorAsAuthor`

##### Content features drive the gist and inventory line

**Tests**: `SummaryWriter_WriteAsync_WithContentFeatures_OpensWithGistAndOutlinesContent`,
`SummaryWriter_WriteAsync_LookedForZeroFeature_OutlinesTheZero`

##### Unknown format omits the gist instead of guessing

**Test**: `SummaryWriter_WriteAsync_UnknownFormat_OmitsGistRatherThanGuessing`

##### Images are summarized in aggregate

**Test**: `SummaryWriter_WriteAsync_WithImages_SummarizesInAggregateAndExplainsTemplateImages`

##### Unused available candidates collapse into one counted line

**Test**: `SummaryWriter_WriteAsync_UnusedAvailableCandidates_CollapsesThemIntoOneCountedLine`

##### Representative rendered output matches the committed goldens

**Tests**: `SummaryWriter_Golden_RenderedPagesNoImages_MatchesCommittedGolden`,
`SummaryWriter_Golden_EmbeddedImages_MatchesCommittedGolden`,
`SummaryWriter_Golden_RenderingNotAvailableNote_MatchesCommittedGolden`,
`SummaryWriter_Golden_UnreadableDocxNoBackend_MatchesCommittedGolden`
