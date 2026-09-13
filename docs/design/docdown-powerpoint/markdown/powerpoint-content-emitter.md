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
  speaker notes, inline images) from the model. The speaker-notes count is declared **looked-for**, so
  it is stated even at zero; the remaining counts are dropped by Core when zero, keeping the outline
  compact.

### The speaker-notes accounting

`EmitAsync` counts the slides carrying speaker notes and hands that count to `ReportContentFeatures`.
A deck that carries none reads `0 sets of speaker notes` in the summary's content outline and appears
with `"count": 0` in the manifest's `contentFeatures`, so a reader can tell "every notes slide was read
and there are none" from "notes are not something DocDown counts". No gap, no diagnostic, and no
degradation accompany it: a notes-less deck cost the extraction nothing, and `Degraded` describes only
what DocDown could not do. This supersedes the earlier design, in which the absence was an
informational `PPTX0002` diagnostic and the unit requirement
`DocDownPowerPoint-Markdown-PowerPointContentEmitter-ReportsSpeakerNotesAbsenceGap` called for a counted
gap; that requirement is superseded by
`DocDownPowerPoint-Markdown-PowerPointContentEmitter-ReportsSpeakerNotesAbsenceInInventory`, and
`PPTX0002` is retired and permanently reserved.

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
