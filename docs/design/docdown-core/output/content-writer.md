### ContentWriter

![Output Structure](OutputView.svg)

#### Purpose

`ContentWriter` finalizes `content.md` and, when the layout calls for it, the per-part files under
`parts/`, from the content the sink buffered during extraction. Its single responsibility is to decide
the *shape* of the content document — `content.md` is always the single entry point, but its form
depends on the content and the requested split mode. The writer invents no content: page markers and
part titles originate in the extractor's output.

#### Data Model

`ContentWriter` is a `static class` holding one constant, `ContentFileName` (`content.md`), the fixed
name that is part of the invariant output contract. It has no state; it reads the sink's buffered
content and parts and returns a `ContentWriteResult` (the content path, whether text is present, the
character count, and the relative paths of any part files written).

#### The content.md / parts decision

The shape of `content.md` (and whether a `parts/` folder is produced) is decided as follows:

- **Single-flow document** (no buffered parts) — `content.md` holds the full buffered markdown
  verbatim, with the extractor's `<!-- docdown:page N -->` markers preserved; no `parts/` folder.
- **Multi-part document, `ContentSplitMode.Auto` or `PerPart`** — `content.md` is an **index**: a
  `# {title}` heading, a part-count line, and a document-ordered markdown list linking each part;
  parts live under `parts/{ordinal:D4}-{kind}-{slug}.md`.
- **Multi-part document, `ContentSplitMode.Single`** — `content.md` holds the parts concatenated under
  `## {part title}` headings; no `parts/` folder.

Presence of text is judged on non-whitespace content, so an empty flow is honestly reported as absent
text (which the engine turns into a `DD0101` gap). Part file paths were allocated Core-side by
`ExtractionSink`, so the index links resolve on disk; a part with no title still receives a
deterministic heading (for example `Sheet 2`).

#### Key Methods

- **`static ValueTask<ContentWriteResult> WriteAsync(ExtractionSink sink, ContentSplitMode mode,
  string? documentTitle, CancellationToken)`** — the entry point.
  - *Precondition*: `sink` non-null.
  - *Algorithm*: when no parts were buffered, write the single-flow document (preserving buffered
    content); for `ContentSplitMode.Single` with parts, write the concatenated document; otherwise write
    the index plus one file per part. Always writes `content.md`.
  - *Postcondition*: returns a `ContentWriteResult` describing exactly what was written.

Private helpers `WriteSingleFlowAsync`, `WriteConcatenatedAsync`, and `WriteIndexAsync` implement the
three layouts; `ResolveTitle` falls through the caller title, the extractor's reported document title,
and a generic `Document` label so the index always has a heading; `HeadingFor` produces a part's
display heading.

#### Error Handling

`WriteAsync` throws `ArgumentNullException` for a null sink. All writes go through
`ScratchFolder.WriteTextAsync`, which applies the containment and component defenses and the
deterministic `\n`/no-BOM encoding; a path fault surfaces as `ScratchFolderException`. The writer holds
no state and performs no recovery of its own — it either writes the chosen layout or propagates the
scratch-folder refusal.

#### Dependencies

- **ExtractionSink** — the source of buffered content and parts, and (via its `Folder`) the write gate.
  See *ExtractionSink Design*.
- **ScratchFolder** — the containment gate and text writer. See *ScratchFolder Design*.
- **ContentSplitMode** (supporting type; see *Extraction Subsystem Design*).

#### Callers

`DocDownEngine` calls `WriteAsync` during the serialization step, before `ManifestWriter` and
`SummaryWriter`, so the ledger and summary reflect exactly what the content writer produced. See
*DocDownEngine Design*.
