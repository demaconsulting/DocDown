## PowerPointOpenXmlImageReader

![DocDown.PowerPoint Structure](PowerPointView.svg)

### Purpose

`PowerPointOpenXmlImageReader` resolves the embedded images of a presentation to their bytes and
provenance. Its single responsibility is image resolution across every part that can carry a picture —
slides, their notes, and the slide layouts and masters — recording which slides reference each image so
the association survives extraction. It has no dedicated test class; its observable contract is what
reaches the model, proved through the reader tests that read image decks and the emitter tests that render
the inline links.

### Data Model

`PowerPointOpenXmlImageReader` is an `internal static class` with a private `PartAccumulator` scratch type
that accumulates one distinct image part's referring slides and template flag while the package is walked.
It returns a `PowerPointImageCollection`: the distinct `EmbeddedImage` list in content-then-template order,
and the per-slide ordered `PowerPointSlideImageRef` occurrences for inline linking.

### Key Methods

- **`PowerPointImageCollection Collect(PresentationPart presentationPart, IReadOnlyDictionary<SlidePart,
  int> slideOrdinals)`** — walks slides in presentation order (each with its notes), then the masters and
  their layouts, records every image part each container references, accumulates each part's referring
  slides, flags a part reached only through a layout or master as template furniture, and builds the
  distinct images in first-occurrence order with their per-slide references. Preconditions: both arguments
  non-null.
- **`RecordContainer`** (private) — records every image part a container references, including a
  background or fill image no picture names, adding a slide referrer or a template flag as the container
  dictates; a part already seen keeps its first container's naming candidates and gains the new referrer.
- **`OrderedPictureUris`** (private) — reads the ordered, distinct image-part URIs a slide's pictures
  reference in reading order, used to emit an inline link at each image's point of occurrence.
- **`MapCandidatesByPart`** / **`CandidatesFor`** (private) — map each referenced image part to the
  referencing picture's naming candidates (description, title, object name), so a file is named from the
  document's own meaning.
- **`EmbedIds`** (private) — reads every `r:embed` relationship id a picture carries across its raster
  fallback (`a:blip`) and its scalable vector graphic (`svgBlip`, read from the raw element by name), so
  the vector asset is not lost.
- **`BuildImage`** / **`ReadPartBytes`** / **`NameFromUri`** (private) — read an accumulated part's bytes
  fully, and build its `EmbeddedImage` with the naming candidates, the full referrer set, and the
  template flag.

### Error Handling

Image bytes are a passthrough — never re-encoded — so the reader introduces no image faults. A part that
cannot be read propagates its I/O fault to Core through the calling reader. `ArgumentNullException` guards
the two arguments. A picture that references no resolvable part contributes nothing rather than a
fabricated entry.

### Dependencies

- **DocDown.Core** — `EmbeddedImage`, `ImageTextCandidate`, `ImageTextSource`, and the image-collection
  model types.
- **DocumentFormat.OpenXml** (OTS) — `PresentationPart`, `SlidePart`, `NotesSlidePart`, `SlideMasterPart`,
  `SlideLayoutPart`, `ImagePart`, `OpenXmlPartContainer`, and the Drawing and Presentation picture types.

### Callers

`PowerPointOpenXmlReader.Read` calls `Collect` once, and uses its result to populate the deck images and
each slide's inline image references. Nothing else calls it.
