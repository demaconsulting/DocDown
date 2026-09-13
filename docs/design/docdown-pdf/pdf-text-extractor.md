## PdfTextExtractor

![DocDown.Pdf Structure](DocDownPdfView.svg)

### Purpose

`PdfTextExtractor` turns a PDF page's positioned glyphs into markdown. Its single responsibility is
the text rendering decision: how glyphs become words, words become paragraphs, paragraphs acquire an
order a human would read them in, and where the links to that page's images and the page boundary
markers belong.

### Data Model

`PdfTextExtractor` is an `internal static class`. It holds no mutable state; the markdown builder
lives for the duration of a single call, so it is thread-safe.

- **`HeadingTagLevels`** (`Dictionary<string, int>`) — the marked-content structure tags that denote
  a heading, mapped to a markdown level. `Title` maps to level one, the untyped `H` tag to level two
  because it is a heading of unspecified rank and the title already owns level one, and `H1` through
  `H6` one-to-one.
- **`PdfTextResult`** (`internal sealed record`) — the rendered `Markdown` and an `AnyGlyphs` flag.
  The two are reported separately because they answer different questions: whether the document has a
  text layer at all, and what could be made of it. A scanned document has neither, and the
  distinction is what lets the caller explain that rather than emit a silently empty document.

### Key Methods

- **`static PdfTextResult Extract(IReadOnlyList<Page> pages, string? documentTitle,
  IReadOnlyList<PdfExtractedImage> images, CancellationToken)`** — heads the output with the document
  title when one is declared, then for each page emits a `<!-- docdown:page N -->` marker, the page's
  text, and the links for that page's images. Preconditions: `pages` and `images` non-null.
  Postcondition: `AnyGlyphs` is true if and only if at least one supplied page carried a letter.
  The page marker convention matches what Core passes through untouched, so a consumer can map any
  passage back to its source page.
- **`AppendPageText`** (private) — the text rendering decision for one page. Algorithm: collect the
  page's declared headings from its marked-content tree; segment the page into reading-ordered
  blocks; emit each block as a heading when the document declared it as one, and as a paragraph
  otherwise. When segmentation yields no blocks at all, falls back to the parser's content-order text
  extractor rather than losing the text.
- **`SegmentIntoReadingOrder`** (private) — groups letters into words by nearest-neighbour proximity,
  words into blocks by the Docstrum algorithm, and orders the blocks by unsupervised reading-order
  detection. This combination is what turns a two-column page into two sequential columns rather than
  interleaved lines. **The parser's raw page text is never used**: it is content-stream paint order
  with no word or paragraph boundaries, and the parser's own documentation warns against it.
- **`CollectHeadingTexts`** (private) — walks the marked-content tree, including children, and maps
  each heading element's normalized text to its level. Matching on text rather than on geometry lets
  an independently segmented block be recognized as a heading without reconciling two different
  groupings of the same glyphs. An untagged page — the overwhelmingly common case — contributes
  nothing, which is why it degrades to plain paragraphs. Headings are never guessed from font size:
  a confidently wrong heading is worse for a downstream consumer than no heading at all.
- **`AppendPageImages`** (private) — emits a markdown image link for each image attributed to the
  page, using the relative path the sink returned. No path is constructed here.
- **`Normalize`** (private) — collapses whitespace runs to single spaces and trims. A block's line
  breaks come from the page's physical layout rather than the author's paragraphing, so preserving
  them would encode page geometry into the markdown; collapsing them also makes heading text
  comparable between the tagged structure and the segmented blocks.

### Error Handling

Null `pages` or `images` are rejected with `ArgumentNullException` as caller errors. No other error
condition is raised: a page with no glyphs contributes no text and is reported through `AnyGlyphs`,
and a page whose segmentation yields nothing falls back rather than failing. Cancellation is observed
per page and propagates.

### Dependencies

- **PdfPig** (OTS) — `Page.Letters`, `Page.GetMarkedContents`, the word extractor, the page
  segmenter, the reading-order detector, and the content-order text extractor used as a fallback.
- **PdfExtractedImage** — the link paths and descriptions produced by `PdfImageExtractor`. See
  *PdfImageExtractor Design*.

### Callers

`PdfDocumentExtractor`, after the image extraction whose links this unit places. See
*PdfDocumentExtractor Design*.
