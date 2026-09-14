## Markdown Subsystem Verification Design

This document describes the verification strategy for the Markdown subsystem, the reader-neutral
projection of a deck model onto the output contract.

### Verification Approach

The Markdown subsystem is verified through unit tests in
`Markdown/PowerPointContentEmitterTests.cs` and complementary system-level scenarios in
`DocDownPowerPointTests.cs`, all in `DemaConsulting.DocDown.Office.Tests`.

The emitter is exercised from hand-built deck models through a recording sink, with no deck and no
Open XML SDK behind them, because its contract is the mapping from the model to the output: the
per-slide title, text, and notes; the inline image links and their suppression; the content
inventory; the empty-deck behavior; the vector-image passthrough; and the single content flow
split. Building the model by hand is what lets each mapping decision be proved in isolation.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: hand-built `PowerPointDeckModel` instances with synthetic slides, titles, notes, and
  image references; on-disk link-resolution scenarios use a per-test `TempScratch` folder
- **Mocking**: a recording sink captures the emitter's content, parts, images, notes, and features;
  the emitter itself is the real unit
- **Isolation**: each test builds its own model and, where needed, its own scratch folder

### Acceptance Criteria

Per IEC 62304 §5.6.2, a Markdown subsystem test run passes when the emitter writes each slide's title,
body text, and speaker notes; links a slide's images inline only when a path was returned; writes an
EMF or WMF image unchanged with no PowerPoint-specific note; reports the content outline including the
speaker-notes count stated even when it is zero; writes empty content plus zero-count inventory for an
empty deck; and writes the deck as a single flow or one part per slide with links resolving on disk.
Any silent content loss, dangling link, or wrong inventory count is a failure.

### Test Scenarios

The per-unit scenarios are given in the `PowerPointContentEmitter` unit verification chapter, each
naming the requirement it evidences.
