### ContentWriter

![Output Structure](OutputView.svg)

#### Purpose

`ContentWriter` finalizes `content.md` and, when the layout calls for it, the per-part files under
`parts/`, from the content the sink buffered during extraction. Its single responsibility is to decide
the *shape* of the content document without inventing content of its own.

#### Data Model

`ContentWriter` is a `static class` holding one constant, `ContentFileName` (`content.md`), the fixed
entry-point name in the output layout. It returns `ContentWriteResult`, which records the content
path, whether any text was present, the total character count, and the relative paths of any part
files written.

#### Key Methods

- **`WriteAsync(ExtractionSink sink, ContentSplitMode mode, string? documentTitle,
  CancellationToken)`** — chooses one of three layouts:
  - single-flow `content.md` when no parts were buffered;
  - concatenated `content.md` for `ContentSplitMode.Single` with parts;
  - index `content.md` plus part files for `Auto` or `PerPart` with parts.
- **`WriteSingleFlowAsync`** — writes the buffered markdown verbatim, preserving page markers.
- **`WriteConcatenatedAsync`** — renders buffered parts into one document under headings.
- **`WriteIndexAsync`** — writes the part files and an index linking to them.

#### Error Handling

`WriteAsync` throws `ArgumentNullException` for a null sink. All text writes go through
`ScratchFolder.WriteTextAsync`, so path validation and deterministic line ending behavior remain
centralized.

A whitespace-only flow still writes `content.md`, but `ContentWriteResult.ContentPresent` is false so
later summary and manifest serialization can state honestly that no textual content was produced.

#### Dependencies

- **`ExtractionSink`** — buffered content and parts plus the scratch-folder gate.
- **`ScratchFolder`** — deterministic contained text writes.
- **`ContentSplitMode`** — caller-controlled layout policy.

#### Callers

`DocDownEngine` calls `ContentWriter` before `MetadataWriter`, `ManifestWriter`, and `SummaryWriter`.
