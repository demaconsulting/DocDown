### SummaryWriter

![Output Structure](OutputView.svg)

#### Purpose

`SummaryWriter` serializes `summary.txt`, the human- and LLM-readable account of what was extracted,
where it was written, and which attempted steps could not be completed.

#### Data Model

`SummaryWriter` is a `static class` with no mutable state. It reads the engine's `ExtractionReport`,
`ContentWriteResult`, and the sink's recorded content features, notes, images, pages, parts,
document metadata, and environment facts, then renders one plain-text document.

#### On-disk format

`summary.txt` is written as UTF-8 without a byte-order mark and uses `
` line endings always. Its
sections appear in this fixed order:

1. title and one-line gist;
2. header (`Scratch folder`, `Source document`, `Detected format`, `Extracted (UTC)`, `Status`);
3. optional `Failure` for unreadable runs;
4. `Backend`;
5. `Environment`;
6. `Document metadata`;
7. `Layout`;
8. `What WAS extracted`;
9. `Could not read`.

The summary keeps to aggregate image reporting, points to `manifest.json` for per-image detail,
describes the content through the inventory line, and lists only plain notes for attempted
extraction steps that could not be completed.

#### Key Methods

- **`WriteAsync(...)`** — public entry point that appends each fixed section and writes the file.
- **`AppendTitleAndHeader`** — emits the title banner, gist, and header block.
- **`AppendFailure`** — reproduces `ExtractionFailure.Explanation` verbatim for unreadable runs.
- **`AppendBackend`** — names the selected extractor or states that no backend was selected.
- **`AppendEnvironment`** — renders the runtime plus grouped environment facts, collapsing unused
  available candidates into a counted line pointing at `manifest.json`.
- **`AppendDocumentMetadata`** — inlines author and modified date, then points at `metadata.json`.
- **`AppendLayout`** — lists the fixed artifact names and resource-folder presence facts.
- **`AppendWhatWasExtracted`** — reports content character count and inventory, aggregate image
  summary, rendered page count, and part count.
- **`AppendNotes`** — renders the `Could not read` section from recorded `ExtractionNote` values.

#### Error Handling

`WriteAsync` throws only for null required collaborators. It performs no inference beyond the direct
format gist and does not fabricate content when data is absent: an unknown format omits the gist and
an empty note list is stated plainly.

#### Dependencies

- **`ScratchFolder`** — deterministic contained text write.
- **`ExtractionReport`**, **`ExtractionOutcome`**, **`ExtractionFailure`**,
  **`ContentWriteResult`**, **`DocumentMetadata`**, **`ContentFeature`**, **`RecordedImage`**, and
  **`ExtractionNote`** — supporting data.

#### Callers

`DocDownEngine` calls `SummaryWriter` last so the human-readable record reflects the final extraction
state.
