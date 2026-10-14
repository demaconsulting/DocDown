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

- **`WriteAsync(ExtractionSink sink, string? documentTitle, CancellationToken)`** — chooses one of
  two layouts, driven by the document rather than by the caller:
  - single-flow `content.md` when no parts were buffered;
  - index `content.md` plus part files under `parts/` when the backend offered parts.
- **`WriteSingleFlowAsync`** — writes the buffered markdown verbatim, preserving page markers.
- **`WriteIndexAsync`** — writes each part file, then an index linking to them.
  - *Part link rewrite*: an extractor emits sink-allocated, root-relative resource links
    (`images/…`, `pages/…`) because path allocation belongs to the sink. Those resolve from
    `content.md` but not from a part file one folder deeper under `parts/`, so each part's markdown is
    passed through the internal `PartResourceLinkRewriter` before it is written. The rewrite is a pure
    function that prefixes only link targets that begin with a sink resource folder — for both the
    image form `![alt](…)` and the plain-link form `[text](…)` — with one `../` per directory level
    above the part file. External targets (`http(s):`, `mailto:`), `#anchor` targets, and targets that
    are already relative pass through verbatim, so the rewrite is idempotent. The single-flow layout
    needs no rewrite because `content.md` sits at the scratch root.
  - *Index format*: a `# {title}` heading, a blank line, a `N part` or `N parts` count line, a blank
    line, then one `- [{heading}]({part path})` bullet per part in document order.
  - *Title fallback*: the document title passed by the engine, else the title the backend reported
    through `ReportDocumentInfo`, else the literal `Document`, so the heading is never empty.
  - *Heading fallback*: a part's own title when it has one, else its capitalized kind and ordinal
    (for example `Sheet 2`), with `Part` used when the kind is empty.

#### Error Handling

`WriteAsync` throws `ArgumentNullException` for a null sink. All text writes go through
`ScratchFolder.WriteTextAsync`, so path validation and deterministic line ending behavior remain
centralized.

A whitespace-only flow still writes `content.md`, but `ContentWriteResult.ContentPresent` is false so
later summary and manifest serialization can state honestly that no textual content was produced. An
index layout always reports content present, because the index itself and its parts are content.

#### Dependencies

- **`ExtractionSink`** — buffered content and parts plus the scratch-folder gate.
- **`ScratchFolder`** — deterministic contained text writes.

#### Callers

`DocDownEngine` calls `ContentWriter` first among the artifact writers, then `ReviewCommentsWriter`
(which relies on the sink's recorded review comments rather than on `ContentWriter`'s output), then
`ManifestWriter`, `MetadataWriter`, and `SummaryWriter`.
