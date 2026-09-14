### PowerPointOpenXmlReader

![DocDown.PowerPoint Structure](DocDownPowerPointView.svg)

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
body text lines, its speaker notes or null, and the images it references.

### Key Methods

- **`PowerPointDeckModel Read(Stream stream)`** — opens the package read-only, walks the
  presentation's `SlideIdList` to order the slide parts by presentation order and assign 1-based
  ordinals, resolves the embedded images through `PowerPointOpenXmlImageReader`, reads each slide, and
  maps the OPC core properties to deck metadata. Precondition: `stream` is non-null, readable, and
  seekable. Throws `PowerPointExtractionException` when the package cannot be opened or carries no
  presentation part.
- **`ReadSlide`** (private) — reads one slide: the title from the title placeholder, every other
  shape's paragraphs as body lines, and the speaker notes; returns the slide model with its ordinal
  and image references.
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
  `NotesSlidePart`, and the Presentation and Drawing element types.
- **PowerPointOpenXmlImageReader** — resolves the deck's embedded images and per-slide references.

### Callers

`PowerPointOpenXmlExtractor.ExtractAsync` calls `Read` after buffering the source. The self-test
round-trip case and the COM backend's delegated managed run reach it through the same extractor.
