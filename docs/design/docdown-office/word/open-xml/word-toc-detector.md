## WordTocDetector

![DemaConsulting.DocDown.Word Structure](WordView.svg)

### Purpose

`WordTocDetector` finds the table-of-contents paragraphs in a Word document. Its single
responsibility is detection. The reader omits those paragraphs from `content.md` and the scanner
labels tracked changes inside them, so the omission is deliberate, counted, and reported rather than
silent.

**Decision.** A table of contents is generated, derived content: every entry repeats a heading that
`content.md` already renders, so emitting it would add noise and duplicate text. The extractor
therefore omits it and says so in `summary.txt` and `manifest.json` (the "tables of contents
skipped" content feature, with the entry count and the number of tracked changes inside). Tracked
changes inside it are still counted in the total and listed in `review-comments.md`.

### Data Model

`WordTocInfo(Paragraphs, Regions)` is a record. `Paragraphs` is a set of paragraph elements compared
by reference; `Regions` is the number of runs of adjacent contents paragraphs. `WordTocInfo.None` is
the empty result.

### Key Methods

- **`static WordTocInfo Find(MainDocumentPart mainPart)`** — walks the body and marks a paragraph as
  contents when any of these holds: it is inside a block content control whose document-part gallery
  is "Table of Contents"; its style id is `TOC1` to `TOC9` or its style name matches `toc N`; or it
  lies inside a `TOC` complex or simple field (a field stack is carried across paragraphs, and the
  first token of the instruction decides). A heading met while a `TOC` field is still open clears
  the stack, so an unclosed field cannot swallow the document.
- **`static bool IsTocSdt(SdtBlock)`** — whether a block content control is a table of contents.

### Error Handling

The detector reads only; a missing styles part or an unusual field degrades to "not contents". Faults
from a corrupt part propagate to Core.

### Dependencies

- **DocumentFormat.OpenXml** (OTS) — the body, paragraph, field, and content-control elements.
- **`WordOpenXmlReader.DetermineHeadingLevel`** — recognizes a heading for the recovery rule.

### Callers

`WordOpenXmlReader.Read()` calls `Find()` once and passes the result to the body walk and to
`WordRevisionScanner.Scan()`.
