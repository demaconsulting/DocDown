## PowerPointContentEmitter

![DocDown.PowerPoint Structure](DocDownPowerPointView.svg)

### Purpose

`PowerPointContentEmitter` is the single emission path: it turns a read `PowerPointDeckModel` into the
output contract. Its single responsibility is that every output the model implies — the per-slide content
carrying title, text, and speaker notes, the inline images, the content outline, the diagnostics, and the
gaps — is produced in one place, driven only by the model and never by how the model was read. The COM
backend reuses it by delegation, so a deck's content reads identically whether or not it was rendered.

### Data Model

`PowerPointContentEmitter` is an `internal static class`. It holds no state. Named constants pin the ledger
target paths (`content.md`, `images/`) and the empty image-path map used when images are suppressed.

### Key Methods

- **`ValueTask<bool> EmitAsync(IExtractionSink sink, ExtractionOptions options, PowerPointDeckModel model,
  CancellationToken)`** — the whole emission. For an empty deck it reports the `PPTX0001` diagnostic and a
  counted `Text` gap, writes an empty content document, and returns degraded. Otherwise it writes the
  embedded images first (so each slide can link its pictures inline), writes the per-slide content, reports
  the document info and — when captured — the deck metadata, accounts for the speaker notes, reports the
  image gaps and caveats, reports the charts-not-read gap when the deck embeds charts, and reports the
  content outline. Returns whether any gap was reported. Preconditions: all arguments non-null.
- **`WriteContentAsync`** (private) — writes the deck as one content flow or, in per-part mode, one part
  per slide under `parts/`, each slide carrying its title heading, body text, and speaker notes, with the
  inline image links resolved from the sink's written-path map.
- **`ReportChartsNotExtracted`** (private) — reports the `PPTX0005` diagnostic and a counted `Text` gap
  naming how many charts the deck embeds whose plotted data this backend does not read, with a remedy
  pointing at the workbook route the Excel backend reads in full.
- **`ReportImages`** (private) — reports the vector-metafile caveat (`PPTX0003`, informational), a
  size-skip gap, and an unhonored force-PNG gap; a deck that embeds no images reports nothing here.
- **`ReportContentFeatures`** (private) — reports the outline counts (slides, slide titles, sets of
  speaker notes, inline images) from the model; Core drops any zero count.

### The speaker-notes accounting (forward-trace finding)

`EmitAsync` counts the slides carrying speaker notes. When that count is zero, the shipped code reports an
**informational** `PPTX0002` diagnostic (severity `Info`) stating the deck carries no notes, and does not
degrade the run. The PowerPoint statement of intent requires the absence of notes to be a **counted gap**
with a reason, so a reader can tell "this deck has no speaker notes" from "notes were not looked for". The
unit requirement `DocDownPowerPoint-Markdown-PowerPointContentEmitter-ReportsSpeakerNotesAbsenceGap` is
written to that intent; the implementation diverges, and the divergence is recorded as a finding in the
developer report rather than back-written to match the code.

### Error Handling

Null arguments are rejected with `ArgumentNullException`. Cancellation is observed and propagates as
`OperationCanceledException`. No other error condition arises here: the emitter writes only through the sink
and reads only the model, so an adverse deck was already turned into a structured failure upstream in the
reader or Core.

### Dependencies

- **DocDown.Core** — `IExtractionSink`, `ExtractionOptions`, `ContentSplitMode`, `ImageOutputMode`,
  `ExtractionDiagnostic`, `ExtractionGap`, `GapKind`, `GapScope`, `DiagnosticSeverity`, `DocumentInfo`,
  `ContentFeature`, `EmbeddedImageWriter`.
- **PowerPointDeckModel** — the read model it renders. See *PowerPointOpenXmlReader Design*.
- **PowerPointDiagnosticCodes** — the `PPTX` diagnostic codes it reports.

### Callers

`PowerPointOpenXmlExtractor.ExtractAsync` calls `EmitAsync` after the reader produces the model; the COM
backend reaches it through the same delegated managed extraction. Nothing else calls it.
