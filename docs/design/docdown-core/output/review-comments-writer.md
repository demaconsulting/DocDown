### ReviewCommentsWriter

![Output Structure](OutputView.svg)

#### Purpose

`ReviewCommentsWriter` finalizes `review-comments.md`, the reviewer commentary a document carries,
from the comments the sink recorded during extraction. Its single responsibility is to turn recorded
comments into a readable artifact — and, when there are none, to write nothing at all.

Reviewer commentary is editorial conversation *about* a document rather than the document's own
content, so it gets its own artifact instead of being interleaved into `content.md`. A consumer that
wants the document reads `content.md`; a consumer that wants the review reads this file; neither has
to filter the other out.

#### Data Model

`ReviewCommentsWriter` is a `static class` holding three constants: `ReviewCommentsFileName`
(`review-comments.md`), the fixed name in the output layout; `Heading` (`# Review comments`), so the
file is self-describing when read on its own; and `UnattributedAuthor` (`Unattributed`), the label
used for a comment the document records no author for.

`DocumentComment(string? Author, string Body, string Location)` is the record an extractor reports.
`Location` is a single, precomputed, human-readable display string rather than a structured address:
the in-scope formats locate a comment in genuinely different terms, so any shared structure would be
mostly-null fields that each consumer would have to re-render anyway. The backend that knows the
format composes the phrase once, in its own vocabulary, and Core carries it verbatim. The per-format
conventions are:

| Format | `Location` convention | Example |
| --- | --- | --- |
| Word | enclosing heading plus the commented-on words | `§Overview — "the system…"` |
| Excel | sheet and cell reference | `Sheet1!B7` |
| PowerPoint | slide ordinal | `Slide 4` |
| PDF | page ordinal | `Page 12` |

`ReviewCommentsWriteResult(string? Path, int Count)` is returned to the engine. `Path` is `null` and
`Count` is zero exactly when no file was written.

`Body` is the comment text *as the document records it*. Every backend reports it unaltered, and
`manifest.json` carries exactly that, so the machine-readable record holds the reviewer's words and
not one backend's rendering of them. The only place the text is transformed is on its way into this
markdown file, by `EscapeInline` below — which means the two artifacts deliberately differ, and
differ in a stated way.

#### Key Methods

- **`WriteAsync(ExtractionSink sink, CancellationToken)`** — returns an empty result without touching
  the filesystem when the sink recorded no comments; otherwise writes the heading followed by one
  entry per comment, in the order the extractor reported them, and returns the path and the count.
- **`AppendEntry`** — renders one comment as `- **Author** (Location): Body`. The author is
  emphasized so a reader can follow one reviewer's thread, and the location precedes the body so the
  entry reads as a statement about a place in the document. Every field passes through
  `EscapeInline` on the way in, because this is where markdown is written.
- **`EscapeInline`** — backslash-escapes the inline-structural set `` \ ` * _ [ ] < `` in the author,
  the location, and the flattened body. Escaping lives here, once, rather than in each backend: that
  keeps every extractor reporting the comment text *as the document records it*, which is what
  `manifest.json` promises its consumers, while still producing a `review-comments.md` a markdown
  parser reads as one entry per comment. A reviewer who wrote `*urgent*` gets those asterisks back
  rather than emphasis they never asked for. The set is deliberately minimal. Each entry's text sits
  mid-line, after `):`, where a `#`, a `-`, a `|`, or a trailing `.` cannot take effect as a
  heading, a list marker, a table separator, or an ordered-list number, so escaping them would
  litter the output with backslashes that mean nothing — which is precisely what made one backend's
  comment text read as `Please clarify the scope\.` before escaping moved here. What *can*
  restructure an entry from mid-line is emphasis, code spans, link syntax, and inline HTML, so
  exactly those are escaped, together with the backslash itself so a body that genuinely contains
  one does not produce a different escape in the output.
- **`Flatten`** — replaces a body's line breaks with single spaces so a multi-line comment cannot
  break out of its list entry and read as document text. Nothing is truncated or summarized.

#### Error Handling

`WriteAsync` throws `ArgumentNullException` for a null sink. Comment validation happens earlier, at
`ExtractionSink.ReportReviewComment`, which rejects a null comment and a blank body or location, so a
comment that reached the writer is already renderable. That invariant is deliberately retained: a
blank body reaching this point is a backend defect, and each backend drops a text-free comment in its
own reader rather than relying on this writer to tolerate one.

The absence of comments is not an error and is not reported as one. It is reported as the absence of
the file, and — because silence is not an answer — as an explicit `not present - none were written`
line in `summary.txt`.

#### Dependencies

- **`ExtractionSink`** — recorded review comments plus the scratch-folder gate.
- **`ScratchFolder`** — deterministic contained text writes.

#### Callers

`DocDownEngine` calls `ReviewCommentsWriter` immediately after `ContentWriter`, on both the success
and the failure path, and threads the result into `ManifestWriter`, `SummaryWriter`, and
`ExtractionResult`.
