## PdfTextExtractor

![DocDown.Pdf Structure](DocDownPdfView.svg)

### Purpose

`PdfTextExtractor` turns a PDF page's positioned glyphs into markdown. Its single responsibility is
to render page text in human reading order, preserve page boundaries, place image links under the
page they came from, and count the headings and paragraphs it emitted.

### Data Model

`PdfTextExtractor` is an `internal static class`. It holds no mutable shared state; the markdown
builder and counters live only for the duration of one call.

- **`HeadingTagLevels`** (`Dictionary<string, int>`) - maps tagged-PDF heading tags to markdown
  levels.
- **`PageTextCounts`** (`internal readonly record struct`) - the heading and paragraph counts emitted
  for one page.
- **`PdfTextResult`** (`internal sealed record`) - the rendered markdown together with the total
  heading and paragraph counts for the selected pages.

### Key Methods

- **`static PdfTextResult Extract(IReadOnlyList<Page> pages, string? documentTitle,
  IReadOnlyList<PdfExtractedImage> images, CancellationToken)`** - writes the document title when
  present, emits a `<!-- docdown:page N -->` marker for each page, renders page text, places that
  page's image links, and returns the total heading and paragraph counts. Preconditions: `pages` and
  `images` non-null.
- **`AppendPageText`** (private) - renders one page's text. A page with no glyphs contributes zero
  counts. When segmentation yields no blocks, the method falls back to PdfPig's content-order text
  extractor rather than losing text.
- **`SegmentIntoReadingOrder`** (private) - groups letters into words, words into blocks, and blocks
  into reading order so multi-column pages read as a human would read them.
- **`CollectHeadingTexts`** (private) - maps tagged heading text to markdown levels without guessing
  headings from font size or geometry.
- **`AppendPageImages`** (private) - writes markdown image links using the relative paths returned by
  the sink.
- **`Normalize`** (private) - collapses whitespace runs so paragraph text and tagged heading text are
  comparable.

### Error Handling

Null `pages` or `images` are rejected with `ArgumentNullException` as caller errors. A page with no
glyphs contributes no text and zero counts rather than throwing. Cancellation is observed per page
and propagates.

### Dependencies

- **PdfPig** (OTS) - page letters, marked-content inspection, word extraction, page segmentation,
  reading-order detection, and the content-order fallback extractor.
- **PdfExtractedImage** - the image links supplied by `PdfImageExtractor`. See
  *PdfImageExtractor Design*.

### Callers

`PdfDocumentExtractor`, after image extraction has produced the links this unit places. See
*PdfDocumentExtractor Design*.
