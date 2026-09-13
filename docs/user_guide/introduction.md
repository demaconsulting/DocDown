# Introduction

## Purpose

This document is the user guide for DocDown, a family of .NET libraries and a command-line tool
that extract useful information from documents of many types into a scratch folder, in a
predictable layout designed to be fed to multimodal AI agents.

## Scope

This user guide covers:

- Installation of the libraries and the command-line tool
- The output contract
- Basic usage and examples
- The `docdown` command-line tool
- API reference

# The Output Contract

Every extraction, regardless of source format, produces the same five artifacts in the scratch
folder:

- **`summary.txt`** — a human- and LLM-readable write-up of what was extracted and where,
  including the absolute path to the scratch folder. This is the file to paste into an LLM
  context window, so it is kept short: it opens with a one-sentence plain-English description of
  the document, composed only from facts already established (never inferred, and omitted
  entirely when the facts do not support one); it outlines what `content.md` contains — headings,
  tables, comments and their distinct comment authors, speaker notes, worksheets, and the like, naming
  only what is genuinely present; and it describes the extracted images in aggregate (how many,
  how large, which pages or slides they span) rather than one line each. It reports the selected
  backend's environment facts and any genuine unavailability, and counts the remaining registered
  backends that did not run.
- **`manifest.json`** — the machine-readable twin of `summary.txt`, and the authoritative record
  of the per-image inventory, the `contentFeatures` outline, and every backend's availability.
  Nothing the summary summarizes is lost: it is recorded here in full.
- **`metadata.json`** — what the document asserts about itself (creator, last-modified-by,
  created and modified dates, revision, and the like), each value tagged with its provenance
  (OPC core properties, PDF document information, or a backend heuristic). Blank values are
  omitted rather than emitted as noise; interesting fields the document left blank are named
  so the file itself records the absence; and it is always written, even when the document
  supplied nothing readable, in which case it says so rather than emitting an empty object.
  `summary.txt` inlines only the author and the modified date and names this file for the rest.
- **`content.md`** — the textual content as markdown, linking to the extracted images.
- **`images/` and `pages/`** — extracted image resources and optional rendered page images.

The output layout is invariant, but the extracted content is best-effort: what can be extracted
depends on the operating system, the installed applications, and the available native binaries.
Whatever could not be extracted is stated explicitly in `summary.txt` and `manifest.json`, with a
reason — you never have to infer a gap from a missing folder.

# Project Status

Eight packages are implemented and under active development: `DemaConsulting.DocDown.Core`, which holds
the shared abstractions and the output contract; `DemaConsulting.DocDown.Pdf`, which extracts PDFs;
`DemaConsulting.DocDown.Pdf.Rendering`, an optional add-on that rasterizes PDF pages to images;
`DemaConsulting.DocDown.Word`, which extracts Word documents; `DemaConsulting.DocDown.Excel`, which
extracts workbooks; `DemaConsulting.DocDown.PowerPoint`, which extracts presentations;
`DemaConsulting.DocDown.Visio`, which extracts drawings; and `DemaConsulting.DocDown.Tool`, the
`docdown` command-line tool. `DocDown.Html` is the one format-specific library that remains planned and
not yet available. This guide documents the API and tool that are present today and will grow as each
remaining library is delivered.

# Continuous Compliance

