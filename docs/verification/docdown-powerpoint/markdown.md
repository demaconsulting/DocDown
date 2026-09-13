## Markdown Subsystem Verification Design

This document describes the verification strategy for the Markdown subsystem, the reader-neutral projection
of a deck model onto the output contract: the content emitter and the diagnostic-code contract.

### Verification Approach

The Markdown subsystem is verified through unit tests in `Markdown/PowerPointContentEmitterTests.cs` and
`Markdown/PowerPointDiagnosticCodesTests.cs`, plus the system-level scenarios in `DocDownPowerPointTests.cs`,
all in `DemaConsulting.DocDown.PowerPoint.Tests`.

The emitter is exercised from **hand-built deck models** through a recording sink, with no deck and no Open
XML SDK behind them, because its contract is the mapping from the model to the output — the per-slide title,
text, and notes, the inline image links, the vector-image caveat, the charts-not-read gap, the empty-deck
gap, the content outline, and the single-flow-versus-per-part split. Building the model by hand is what lets
each mapping decision be proved in isolation. The diagnostic-code constants are pinned by a table test that
treats the contiguous `PPTX` numbering as a contract.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: hand-built `PowerPointDeckModel` instances with synthetic slides, titles, notes, and image
  references; on-disk resolution scenarios use a per-test `TempScratch` folder
- **Mocking**: a recording sink captures the emitter's content, parts, images, diagnostics, gaps, and
  features; the emitter itself is the real unit
- **Isolation**: each test builds its own model and, where needed, its own scratch folder

### Acceptance Criteria

Per IEC 62304 §5.6.2, a Markdown subsystem test run passes when the emitter writes each slide's title, body
text, and speaker notes; links a slide's images inline only when a path was returned; writes a vector image
with an informational caveat and no gap; reports a counted gap for charts the deck embeds and none for a
chart-free deck; degrades with a counted gap for an empty deck; reports the content outline including the
speaker-notes count; and writes the deck as a single flow or one part per slide with links resolving on
disk. The diagnostic-code set must be exact and contiguous.

**Recorded divergence.** The intent requires the whole-deck absence of speaker notes to be a **counted gap**;
the shipped emitter reports it as an **informational** `PPTX0002` diagnostic (proved by
`PowerPointContentEmitter_Emit_NoNotes_ReportsInfoDiagnosticNotGap`). Requirement
`DocDownPowerPoint-Markdown-PowerPointContentEmitter-ReportsSpeakerNotesAbsenceGap` is written to the intent
and is not satisfied by the current code; see the developer report.

### Test Scenarios

The per-unit scenarios are given in the `PowerPointContentEmitter` unit verification chapter, each naming the
requirement it evidences. The diagnostic-code constants are pinned by
`PowerPointDiagnosticCodes_Constants_MatchContract` and `PowerPointDiagnosticCodes_Set_IsExactAndContiguous`,
which verify the supporting `PowerPointDiagnosticCodes` type documented in the subsystem design.
