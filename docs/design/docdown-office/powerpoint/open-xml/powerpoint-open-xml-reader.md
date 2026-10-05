### PowerPointOpenXmlReader

![DocDown.PowerPoint Structure](PowerPointView.svg)

### Purpose

`PowerPointOpenXmlReader` is the one place that touches the Open XML object model. Its single
responsibility is translation: it turns a `.pptx` package into the backend-neutral
`PowerPointDeckModel`, preserving slide order and extracting each slide's title, body text, and
speaker notes, which never appear in any render. It performs no filesystem I/O; the caller hands it a
seekable stream.

### Data Model

`PowerPointOpenXmlReader` is an `internal static class`. It holds no state and is safe to reuse. It
reads into `PowerPointDeckModel` — the slides in presentation order, the deduplicated images, and the
deck metadata — where each `PowerPointSlideModel` carries its 1-based ordinal, its title or null, its
body text lines, its speaker notes or null, the images it references, and the reviewer comments
attached to it. Each `PowerPointCommentModel` carries the resolved author name (or `null` when the
deck names nobody for it) and the comment text taken whole.

### Stated scope limitation: modern persona comments

PowerPoint has two comment grammars. The **legacy** grammar stores a slide's comments in
`ppt/comments/comment<n>.xml`, exposed as `SlidePart.SlideCommentsPart`, and names each comment's
author by an identifier resolved through the presentation's `commentAuthors.xml`
(`PresentationPart.CommentAuthorsPart`). This reader reads that grammar.

The **modern** grammar — the persona-based, cloud-synced comments that current PowerPoint writes — is
deliberately **not** read. This is a scope choice, not a tooling limit, and the distinction matters
because an earlier revision of this document claimed the opposite. `DocumentFormat.OpenXml 3.5.1`,
the SDK this package is built on, *does* expose both the part and its grammar: `SlidePart.commentParts`
yields `PowerPointCommentPart`, whose `CommentList` is typed over the
`DocumentFormat.OpenXml.Office2021.PowerPoint.Comment` namespace. Nothing here needs raw relationship
access or guesswork about an unversioned grammar.

What is not read is the modern **author model**. A modern comment names its author through a separate
persona and author list rather than through the presentation's `commentAuthors.xml`, and that
resolution could not be validated here against a deck a genuine PowerPoint wrote. Reading the
comments without it would mean attributing a reviewer's words on unverified reasoning, which is a
worse outcome than stating the boundary.

So the boundary is **reported at runtime rather than only here**. `CountModernComments` counts the
modern comments each slide carries — a count only, with no text and no author read — and
`PowerPointContentEmitter` turns a non-zero total into an extraction note naming how many comments
sit on how many slides. The consequence is therefore: **a deck whose comments were all authored as
modern persona comments contributes no comments to `review-comments.md`, and says so plainly in
`summary.txt` and `manifest.json`.** A user who finds `review-comments.md` absent for a deck they
know carries remarks reads the note, not a fault. The same statement appears in the reader's own XML
documentation so it is visible at the call site.

### Key Methods

- **`PowerPointDeckModel Read(Stream stream)`** — opens the package read-only, walks the
  presentation's `SlideIdList` to order the slide parts by presentation order and assign 1-based
  ordinals, resolves the embedded images through `PowerPointOpenXmlImageReader`, loads the
  presentation's comment-author list, reads each slide, counts the modern comments each slide
  carries, and
  maps the OPC core properties to deck metadata. Precondition: `stream` is non-null, readable, and
  seekable. Throws `PowerPointExtractionException` when the package cannot be opened or carries no
  presentation part.
- **`ReadSlide`** (private) — reads one slide: the title from the title placeholder, every other
  shape's paragraphs as body lines, the speaker notes, and the reviewer comments; returns the slide
  model with its ordinal
  and image references.
- **`LoadCommentAuthors`** (private) — reads `PresentationPart.CommentAuthorsPart` into a map of
  author identifier to display name, up front, so every comment resolves its author without
  re-walking the package. A deck with no comment-authors part yields an empty map.
- **`CountModernComments`** (private) — counts the modern comments a slide carries through
  `SlidePart.commentParts` and `PowerPointCommentPart.CommentList`, reading neither their text nor
  their authors. The deck-level total and the number of slides carrying them travel on the model as
  `ModernCommentCount` and `ModernCommentSlideCount`, so the emitter can state their presence
  without this reader having to decide how it is phrased.
- **`ReadComments`** (private) — reads `SlidePart.SlideCommentsPart` in declared order, resolving each
  comment's author identifier against that map. An identifier the list does not name yields no
  attribution rather than a bare number. A comment with no text is dropped because it says nothing. A
  slide with no comments part yields no comments rather than an error. Modern persona comments are not
  read; see the stated scope limitation above.
- **`ReadComments`** (private) — reads `SlidePart.SlideCommentsPart` in declared order, resolving each
  comment's author identifier against that map. An identifier the list does not name yields no
  attribution rather than a bare number. A comment with no text is dropped because it says nothing. A
  slide with no comments part yields no comments rather than an error. Modern persona comments are not
  read; see the stated scope limitation above.
- **`ReadNotes`** (private) — reads the speaker notes from the notes slide's body placeholder only, so
  slide-number and date furniture on the notes slide do not contaminate the narration; returns null
  when the slide has no notes.
- **`ReadShapeParagraphs`** (private) — concatenates each paragraph's text runs into one line and
  drops blank paragraphs, so a run-split line reads as one line.
- **`IsTitle`** / **`IsBody`** / **`PlaceholderType`** (private) — classify a shape by its
  placeholder type, separating the title placeholder from the body shapes and identifying the notes
  body placeholder.

### Error Handling

An `OpenXmlPackageException` or `FileFormatException` from opening the package, and a missing
presentation part, are wrapped in a `PowerPointExtractionException` with a message that names the
likely cause. Every other fault propagates to Core. `ArgumentNullException` guards a null stream. The
reader never truncates or reorders content: a run-split line is rejoined, but nothing is dropped except
genuinely blank paragraphs and notes-slide furniture.

### Dependencies

- **DocDown.Core** — `EmbeddedImage`, `DocumentMetadata`, `OpcCoreProperties`, `OpcMetadataMapper`,
  and the model types the reader populates.
- **DocumentFormat.OpenXml** (OTS) — `PresentationDocument`, `PresentationPart`, `SlidePart`,
  `NotesSlidePart`, `CommentAuthorsPart`, `SlideCommentsPart`, `PowerPointCommentPart` and the
  `DocumentFormat.OpenXml.Office2021.PowerPoint.Comment` types used to count modern comments, and
  the Presentation and Drawing element types.
- **PowerPointOpenXmlImageReader** — resolves the deck's embedded images and per-slide references.

### Callers

`PowerPointOpenXmlExtractor.ExtractAsync` calls `Read` after buffering the source. The self-test
round-trip case and the COM backend's delegated managed run reach it through the same extractor.