DocDown follows the
[Continuous Compliance](https://github.com/demaconsulting/ContinuousCompliance) methodology, which ensures
compliance evidence is generated automatically on every CI run.

## Key Practices

- **Requirements Traceability**: Every requirement is linked to passing tests, and a trace matrix is
  auto-generated on each release
- **Linting Enforcement**: markdownlint, cspell, and yamllint are enforced before any build proceeds
- **Automated Audit Documentation**: Each release ships with generated requirements, justifications,
  trace matrix, and quality reports
- **CodeQL and SonarCloud**: Security and quality analysis runs on every build

# Installation

Install the libraries using the .NET CLI:

```bash
dotnet add package DemaConsulting.DocDown.Core
dotnet add package DemaConsulting.DocDown.Pdf
```

To also rasterize PDF pages to images, add the optional rendering package. It carries native binaries
(PDFium and SkiaSharp, via PDFtoImage), so a self-contained single-file build is runtime-identifier
specific and must be published with `dotnet publish -r <rid>`; a framework-dependent reference works on
every runtime identifier:

```bash
dotnet add package DemaConsulting.DocDown.Pdf.Rendering
```

To extract Word documents, add the Word package. Its Open XML backend is fully managed and reads a
`.docx` on every platform with no native dependency. The legacy binary `.doc` format is not
supported by DocDown; it is recognized and refused with an explanation rather than reported as an
unrecognized file:

```bash
dotnet add package DemaConsulting.DocDown.Word
```

To extract Excel workbooks, PowerPoint presentations, or Visio drawings, add the matching packages.
All three extract content on every platform with no native dependency: Excel reads `.xlsx`, PowerPoint
reads `.pptx`, and Visio reads `.vsdx` and its macro-enabled sibling `.vsdm`. The PowerPoint and Visio
packages additionally rasterize slides and pages on Windows when the corresponding Microsoft Office
application is installed, and report a gap when it is not. The legacy binary `.xls`, `.ppt`, and `.vsd`
formats are not supported by DocDown; each is recognized and refused with an explanation:

```bash
dotnet add package DemaConsulting.DocDown.Excel
dotnet add package DemaConsulting.DocDown.PowerPoint
dotnet add package DemaConsulting.DocDown.Visio
```

Install the command-line tool globally (or with `--local` in a tool manifest):

```bash
dotnet tool install -g DemaConsulting.DocDown.Tool
```

## API Documentation

Detailed API documentation for all public types and members is distributed in the `api/` folder
of the NuGet package. It is laid out for progressive reading, in the same spirit as DocDown's own
output contract: `api/api.md` indexes the namespaces, each namespace page lists its types, and
each type page carries the signature, the summary prose, and links on to a page per member. Read
only as far down as the question requires.

The command-line tool package is the one exception: `DemaConsulting.DocDown.Tool` has no public
API — its types are internal and its interface is the command line documented below — so it ships
no `api/` folder.

# Usage

`DemaConsulting.DocDown.Core` provides the extraction engine and the abstractions that define the
output contract. It extracts nothing on its own: a host registers one or more format-specific
backends with it. `DemaConsulting.DocDown.Pdf`, `.Word`, `.Excel`, `.PowerPoint`, and `.Visio` are
those backends, and each is registered by a single explicit `Add…()` call.

## Extracting a PDF

Register the PDF backend, build an engine, and extract:

```csharp
using DocDown.Core;
using DocDown.Pdf;

var engine = new DocDownBuilder()
    .AddPdf()
    .Build();

var result = await engine.ExtractAsync(
    documentPath: @"C:\documents\report.pdf",
    scratchFolder: @"C:\scratch\report",
    cancellationToken: cancellationToken);

Console.WriteLine(result.Outcome);        // Succeeded, Degraded, or Failed
Console.WriteLine(result.SummaryPath);    // the summary.txt to paste into an LLM context
```

`AddPdf` is the entire registration surface. Registration is explicit rather than
reflection-based, so the set of backends an engine has is exactly the set your code asked for.

## What the PDF package provides, and what it does not

`DemaConsulting.DocDown.Pdf` extracts:

- **Text** — in reading order, so a multi-column page reads the way a person would read it rather
  than the way it was painted
- **Embedded images** — written to `images/` and linked from `content.md`
- **Document metadata** — the title, author, and page count the document declares

It does **not** render pages to images on its own. The package is fully managed and ships no native
assets, which is what lets it run anywhere .NET runs, and rendering a page needs a renderer. If you
request rendered pages without the rendering package registered, the extraction does not fail: it
degrades, `pages/` stays empty, and `summary.txt` explains that rendered page images come from a
separate PDF page-rendering extractor package that a host registers alongside this one.

## Rendering pages with DocDown.Pdf.Rendering

`DemaConsulting.DocDown.Pdf.Rendering` is that separate package. Register it alongside the managed PDF
backend and request rendered pages:

```csharp
using DocDown.Core;
using DocDown.Pdf;
using DocDown.Pdf.Rendering;

var engine = new DocDownBuilder()
    .AddPdf()
    .AddPdfRendering()
    .Build();

var options = new ExtractionOptions { RenderPages = true, PageRenderDpi = 150 };
var result = await engine.ExtractAsync(
    @"C:\documents\report.pdf", @"C:\scratch\report", options, cancellationToken);
```

A few things are worth knowing:

- **Selection is automatic.** The rendering backend is chosen only when you request rendered pages
  (`RenderPages = true`, or `--pages` on the tool). For a plain extraction the lighter managed backend
  is used, so you pay no native cost for a run that does not render.
- **`--dpi` (or `PageRenderDpi`) controls resolution.** A higher DPI produces a larger, more detailed
  page image and a larger file; the default is 150.
- **Degradation stays honest.** If a page cannot be rasterized — an unsupported page, or an
  out-of-memory at a very high DPI on a very large page — that page is reported as a counted gap naming
  the page, the remaining pages still render, and no exception reaches your code. If the native binary
  for your platform cannot load, the backend reports itself unavailable with a reason and the run
  degrades exactly as if the package were not registered.
- **One page renders at a time.** The underlying PDFium renderer is not thread-safe, so rendering calls
  are serialized process-wide; concurrent extractions cannot rasterize in parallel.
- **Native binaries mean RID-specific single-file builds.** This is the one package that carries native
  assets. A framework-dependent reference (including `dotnet tool install -g` of the `docdown` tool)
  resolves the correct native binary for your runtime at run time. A self-contained single-file build,
  however, must be published per runtime identifier with `dotnet publish -r <rid>`; a single portable
  binary cannot carry every platform's native assets. Trim and AOT compatibility for this stack are
  unverified, so leave `PublishTrimmed` and AOT off.

## Degradation you may see, and what it means

Degradation is normal rather than exceptional, and every instance of it is explained in
`summary.txt` and `manifest.json`.

**The PDF has no text layer (a scan).** The run degrades, `content.md` is still written, and a gap
states that the pages carry no extractable text. Text from a scan needs optical character
recognition, which this package does not perform.

**An image uses JPEG 2000.** That image is written out as the JPEG 2000 file it already is, with a
`.jp2` extension and an `image/jp2` media type, because this package does not decode JPEG 2000. A gap
says so and warns that many image viewers and image libraries cannot read the format — the file is
there, but you may need a JPEG 2000-capable tool to open it.

**An image uses JBIG2.** That image is not written, and a counted gap names the encoding and how many
images it cost. Its stored bytes are a bare segment rather than a file, so there is no extension under
which they could honestly be written. The images that could be extracted still are.

**You set `ImageOutput` to `ForcePng` and the PDF holds JPEGs.** The JPEGs are written as JPEG, with
a `.jpg` extension and a JPEG media type, because this package does not decode them. A gap explains
which images defeated the request and why — the file on disk always describes its own bytes, never
the format you asked for.

**You set `MaxImageBytes` or `MaxImageDimensionPx`.** Images over the limit are skipped and reported
in their own gap, separately from any decoding failure, so you can tell a limit you chose from a
limitation of the document.

**The PDF is encrypted or corrupt.** The run fails rather than throwing: you get a coded, structured
failure and the full output layout, so a batch run can record the outcome and continue.

**The PDF has no pages.** The run completes with a gap stating that the document contains no pages.

## Image provenance

Every image `manifest.json` lists records how it was produced:

- `passthrough` — the bytes are exactly what the document stored. A JPEG embedded in a PDF is
  already a complete JPEG file, so it is copied out unchanged; so is a JPEG 2000 image, which is
  written as `.jp2` with a gap warning that the format is not widely readable.
- `decodedToPng` — the document stored compressed samples rather than an image file, so the samples
  were decoded and re-encoded as PNG.

The distinction matters if you need an extracted image to stand for the original: only a
`passthrough` image is byte-for-byte what the document contained.

Each image also records where it is referenced, so `images/` stays connected to the narrative in
`content.md`:

- `sourcePages` lists **every** 1-based page, slide, or worksheet that references the image, sorted
  and distinct. A logo shown on slides 3 and 7 lists both; a picture on one worksheet lists that one.
- `sourcePage` is a convenience alias for the first (lowest) entry of `sourcePages`, kept for a
  consumer that reads only the scalar.
- `referencedByTemplate` is `true` when the image is reached only through a template container — a
  PowerPoint slide layout or master, or a Visio master — rather than a specific page.

Together these give an honest three-way distinction: an image referenced by a page (non-empty
`sourcePages`), referenced only via a template (`referencedByTemplate` with an empty `sourcePages`),
or a true orphan referenced by nothing (both empty). A template-referenced image is never given a
fabricated page. Where a format does not expose the association, `sourcePages` is simply empty rather
than guessed.

## Extracting a Word document

`DemaConsulting.DocDown.Word` reads Word documents. Register it and extract exactly as you would the
PDF backend:

```csharp
using DocDown.Core;
using DocDown.Word;

var engine = new DocDownBuilder()
    .AddWord()
    .Build();

var result = await engine.ExtractAsync(
    documentPath: @"C:\documents\report.docx",
    scratchFolder: @"C:\scratch\report",
    cancellationToken: cancellationToken);

Console.WriteLine(result.Outcome);        // Succeeded, Degraded, or Failed
Console.WriteLine(result.SummaryPath);    // the summary.txt to paste into an LLM context
```

`AddWord()` registers the package's single Open XML backend. There is no second call and no second
backend: the result is the same fully managed, deterministic extraction on every platform.

## What the Word package provides, and what it does not

`DemaConsulting.DocDown.Word` extracts:

- **Text and structure** — headings, ordered and bulleted lists, and inline emphasis and links, in
  document order
- **Real tables** — a Word table becomes a genuine GitHub-Flavored-Markdown table, which is the
  headline advantage of reading the document's own model rather than a flattened rendering. Where a
  cell is merged (`gridSpan`/`vMerge`) or a table is nested, GitHub-Flavored Markdown cannot express
  it, so the cell is flattened and every flattened cell is counted in a reported gap rather than
  misrepresented silently
- **A Document Control section** — the identifying furniture a Word document keeps in its headers and
  footers (document number, revision, classification) is collected once, under a `## Document Control`
  heading placed after the title and before the body. Page-number-only headers and footers are omitted
  with a counted gap, because a page number is meaningless in a single flow
- **Embedded images** — written to `images/` and linked from `content.md`, with their bytes passed
  through unchanged; a vector metafile (EMF/WMF) is written as-is with a caveat that many viewers
  cannot open it
- **Document metadata** — the title and author the document declares, and the page count the producer
  recorded (never a computed one)
- **Tracked changes** — rendered in the accepted-revisions view (insertions kept, deletions dropped),
  with a note that a view was chosen

It does **not** render pages to images. The package is fully managed and ships no native assets, which
is what lets it run anywhere .NET runs, and it therefore declares no rendered-pages capability: a
request for rendered pages degrades with a reasoned explanation rather than producing an empty
`pages/` folder.

It does **not** read the plotted data of a chart embedded in a document. A chart is anchored through
a graphic frame carrying no image and no text, so it would otherwise leave no trace at all; instead,
every embedded chart is counted and reported as a gap (`WORD0010`) that points at extracting the
source workbook with `DemaConsulting.DocDown.Excel`, which does read a chart's cached data series in
full. The PowerPoint package reports the same way (`PPTX0005`).

A word on why a future Word page-rendering package, if one is ever built, would not undermine this:

> Exporting a document to PDF **purely to rasterize page images** is legitimate: a page image is
> inherently a visual artifact, and a faithful raster of a page loses nothing that a page image was
> ever going to carry. What DocDown rejects is converting to PDF and then **extracting text and
> structure from the PDF**, because that path destroys precisely the structure DocDown exists to
> harvest — cell values, formulas, speaker notes, heading levels and table semantics — and replaces it
> with positioned glyphs. Rendering-via-PDF is not extraction-via-PDF. In every DocDown package, text
> and structure come from the document's own model; a PDF, where one is produced at all, is a
> rasterization intermediate and is never read back for content.

A Word page-rendering package is therefore a possible future follow-on, not something this package
does today: text and structure would still come from the Word model, and a rendered page image, if
ever produced, would be a rasterization intermediate and never read back for content.

## Extracting an Excel workbook

`DemaConsulting.DocDown.Excel` reads Excel workbooks. Register it and extract exactly as you would the
Word backend:

```csharp
using DocDown.Core;
using DocDown.Excel;

var engine = new DocDownBuilder()
    .AddExcel()
    .Build();

var result = await engine.ExtractAsync(
    documentPath: @"C:\documents\budget.xlsx",
    scratchFolder: @"C:\scratch\budget",
    cancellationToken: cancellationToken);
```

`AddExcel()` registers the package's single Open XML backend, `excel-openxml`. There is no second
call and no second backend, and that is deliberate rather than incidental: the workbook intent offers
no rendering, so there is no environment-dependent sibling to register.

## What the Excel package provides, and what it does not

`DemaConsulting.DocDown.Excel` extracts:

- **Every worksheet's cell values, verbatim and at full length** — nothing is truncated and nothing is
  summarized. In this product's domain a workbook is a prose and data container, not a picture: its
  cells hold requirement quotations, pasted transcripts, and exact numbers, and an abbreviated cell
  would be a different fact from the one the workbook carries
- **Formulas, preserved alongside the computed value** — a cell is written as its address, its value,
  and its formula where one exists, because the formula states the relationship and the value records
  only one evaluation of it
- **A grid table for a dense rectangular region, in addition to the cell listing** — when a worksheet's
  populated cells form a table-shaped block, that block is also rendered as a markdown table with
  column-letter headers and row numbers, so the row/column relationships survive for a reader. The
  table is emitted *alongside* the address/value/formula listing, never instead of it: a cell that is
  too long or spans multiple lines is elided in the table as `…` (its full verbatim value stays in the
  listing, and a short note beside the table says exactly that, so the marker is never mistaken for
  content that could not be recovered), a formula cell shows its cached value in the table (its
  formula stays in the listing), and
  a merged range — which a markdown table cannot span — is stated as a note. A sparse or very wide
  worksheet keeps the listing alone, because a table of mostly-blank cells would add nothing
- **Sheet identity** — each worksheet becomes its own content part carrying the sheet name, and every
  cell is cited by its address, so a fact drawn out of the extraction can be traced back to the cell
  it came from
- **Structure** — the worksheet count and order the workbook declares
- **Each chart's cached data series** — a chart part stores the values as last plotted, not merely a
  picture of them, and those values are what a consumer that cannot recalculate the workbook can
  actually use. Every chart becomes its own content part (kind `chart`, written straight after the
  worksheet that shows it and named in that worksheet's part) carrying the chart title, the plot
  type, the category and value axis titles, each series' name, number format and source range, and a
  table of point index and category label against one column per series. Multiple series each get
  their own column, and a cache that omits points — a chart whose source cells were partly empty —
  keeps its declared point indices rather than packing values into rows they do not belong to. A
  chart's table is bounded at 500 plotted points: 500 keeps every ordinary engineering chart whole
  while stopping one logged sweep from crowding out the rest of a workbook, and when the bound
  applies the part says so and a counted gap records it
- **Text on drawing shapes** — callouts, labels, and annotations floating over a worksheet live in
  the drawing layer and in no cell, so they are extracted under the worksheet that carries them
- **Embedded images** — every picture embedded in a worksheet is written to `images/` with its bytes
  passed through unchanged and its true file extension (a `.png` stays a `.png`, a `.emf` metafile
  stays a `.emf`), named from the picture's own description, title, or object name where the document
  supplies one, and linked inline from `content.md` under the worksheet that shows it. The manifest
  records the 1-based tab index of every worksheet that references each image in `sourcePages`; a
  workbook has no template concept, so `referencedByTemplate` is always `false`
- **Document metadata** — the workbook's self-reported properties (creator, last-modified-by, created
  and modified dates, revision) are written to `metadata.json` with OPC-core-properties provenance

It does **not** render pages, because a workbook is not paginated: it has no fixed page grid to
rasterize, rendering would clip long prose at page boundaries, and a picture of a number substitutes
for the number itself. Because rendering does not *apply* to a workbook rather than being merely
unavailable, a `--pages` request is honored with silence — the run still succeeds and an informational
`DD0303` diagnostic records that page rendering did not apply — rather than degrading with a gap for a
capability that could never be delivered.

Two further honest limits are worth stating plainly:

- **A chart that cached no values yields only its labeling.** A chart saved without its cached data
  can be reported by title, axes, and source range but not by value, so the absence is a counted gap
  naming the chart, with the remedy of opening and re-saving the workbook so the chart caches its
  plotted values again. The extended chart types (waterfall, funnel, tree map, box-and-whisker) use a
  different cache grammar this backend does not yet read and are reported the same way.
- **Document metadata now reaches `metadata.json`, but the manifest's `document` block stays thin.**
  The workbook's self-reported OPC core properties (creator, dates, revision) are written to
  `metadata.json`; the manifest `document` block still reports only the worksheet count, because a
  workbook has no title, author, or page count to record there.
- **A workbook that cannot be opened fails rather than throws.** An encrypted, malformed, or truncated
  workbook becomes a structured, coded failure with the full output layout still written, exactly as
  for the other backends.

## Extracting a PowerPoint presentation

`DemaConsulting.DocDown.PowerPoint` reads PowerPoint presentations:

```csharp
using DocDown.Core;
using DocDown.PowerPoint;

var engine = new DocDownBuilder()
    .AddPowerPoint()
    .Build();

var options = new ExtractionOptions { RenderPages = true };
var result = await engine.ExtractAsync(
    @"C:\documents\deck.pptx", @"C:\scratch\deck", options, cancellationToken);
```

`AddPowerPoint()` registers **two** backends in one call: the managed Open XML backend
`powerpoint-openxml`, which is the guaranteed content path on every platform, and the COM automation
backend `powerpoint-com`, which additionally rasterizes slides through Microsoft PowerPoint. Neither
carries a native asset — the COM backend reaches PowerPoint through late-bound IDispatch and reports
itself unavailable where PowerPoint is absent — so one call registers the complete capability with no
platform-specific dependency.

## What the PowerPoint package provides, and what it does not

`DemaConsulting.DocDown.PowerPoint` extracts:

- **Every slide's text, in presentation order** — the deck stays navigable, and with
  `--split part` (or `ContentSplitMode.PerPart`) each slide becomes its own content part
- **Each slide's title** — read from the slide itself rather than inferred from the text
- **Speaker notes** — the half of a deck that appears in no rendering at all, and therefore must be
  recovered from the file. Where a deck genuinely carries none, that is stated as an informational
  `PPTX0002` diagnostic (visible in `summary.txt`) rather than a gap: a notes-less deck is well-formed,
  and no better environment would yield notes that do not exist, so it must not degrade the run
- **Embedded images** — every picture embedded anywhere in the deck (on slides, notes, layouts, and
  masters) is written to `images/`, deduplicated so a logo reused across many slides is stored once,
  with its bytes passed through unchanged and its true file extension; where a picture carries both a
  raster fallback and a scalable vector graphic, both are written, because the vector asset is the one
  a downstream consumer most often wants. Each slide's pictures are linked inline from `content.md` at
  the slide that shows them, so a picture reused on several slides is linked from each; the manifest
  records every referring slide in `sourcePages`, and an image reached only through a layout or master
  is flagged `referencedByTemplate` rather than given a fabricated slide
- **Rendered slides, when `powerpoint-com` is selected** — each slide is exported straight to a PNG in
  `pages/` at the requested `--dpi`, with no PDF hop and no reading back of the raster for content

Where the embedded images include EMF or WMF vector metafiles, their bytes are written through
unchanged and counted as extracted, and an informational `PPTX0003` caveat notes that many viewers
cannot render a Windows metafile — the image is present, but its readability depends on the consumer.
Because no better environment would yield more, the caveat does not degrade the run.

The honest limits on rendering:

- **Rendering needs Windows and an installed Microsoft PowerPoint.** The availability probe checks the
  operating system and then the registered ProgID; it opens no deck and launches nothing. Where either
  is missing the backend reports itself unavailable with a reason, and the managed backend still
  delivers slide text, titles, and notes.
- **A failed slide is counted, not fatal.** A slide that cannot be rendered becomes a counted gap naming
  the slide, and the remaining slides still render.
- **Metadata is partial.** The backend reports the slide count as the page count and uses the first
  slide's title as the document title; it reports no author.

## Extracting a Visio drawing

`DemaConsulting.DocDown.Visio` reads Visio drawings — both `.vsdx` and the macro-enabled `.vsdm`, which
is read exactly as a `.vsdx` because it uses the same modern Open Packaging Conventions:

```csharp
using DocDown.Core;
using DocDown.Visio;

var engine = new DocDownBuilder()
    .AddVisio()
    .Build();

var result = await engine.ExtractAsync(
    documentPath: @"C:\documents\schematic.vsdm",
    scratchFolder: @"C:\scratch\schematic",
    cancellationToken: cancellationToken);
```

As with PowerPoint, `AddVisio()` registers two backends: the managed Open Packaging backend
`visio-openxml`, which needs no Visio installation, and `visio-com`, which additionally rasterizes pages
through Microsoft Visio.

## What the Visio package provides, and what it does not

`DemaConsulting.DocDown.Visio` extracts:

- **Page names and shape text**, in document order, for `.vsdx` and `.vsdm` alike. A shape whose text
  spans several lines — a pump with its part numbers beneath it, a fittings list, a valve-positioning
  table — keeps every line inside its own list item, indented and hard-broken so the structure the
  author wrote survives a markdown renderer. Nothing is truncated: those part numbers are much of
  what makes Visio text worth extracting
- **Symbol-font glyphs recovered where the drawing proves what they are.** Visio stores a Wingdings
  arrow as a code point that decodes to the Latin letter `à`, so a valve table would otherwise read
  `IN à OUT`. The substitution is gated on the run's own declared font, and only code points with a
  documented Unicode equivalent are mapped, so genuine French text is never touched and no glyph is
  guessed at
- **Zero-information output suppressed, and counted.** A shape whose text is a bare callout number
  (`1`, `2`, `3`) keys into a legend the drawing renders graphically; listed as a component it reads
  as equipment named "1". Likewise an edge whose *both* endpoints are unidentified — `(shape 10) →
  (shape 11)` — tells a reader nothing. Both are omitted, and the omitted counts are stated in the
  content so the omission is visible rather than silent. Every edge with at least one meaningful
  endpoint is kept
- **The directed connector topology** — the headline capability, and the reason the managed backend is
  worth having on a machine with no Visio installed. A list of disconnected strings is not a schematic:
  each page's connections are rendered as a readable directed graph (`Inlet Tank → Transfer Pump`), so which
  shape connects to which survives extraction
- **Honest endpoint labels.** An endpoint is labeled by the shape's own text where it has any. Where it
  has none, the name of the master the shape was instantiated from is used instead, rendered
  parenthesized and paired with the shape id — `(3-way Plug Valve, shape 34)` — so a type-derived label
  can never be mistaken for a name someone authored, and two shapes of the same type stay
  distinguishable. Masters that describe connective geometry rather than a component are refused as
  labels, because naming an endpoint `Straight Line` would assert that a line is the thing being
  connected to. Where neither fact exists the bare shape id remains, and the manifest publishes both the
  labeling convention and the counts of endpoints resolved by text, by type, and not at all — so a
  consumer can judge how readable the topology really is rather than taking the edge list on trust
- **Rendered pages, when `visio-com` is selected** — each *foreground* page is exported straight to a
  PNG in `pages/` at the requested `--dpi`, with no PDF hop
- **Embedded images** — a drawing that embeds a picture in a `ForeignData` shape has that picture
  written to `images/` with its bytes passed through unchanged and its true file extension, and linked
  inline from `content.md` under the page that shows it. The manifest records every referring page in
  `sourcePages`; a picture reached only through a master is flagged `referencedByTemplate` rather than
  given a fabricated page. The package thumbnail (`docProps/thumbnail.emf`) is deliberately **not**
  treated as embedded content: it is furniture referenced from the package root, not a picture placed
  on a page, so reporting it as an embedded image would be dishonest. A drawing whose vector geometry
  is drawn directly in page XML simply embeds no image parts, and reports none

Where the embedded images include EMF or WMF vector metafiles, their bytes are written through
unchanged and counted as extracted, and an informational `VISIO0003` caveat notes that many viewers
cannot render a Windows metafile. Because no better environment would yield more, the caveat does not
degrade the run.

The honest limits on rendering:

- **Rendering needs Windows and an installed Microsoft Visio**, probed the same declarative way. Without
  it the topology, page names, and shape text are still delivered in full by the managed backend.
- **A failed page is counted, not fatal**, and the remaining pages still render.
- **Background pages are not rendered.** The COM backend exports foreground pages only.
- **Metadata is thin.** The backend reports the page count; it reports no title and no author.

## Backends and how one is chosen

Across all the format packages, `docdown --list-backends` reports eight registered backends:

| Backend id | Formats | Capabilities beyond text |
| ---------- | ------- | ------------------------ |
| `pdf` | `pdf` | embedded images, document metadata |
| `pdf-rendering` | `pdf` | embedded images, **rendered pages**, document metadata |
| `word-openxml` | `docx` | embedded images, document metadata, document structure |
| `excel-openxml` | `xlsx` | embedded images, document metadata, document structure |
| `powerpoint-openxml` | `pptx` | embedded images, document metadata, document structure |
| `powerpoint-com` | `pptx` | embedded images, **rendered pages**, document metadata, document structure |
| `visio-openxml` | `vsdx`, `vsdm` | embedded images, document metadata, document structure |
| `visio-com` | `vsdx`, `vsdm` | embedded images, **rendered pages**, document metadata, document structure |

The engine chooses **exactly one** backend per document. That single fact explains the shape of the
table above: capabilities **supersede rather than compose**, so a rendering backend that advertised only
rendered pages would lose selection to the managed backend and never run at all. `powerpoint-com` and
`visio-com` therefore declare the *full* set their managed sibling declares plus rendered pages, and
deliver all of it — they delegate text and structure to the managed backend and add the rasterized pages
it cannot produce.

Selection is a pure function of the detected format, the registered backends, their probed availability,
and your options. It filters candidates by format, honors a `--backend` override (which never falls
back), drops unavailable candidates, computes the capabilities your options require, prefers a candidate
that satisfies all of them, and breaks remaining ties by declared priority and then by backend id — never
by registration order. Every candidate, chosen or not, receives a verdict in the decision trace.

The practical consequences:

- **A COM backend runs only when you ask for pages.** `RenderPages = true` (or `--pages`) adds rendered
  pages to the required capabilities, which is the only thing that lets the COM backend outrank its
  managed sibling; the managed backends carry the higher priority (10 versus 0) so they stay the default.
  Without a rendering request no COM is touched at all.
- **A COM backend runs only where the Office application is present.** Off Windows, or on a Windows
  machine without the application registered, the probe reports it unavailable with a declarative reason,
  the managed backend is selected, and the missing pages are reported as a gap rather than an error.
- **A `.docx`, `.xlsx`, `.pptx`, `.vsdx`, or `.vsdm` is always handled** by a fully managed backend on
  every platform, with no native cost.
- **A `.doc`, `.xls`, `.ppt`, or `.vsd` is recognized as a legacy binary Office format and refused.**
  DocDown does not support those formats on any platform. The failure names the format and says so
  plainly, which is more useful than reporting the file as unrecognized.

# The Command-Line Tool

`docdown` drives the same engine from a shell or a pipeline. It registers its backends explicitly, so
it can be published as a single-file, trimmed executable.

## Extracting a document

```bash
docdown --input report.pdf --scratch ./out
```

On a successful or degraded extraction the tool prints the absolute path to the produced
`summary.txt` and exits `0`:

```text
DocDown Tool version 1.2.3
Copyright (c) DEMA Consulting

Extraction succeeded.
/home/user/out/summary.txt
```

A degraded extraction additionally notes how many gaps were reported; the detail is in `summary.txt`.
On a failed extraction the tool renders the engine's structured explanation — a headline, the
detected format, the per-candidate verdicts, and a remedy — rather than a stack trace, and exits `1`:

```text
The source document 'report.pdf' could not be read.
Detected format: unknown (application/octet-stream) - detected by file extension

Remedy: Verify the document exists and is readable, then retry.
```

## Exit codes

- `0` — the extraction succeeded, or it degraded with reported gaps (the layout was still produced)
- `1` — the extraction failed, or an argument was invalid

## Options

The tool provides the standard DEMA command-line vocabulary plus DocDown's extraction options. Run
`docdown --help` for the authoritative list.

| Option | Meaning |
| ------ | ------- |
| `--input <file>` | Document to extract (required for extraction) |
| `--scratch <dir>` | Scratch folder to write the extraction into (required) |
| `--pages` / `--no-pages` | Request or disable rendered page images |
| `--require-pages` | Fail rather than degrade if pages cannot be rendered |
| `--page-range <a-b>` | Restrict extraction to a page range |
| `--dpi <#>` | Page render DPI (range 36-1200) |
| `--images <preserve\|png>` | Embedded image output mode |
| `--no-images` | Do not extract embedded images |
| `--max-image-dim <#>` | Downscale images exceeding this pixel dimension |
| `--max-image-bytes <#>` | Skip images exceeding this byte size |
| `--split <auto\|single\|part>` | `content.md` splitting strategy |
| `--backend <id>` | Force a named backend, e.g. `word-openxml` or `pdf` (never falls back) |
| `--overwrite <require-empty\|clean\|overwrite\|unique>` | Scratch folder policy |
| `--list-backends` | List registered backends with availability and reason |
| `--verify <dir>` | Verify an existing scratch folder against its manifest |
| `-v`, `--version` | Display version information |
| `-?`, `-h`, `--help` | Display help |
| `--silent` | Suppress console output |
| `--log <file>` | Write output to a log file |
| `--validate` | Run self-validation |
| `--results <file>` | Write validation results to a `.trx` or `.xml` file |
| `--depth <#>` | Heading depth for markdown output (default 1) |

## Self-validation

`docdown --validate` runs the tool's own commands and the self-test cases every registered backend
contributes, prints an environment header and per-test results, and — when `--results` is given —
writes the outcome as TRX (`.trx`) or JUnit (`.xml`):

```bash
docdown --validate --results docdown-validate-windows.trx
```

A self-test that cannot run in the current environment (for example the PDF page-rendering case, which
this package never attempts) is recorded as **not-executed**, which is distinct from a failure: it
produces no false failure. Note that a not-executed result is not evidence either — a traceability
tool ignores it rather than counting it — so a skip is safe but satisfies nothing.

**The results-file name is load-bearing.** A traceability pipeline filters results by a
case-insensitive substring match against the result file's base name, so name a `--validate` results
file to contain the platform it was produced on — `docdown-validate-windows.trx`,
`docdown-validate-ubuntu.trx`, `docdown-validate-macos.trx` — so a platform-filtered requirement can
match it.

# Diagnostic codes

Every diagnostic reported in `summary.txt` and `manifest.json` carries a stable code so a consumer can
branch on the condition without parsing prose. The codes are a documented contract: the numbering is
stable across releases, and each package owns a distinct prefix so codes never collide in a shared
manifest — `DD` for the engine, `PDF`/`PDFR` for the PDF packages, `WORD` for the Word package, `XLSX`
for the Excel package, `PPTX` for the PowerPoint package, and `VISIO` for the Visio package.

The engine's `DD` range (reported by `DocDown.Core` for any backend):

| Code | Meaning |
| ---- | ------- |
| `DD0101` | The document carries no extractable text |
| `DD0201` | Embedded images were disabled by request |
| `DD0301` | Rendered pages were requested but no rendering backend is available |
| `DD0302` | No page images were produced |
| `DD0303` | Page rendering was requested against a non-paginated format, so it does not apply |
| `DD0401` | The document format was not recognized |
| `DD0402` | No extractor is registered for the detected format |
| `DD0403` | No registered extractor is available in this environment |
| `DD0404` | The required capabilities are unavailable across all backends |
| `DD0405` | The requested extractor does not apply to this format |
| `DD0406` | The requested extractor is unavailable in this environment |
| `DD0501` | The scratch folder was refused |
| `DD0502` | The source document could not be read |
| `DD0601` | A candidate backend was unavailable |
| `DD0602` | An availability probe failed |
| `DD0701` | An artifact is absent without an explanation |
| `DD0702` | The result degraded because a capability was missing |
| `DD0703` | The selected extractor failed |
| `DD0710`–`DD0723` | Contract-verification findings (manifest, ledger, and resource reconciliation) |

The PDF packages' `PDF` and `PDFR` ranges:

| Code | Meaning |
| ---- | ------- |
| `PDF0001` | An embedded image used an encoding that could not be decoded |
| `PDF0002` | PNG output was requested but could not be honored |
| `PDF0003` | A page carried no text layer |
| `PDF0004` | A JPEG 2000 image was written unchanged |
| `PDFR0001` | A page could not be rendered |
| `PDFR0002` | A page was too large to rasterize |
| `PDFR0003` | The native rasterizer faulted |

The Word package's `WORD` range:

| Code | Meaning |
| ---- | ------- |
| `WORD0001` | The document carries no extractable text |
| `WORD0002` | The document is encrypted or password-protected and cannot be opened |
| `WORD0003` | A table with no cell content was skipped |
| `WORD0004` | A table's first row was used as the header though Word did not mark it one |
| `WORD0005` | Merged or nested table cells were flattened |
| `WORD0006` | An EMF or WMF vector image was written unchanged |
| `WORD0007` | PNG output was requested but this package ships no imaging stack |
| `WORD0008` | Tracked changes were rendered in the accepted-revisions view |
| `WORD0009` | A header or footer carrying only page-numbering fields was omitted from Document Control |
| `WORD0010` | The document embeds charts whose plotted data this backend does not read |

The Excel package's `XLSX` range:

| Code | Meaning |
| ---- | ------- |
| `XLSX0001` | The workbook contains no worksheets |
| `XLSX0002` | A worksheet carried no non-empty cells |
| `XLSX0003` | Embedded images include EMF or WMF vector metafiles, written unchanged with a readability caveat |
| `XLSX0004` | A chart was found whose cached data could not be read |
| `XLSX0005` | A chart carries no cached data points |
| `XLSX0006` | A chart's cached data exceeded the rendering bound and was truncated |

The PowerPoint package's `PPTX` range:

| Code | Meaning |
| ---- | ------- |
| `PPTX0001` | The presentation contains no slides |
| `PPTX0002` | Notes were read from every slide; the deck carries none |
| `PPTX0003` | Embedded images include EMF or WMF vector metafiles, written unchanged with a readability caveat |
| `PPTX0004` | A slide could not be rendered and was omitted |
| `PPTX0005` | The deck embeds charts whose plotted data this backend does not read |

The Visio package's `VISIO` range:

| Code | Meaning |
| ---- | ------- |
| `VISIO0001` | The drawing contains no pages |
| `VISIO0002` | A page carried no shapes with recoverable text and no connections |
| `VISIO0003` | Embedded images include EMF or WMF vector metafiles, written unchanged with a readability caveat |
| `VISIO0004` | A page could not be rendered and was omitted |
| `VISIO0005` | The convention by which a topology endpoint label is to be read |
| `VISIO0006` | How many connector endpoints resolved by text, by master type, and not at all |

# References

- [REF-1] Continuous Compliance Methodology (<https://github.com/demaconsulting/ContinuousCompliance>)
