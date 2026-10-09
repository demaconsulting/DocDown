# Introduction

DocDown turns a document into a predictable scratch-folder layout that downstream agents and other
automation can consume. It reports extraction facts, counts, and plain-language notes about work it
could not complete; it does not make acceptability judgments about the document itself.

## Purpose

This document explains how to install, use, and interpret DocDown's libraries and command-line
tool.

## Scope

This user guide covers:

- installation of the libraries and the command-line tool
- the output contract and scratch-folder layout
- basic library usage and backend registration
- supported formats and their current extraction behavior
- the `docdown` command-line tool and its public options
- self-validation with `--validate`

It describes the API and tool surface that ship today. It does not duplicate the generated API
reference distributed with the NuGet packages.

## References

- [REF-1] Continuous Compliance Methodology
  (<https://github.com/demaconsulting/ContinuousCompliance>)

## Terms used in this guide

Three words recur throughout this guide, and they name different things:

- **Backend** — the component that reads one document format, or renders its pages. It is the
  primary term used here: `docdown --list-backends` prints the registered backends, `summary.txt`
  names the selected one in its *Backend* section, and one format may be served by more than one
  backend, as Visio is by its managed backend and its automation backend.
- **Format package** — the NuGet package that ships one or more backends for a format, such as
  `DemaConsulting.DocDown.Office`. You install format packages; DocDown selects a backend.
- **Extractor** — the spelling the API and the CLI use where a backend needs an identifier:
  `DocDownBuilder.AddExtractor`, `engine.Extractors`, `result.SelectedExtractor`, and the extractor
  id that breaks selection ties. Read it as the code-level name for a backend.

`DemaConsulting.DocDown.Core` holds the abstractions every backend implements. A format package
brings it along as a transitive dependency, so ordinary use never references Core directly; you
reference it yourself only when you are building a backend of your own.

# Quick Start

Two audiences, two short paths. Both produce the same output layout.

## Library consumer

Install the package for the format you read, register that one backend, extract, and branch on the
outcome:

```bash
dotnet add package DemaConsulting.DocDown.Office
```

```csharp
using System;
using System.Threading;
using DemaConsulting.DocDown.Core;
using DemaConsulting.DocDown.Word;

var engine = new DocDownBuilder()
    .AddWord() // .docx - text, tables, images; no page images
    .Build();

var result = await engine.ExtractAsync(
    documentPath: "contracts/sample-agreement.docx",
    scratchFolder: "scratch/sample-agreement",
    options: null,
    cancellationToken: CancellationToken.None);

if (result.Outcome == ExtractionOutcome.Unreadable)
{
    Console.Error.WriteLine(result.Failure?.Explanation);
    return 1;
}

Console.WriteLine(result.SummaryPath);  // paste this summary.txt into the model context window

foreach (var note in result.Notes)
{
    Console.WriteLine($"Note: {note.Message}");
}

return 0;
```

Swap `AddWord()` for `AddExcel()`, `AddPdf()`, `AddPowerPoint()`, or `AddVisio()` — one call per
format package you reference. Nothing else changes.

## Command-line user

Install the tool, run it once, and read the three things it tells you — the outcome, the note count,
and the path to paste into a model:

```bash
dotnet tool install -g DemaConsulting.DocDown.Tool
docdown --input sample-agreement.docx --scratch ./out
```

```text
DocDown Tool version 1.2.3
Copyright (c) DEMA Consulting

Extraction produced the output layout.
1 note(s) recorded; see summary.txt for detail.
/home/user/project/out/summary.txt
```

```bash
echo $?   # 0
```

Unless `--silent` is given, the last line is the absolute path to `summary.txt`, and the notes line
appears only when notes were recorded. `--silent` suppresses all console output, including that
path, so a caller using it must already know where the scratch folder is. Exit codes are `0` when
the output layout was written, whether or not notes
were recorded, and `1` when the document was unreadable, the scratch folder was refused, or an
argument was bad. On exit `1` the failure explanation is printed on standard error in place of the
summary path:

```text
DocDown Tool version 1.2.3
Copyright (c) DEMA Consulting

DocDown does not support the legacy binary Office formats, so 'ppt' cannot be extracted by any
DocDown package; only the modern XML-based Office formats are supported.
```

## Outcomes in one paragraph

An extraction ends as `Produced` — the invariant output layout was written — or `Unreadable` — the
document or the scratch folder could not be read, and the failure explanation says why. A produced
extraction may still carry notes: short factual messages about a step DocDown attempted but could
not finish. Notes are not failures. The rest of this guide fills in the detail behind those two
outcomes.

# The Output Contract

When DocDown produces an extraction, it writes the same five artifacts under the scratch folder,
plus a sixth that appears only when the document carries reviewer comments:

- **`summary.txt`** — the short human- and agent-readable summary of what was extracted and where,
  including the absolute scratch path. It opens with a plain-English gist and then records Scratch,
  Source, Detected, Extracted, Status, Backend, Environment, Document metadata, Layout, What WAS
  extracted, and Could not read. When an unreadable result is written to the layout, `summary.txt`
  also includes a Failure section. The Could not read section contains the recorded notes or the
  sentence `Nothing was left incomplete.`
- **`manifest.json`** — the machine-readable twin of `summary.txt`. Its schema version is `3.1`.
  It carries a `notes` string array, a `reviewComments` array, and preserves the content inventory,
  image provenance, and
  metadata-facing details. It describes the document that was extracted and nothing else: there is
  no `environment` block, no `requestedOptions` echo of the caller's own arguments, and no
  SHA-256 digests. `summary.txt` keeps its Environment section, which is where a reader looks to
  see which backend ran and what was available when a page image is missing.
- **`metadata.json`** — what the document asserts about itself, with per-field provenance and blank
  values omitted.
- **`content.md`** — the extracted textual content as markdown, linking to extracted images.
- **`review-comments.md`** — the reviewer comments and annotations the document carries, one entry
  per comment as `- **Author** (Location): Body`, in the order the document records them. This file
  is written **only** when the document actually has reviewer comments: an empty file would assert
  that a review happened and found nothing, which is a different claim from the document never
  having been commented on. When there are none, `summary.txt` says `not present - none were
  written` in its Layout block and `manifest.json` leaves `reviewCommentsPath` null, so you never
  have to guess whether a missing file means no review or a failed extraction. The `Location` is a
  single human-readable phrase composed by the backend that knows the format: Word uses
  `§Heading — "snippet…"`, Excel uses `Sheet1!B7`, PowerPoint uses `Slide 4`, and PDF uses
  `Page 12`. A comment the document leaves unattributed is reported as `**Unattributed**` rather
  than being dropped or given a guessed name. The `Body` is written with inline markdown characters
  escaped, so a remark containing `*` or `_` reads as the reviewer typed it rather than turning
  into emphasis; `manifest.json` records the same comment text exactly as the document holds it,
  without that escaping, because it is data rather than prose.

  Two scope limits are worth knowing. A Word comment whose only content is a picture or ink
  drawing cannot be rendered as text, so it is left out of the file and counted in an extraction
  note. And PowerPoint reads the classic comment grammar only: the newer persona-based "modern"
  comments are out of scope for now, and a deck carrying them says so in an extraction note naming
  how many there are, so you can tell such a deck from one nobody commented on.
- **`images/` and `pages/`** — extracted embedded images and optional rendered page images.

`summary.txt` is intentionally compact because it is the artifact a user pastes into an LLM context
window. There is no What was NOT extracted section, no Completeness section, and no Diagnostics
section.

DocDown reports extraction details in only two ways:

1. **Inventory counts** — what is present, including an explicit `0` for any feature the backend
   genuinely looked for. For example, a deck with no speaker notes still reports `0 sets of speaker
   notes`.
2. **Plain-language notes** — short factual messages about a step DocDown attempted but could not
   complete. A note carries only a message and no extra classification or follow-up fields, and it
   never characterizes the document.

In the API, `result.Notes` is an `IReadOnlyList<ExtractionNote>`, and each `ExtractionNote` exposes
only `Message`. Notes replace the earlier absence-report collection.

Outcomes are simple:

- **`ExtractionOutcome.Produced`** — the invariant output layout was written.
- **`ExtractionOutcome.Unreadable`** — the document could not be read or the scratch folder was
  refused. The failure explanation states why, and the CLI exits `1`.

Page rendering follows the same reporting model:

- For a paginated format, if `--pages` was requested but no renderer is available, DocDown still
  writes the layout and records a plain note that pages were not rendered.
- For a non-paginated format such as an Excel workbook, a page request is honored with silence.

The scratch folder is an explicit boundary. DocDown writes fixed artifact names beneath that root
and refuses unsafe or unusable scratch targets rather than guessing at another location.

Backends are registered explicitly rather than discovered by reflection or assembly scanning, so
a host decides exactly which backends are available and the command-line tool can be published as a
single-file executable.

# Project Status

Six packages are implemented and under active development:
`DemaConsulting.DocDown.Core`, which holds the shared abstractions and output contract;
`DemaConsulting.DocDown.Pdf`, which extracts PDFs; `DemaConsulting.DocDown.Pdf.Rendering`, an
optional add-on that rasterizes PDF pages to images; `DemaConsulting.DocDown.Office`, which extracts
Word documents, Excel workbooks, PowerPoint presentations and Visio drawings;
`DemaConsulting.DocDown.PowerPoint.Rendering`, an optional add-on that rasterizes PowerPoint slides
to images; and `DemaConsulting.DocDown.Tool`, the
`docdown` command-line tool.

`DemaConsulting.DocDown.Pdf` is fully managed and ships no native assets.
`DemaConsulting.DocDown.Pdf.Rendering` is also fully
managed, depending on the `DemaConsulting.CanvasNet.Pdf`/`DemaConsulting.CanvasNet` packages for
rasterization, so it carries no native assets either and is runtime-identifier agnostic.

The Word and Excel backends are fully managed and read `.docx` and `.xlsx` on every platform
with no native dependency. The PowerPoint backend is fully managed on every platform and can also
rasterize slides through `DemaConsulting.DocDown.PowerPoint.Rendering`, which depends on the
`DemaConsulting.CanvasNet.Pptx`/`DemaConsulting.CanvasNet`/`DemaConsulting.CanvasNet.Charts`
packages and so carries no native assets either. The Visio backend extracts on every platform
through a managed backend and additionally rasterizes pages to PNG on Windows when Microsoft Visio
is installed.

DocDown does not support the legacy binary Office formats (`.doc`, `.xls`, `.ppt`, `.vsd`). It
recognizes them and says so plainly instead of reporting an unrecognized file.
`DemaConsulting.DocDown.Html` is
planned and not yet available.

# Continuous Compliance

DocDown follows the
[Continuous Compliance](https://github.com/demaconsulting/ContinuousCompliance) methodology, which
ensures compliance evidence is generated automatically on every CI run.

## Key Practices

- **Requirements Traceability**: Every requirement is linked to passing tests, and a trace matrix is
  generated on each release.
- **Linting Enforcement**: markdownlint, cspell, and yamllint are enforced before any build
  proceeds.
- **Automated Audit Documentation**: Each release ships with generated requirements,
  justifications, trace matrices, and quality reports.
- **CodeQL and SonarCloud**: Security and quality analysis runs on every build.

# Installation

Install one format package per format you actually read. Each format package brings
`DemaConsulting.DocDown.Core` with it as a transitive dependency, so ordinary use never references
Core directly; install Core yourself only when you are building a backend of your own.

| Format | Package (all prefixed `DemaConsulting.DocDown.`) | Optional extra | Platform note |
| --- | --- | --- | --- |
| PDF `.pdf` | `Pdf` | `Pdf.Rendering` for page images | Fully managed; every platform |
| Word `.docx` | `Office` | — | Managed; every platform |
| Excel `.xlsx` | `Office` | — | Managed; a workbook is not paginated |
| PowerPoint `.pptx` | `Office` | `PowerPoint.Rendering` for slide images | Fully managed |
| Visio `.vsdx`, `.vsdm` | `Office` | — | Page images need Windows and Visio |
| Any format, from a shell | `Tool` | — | Global or local tool manifest install |
| Your own backend | `Core` | — | Abstractions only; extracts nothing itself |

## Supported and unsupported formats

| Format | Extensions | Package | Text, tables, images | Page images |
| --- | --- | --- | --- | --- |
| PDF | `.pdf` | `DemaConsulting.DocDown.Pdf` | Yes | With `DemaConsulting.DocDown.Pdf.Rendering` |
| Word | `.docx` | `DemaConsulting.DocDown.Office` | Yes | No |
| Excel | `.xlsx` | `DemaConsulting.DocDown.Office` | Yes | Not applicable; not paginated |
| PowerPoint | `.pptx` | `DemaConsulting.DocDown.Office` | Yes | With `DemaConsulting.DocDown.PowerPoint.Rendering` |
| Visio | `.vsdx`, `.vsdm` | `DemaConsulting.DocDown.Office` | Yes | Windows, with Visio |

Formats DocDown recognizes but does not extract today:

| Format | Extensions | What happens |
| --- | --- | --- |
| Legacy binary Office | `.doc`, `.xls`, `.ppt`, `.vsd` | Recognized, then refused as unsupported (`Unreadable`) |
| HTML | `.html`, `.htm` | Recognized; `DemaConsulting.DocDown.Html` is planned and not yet available |
| Anything else | — | Reported as an unrecognized format rather than guessed at |

## Installing the packages

Install the libraries using the .NET CLI:

```bash
dotnet add package DemaConsulting.DocDown.Core
dotnet add package DemaConsulting.DocDown.Pdf
```

To also rasterize PDF pages to images, add the optional rendering package. It depends on the fully
managed `DemaConsulting.CanvasNet.Pdf`/`DemaConsulting.CanvasNet` packages, so it carries no native
assets and is runtime-identifier agnostic like the rest of the library:

```bash
dotnet add package DemaConsulting.DocDown.Pdf.Rendering
```

To extract Word documents, add the Word package. Its Open XML backend is fully managed and reads a
`.docx` on every platform with no native dependency. The legacy binary `.doc` format is not
supported by DocDown; it is recognized and refused with a plain explanation:

```bash
dotnet add package DemaConsulting.DocDown.Office
```

To extract Excel workbooks, PowerPoint presentations, or Visio drawings, add the matching packages.
All three extract content on every platform with no native dependency: Excel reads `.xlsx`,
PowerPoint reads `.pptx`, and Visio reads `.vsdx` and `.vsdm`. The Visio package also renders pages
on Windows when Microsoft Visio is installed. When it is not, a page-rendering request still
produces the layout and records a note. The legacy binary `.xls`, `.ppt`, and `.vsd` formats are
not supported; each is recognized and refused with an explanation:

```bash
dotnet add package DemaConsulting.DocDown.Office
```

To also rasterize PowerPoint slides to images without depending on Windows or an installed
PowerPoint, add the optional rendering package. It depends on the fully managed
`DemaConsulting.CanvasNet.Pptx`/`DemaConsulting.CanvasNet`/`DemaConsulting.CanvasNet.Charts`
packages, so it carries no native assets and is runtime-identifier agnostic like the rest of the
library:

```bash
dotnet add package DemaConsulting.DocDown.PowerPoint.Rendering
```

Install the command-line tool globally, or into a local tool manifest so the version travels with
the repository:

```bash
dotnet tool install -g DemaConsulting.DocDown.Tool          # global
dotnet tool install --local DemaConsulting.DocDown.Tool     # local tool manifest
```

The tool requires a **.NET 10 runtime**. It is packaged for a single framework because a tool is
executed rather than referenced, so a second and third framework would add no reachable surface.
Every backend it carries, including PDF page rendering and PowerPoint slide rendering, is fully
managed and runtime-identifier agnostic, so one package installs and runs on every platform with a
.NET 10 runtime. The libraries are unaffected — they target .NET 8, 9, and 10 and are
platform-neutral, including the optional PDF and PowerPoint page renderers, so referencing them
never constrains your project to the tool's runtime.

## API Documentation

Detailed API documentation for all public types and members is distributed in the `api/` folder of
the NuGet package. `api/api.md` indexes the namespaces, each namespace page lists its types, and
each type page carries the signature, the summary prose, and links to each member. Read only as far
down as the question requires.

The command-line tool package is the one exception: `DemaConsulting.DocDown.Tool` has no public
API. Its interface is the command line documented below, so it ships no `api/` folder.

# Usage

`DemaConsulting.DocDown.Core` provides the extraction engine and the abstractions that define the
output contract. It extracts nothing on its own: a host registers one or more format-specific
backends with it. `DemaConsulting.DocDown.Pdf` and `DemaConsulting.DocDown.Office` are
those backends, and each is registered by an explicit `Add…()` call.

Selection is deterministic. DocDown detects the document format, keeps the backends that both match
that format and are available in the current environment, prefers a backend that can render pages
when pages were requested and a renderer is available, and then breaks any remaining tie by backend
priority and extractor id.

## Extracting a PDF

Register the PDF backend, build an engine, and extract:

```csharp
using System;
using System.Threading;
using DemaConsulting.DocDown.Core;
using DemaConsulting.DocDown.Pdf;

var engine = new DocDownBuilder()
    .AddPdf()
    .Build();

var result = await engine.ExtractAsync(
    documentPath: @"C:\documents\report.pdf",
    scratchFolder: @"C:\scratch\report",
    cancellationToken: CancellationToken.None);

if (result.Outcome == ExtractionOutcome.Unreadable)
{
    Console.Error.WriteLine(result.Failure?.Explanation);
    return 1;
}

Console.WriteLine(result.Outcome);     // Produced
Console.WriteLine(result.SummaryPath); // the summary.txt to paste into an LLM context

foreach (var note in result.Notes)
{
    Console.WriteLine(note.Message);
}

return 0;
```

`AddPdf()` is the entire registration surface for the managed PDF backend. Registration is explicit
rather than reflection-based, so the set of backends in an engine is exactly the set your code asked
for.

## What the PDF package provides, and what it does not

`DemaConsulting.DocDown.Pdf` extracts:

- **Text** — in reading order, so a multi-column page reads the way a person would read it rather
  than the way it was painted.
- **Embedded images** — written to `images/` and linked from `content.md`.
- **Document metadata** — the title, author, and page count the document declares.

It does **not** render pages to images on its own. The package is fully managed and ships no native
assets, which is what lets it run anywhere .NET runs. If you request rendered pages without the
rendering package registered, extraction still produces the layout and a note explains that rendered
page images need the separate PDF page-rendering package.

## Rendering pages with DemaConsulting.DocDown.Pdf.Rendering

`DemaConsulting.DocDown.Pdf.Rendering` is that separate package. Register it alongside the managed
PDF backend and request rendered pages:

```csharp
using System.Threading;
using DemaConsulting.DocDown.Core;
using DemaConsulting.DocDown.Pdf;
using DemaConsulting.DocDown.Pdf.Rendering;

var engine = new DocDownBuilder()
    .AddPdf()
    .AddPdfRendering()
    .Build();

var options = new ExtractionOptions { RenderPages = true, PageRenderDpi = 150 };

var result = await engine.ExtractAsync(
    @"C:\documents\report.pdf",
    @"C:\scratch\report",
    options,
    CancellationToken.None);
```

A few things are worth knowing:

- **Selection is still automatic.** For a PDF, DocDown prefers the page-rendering backend only when
  pages were requested and a renderer is available. Otherwise the lighter managed backend runs.
- **`--dpi` (or `PageRenderDpi`) controls resolution.** A higher DPI produces a larger, more
  detailed page image and a larger file. The default is 150.
- **Notes stay factual.** If a page cannot be rasterized, extraction still produces the layout and
  records a short note about what could not be completed.
- **Fully managed, like the rest of the library.** `DemaConsulting.DocDown.Pdf.Rendering` depends on the
  `DemaConsulting.CanvasNet.Pdf`/`DemaConsulting.CanvasNet` packages, which carry no native assets,
  so a reference works the same way on every supported platform and there is no RID-specific
  single-file publish concern.

## Notes you may see while reading PDFs

The PDF backends still tell you what happened plainly:

- **The PDF has no text layer.** A scan can still produce page images and embedded images, but a
  note states that the pages carried no extractable text.
- **An image could not be decoded.** The remaining images are still extracted, and a note records
  the image-decoding failure.
- **You chose an image-size limit.** Images skipped because of `MaxImageBytes` or
  `MaxImageDimensionPx` are called out plainly, so a user-chosen limit is not confused with a read
  failure.
- **The PDF is encrypted or corrupt.** The result is `Unreadable`, and the failure explanation says
  the document could not be opened.

## Image provenance

Every image `manifest.json` lists records how it was produced:

- `passthrough` — the bytes are exactly what the document stored. A JPEG embedded in a PDF is
  already a complete JPEG file, so it is copied out unchanged.
- `decodedToPng` — the document stored compressed samples rather than an image file, so the samples
  were decoded and re-encoded as PNG.

The distinction matters if you need an extracted image to stand for the original: only a
`passthrough` image is byte-for-byte what the document contained.

Each image also records where it is referenced, so `images/` stays connected to the narrative in
`content.md`:

- `sourcePages` lists every 1-based page, slide, or worksheet that references the image, sorted and
  distinct.
- `sourcePage` is a convenience alias for the first entry of `sourcePages`, kept for a consumer that
  reads only the scalar.
- `referencedByTemplate` is `true` when the image is reached only through a template container — a
  PowerPoint slide layout or master, or a Visio master — rather than a specific page.

Together these give an honest three-way distinction: an image referenced by a page, referenced only
via a template, or a true orphan referenced by nothing. A template-referenced image is never given a
fabricated page.

## Extracting a Word document

The Word backend in `DemaConsulting.DocDown.Office` reads Word documents:

```csharp
using System.Threading;
using DemaConsulting.DocDown.Core;
using DemaConsulting.DocDown.Word;

var engine = new DocDownBuilder()
    .AddWord() // .docx - text, tables, images; no page images
    .Build();

var result = await engine.ExtractAsync(
    documentPath: @"C:\documents\report.docx",
    scratchFolder: @"C:\scratch\report",
    cancellationToken: CancellationToken.None);
```

`AddWord()` registers the package's single Open XML backend. There is no second call and no second
backend: the result is the same fully managed, deterministic extraction on every platform.

## What the Word package provides, and what it does not

The Word backend extracts:

- **Text and structure** — headings, ordered and bulleted lists, inline emphasis, and links in
  document order.
- **Real tables** — a Word table becomes a genuine GitHub-Flavored-Markdown table. Where merged or
  nested cells cannot be represented honestly in markdown, the flattened result is accompanied by a
  note.
- **A Document Control section** — identifying data taken from headers and footers and collected once
  near the start of `content.md`.
- **Embedded images** — written to `images/` and linked from `content.md`, with their bytes passed
  through unchanged.
- **Document metadata** — the title and author the document declares, and the page count the
  producer recorded.
- **Tracked changes** — rendered in the accepted-revisions view, with a note that a view was chosen.

It does **not** render pages to images. If pages are requested for a Word document, extraction still
produces the layout and records a note that page rendering was not completed.

It also does **not** expand a Word-embedded chart into plotted data. The surrounding document text,
tables, and images are still extracted, and a note can point a caller to the source workbook when
that chart data matters.

## Extracting an Excel workbook

The Excel backend in `DemaConsulting.DocDown.Office` reads Excel workbooks:

```csharp
using System.Threading;
using DemaConsulting.DocDown.Core;
using DemaConsulting.DocDown.Excel;

var engine = new DocDownBuilder()
    .AddExcel()
    .Build();

var result = await engine.ExtractAsync(
    documentPath: @"C:\documents\budget.xlsx",
    scratchFolder: @"C:\scratch\budget",
    cancellationToken: CancellationToken.None);
```

`AddExcel()` registers the package's single Open XML backend, `excel-openxml`. There is no second
backend, and that is deliberate rather than incidental: a workbook is not a paginated format, so
there is no rendering-oriented sibling to register.

## What the Excel package provides, and what it does not

The Excel backend extracts:

- **Every worksheet's cell values, verbatim and at full length** — nothing is truncated and nothing
  is summarized.
- **Formulas, preserved alongside the computed value** — a formula states the relationship, while
  the cached value records one evaluation of it.
- **A grid table for a dense rectangular region, in addition to the cell listing** — when populated
  cells form a table-shaped block, that block is also rendered as a markdown table so row and column
  relationships stay readable. The cell listing remains authoritative. A long or multi-line value can
  appear as `…` in the table while remaining intact in the listing, and merged ranges are stated
  plainly rather than forced into a misleading table shape.
- **Sheet identity** — each worksheet becomes its own content part carrying the sheet name, and each
  cell is cited by address.
- **Each chart's cached data series** — charts are written as data-bearing content parts, with the
  chart title, plot type, axis titles, series names, source ranges, and cached points.
- **Text on drawing shapes** — callouts, labels, and annotations floating over a worksheet are
  extracted under the worksheet that carries them.
- **Embedded images** — every picture embedded in a worksheet is written to `images/` with its true
  file extension and linked from the worksheet that shows it.
- **Document metadata** — the workbook's self-reported properties are written to `metadata.json`
  with provenance.

Excel does **not** render pages, because a workbook is not paginated. A `--pages` request is
honored with silence.

Two practical limits are still worth stating plainly:

- **A chart saved without cached values yields only its labels and source references.** The chart
  part says what was available, and a note can record that the plotted values were not present.
- **A chart table is bounded at 500 plotted points.** The content part says when that bound applies,
  so one very large chart does not crowd out the rest of the workbook.

An encrypted, malformed, or truncated workbook becomes `Unreadable`, and the failure explanation
says the workbook could not be opened.

## Extracting a PowerPoint presentation

The PowerPoint backend in `DemaConsulting.DocDown.Office` reads PowerPoint presentations:

```csharp
using System.Threading;
using DemaConsulting.DocDown.Core;
using DemaConsulting.DocDown.PowerPoint;

var engine = new DocDownBuilder()
    .AddPowerPoint()
    .Build();

var options = new ExtractionOptions { RenderPages = true };

var result = await engine.ExtractAsync(
    @"C:\documents\deck.pptx",
    @"C:\scratch\deck",
    options,
    CancellationToken.None);
```

`AddPowerPoint()` registers the managed Open XML backend `powerpoint-openxml`, the guaranteed
content path on every platform. It always states plainly that it provides no rendered pages.
Rendered slide images are a separate, opt-in concern — see "Rendering slides with
DemaConsulting.DocDown.PowerPoint.Rendering" below.

## What the PowerPoint package provides, and what it does not

The PowerPoint backend extracts:

- **Every slide's text, in presentation order** — each slide becomes its own section, under its
  own heading, in a single `content.md`.
- **Each slide's title** — read from the slide itself rather than inferred from surrounding text.
- **Speaker notes** — recovered from the file, not from a rendering. The number of slides carrying
  notes is always explicit, so a deck that genuinely carries none still reports `0 sets of speaker
  notes`.
- **Embedded images** — written to `images/`, deduplicated where the same picture is reused, and
  linked inline from the slides that show them. `sourcePages` records every referring slide, and an
  image reached only through a layout or master is flagged `referencedByTemplate`.

The honest limits are straightforward:

- **This package never renders a slide to an image.** If pages were requested and only this
  package is registered, the managed extraction still runs and a note records that slide rendering
  was not completed; register `DemaConsulting.DocDown.PowerPoint.Rendering` to obtain rendered pages.
- **Metadata is partial.** The package reports the slide count and can use the first slide's title,
  but it does not provide full authorship metadata.

## Rendering slides with DemaConsulting.DocDown.PowerPoint.Rendering

`DemaConsulting.DocDown.PowerPoint.Rendering` is the fully-managed, Windows-independent backend that
renders PowerPoint slides to images. Register it alongside the managed PowerPoint backend and
request rendered pages:

```csharp
using System.Threading;
using DemaConsulting.DocDown.Core;
using DemaConsulting.DocDown.PowerPoint;
using DemaConsulting.DocDown.PowerPoint.Rendering;

var engine = new DocDownBuilder()
    .AddPowerPoint()
    .AddPowerPointRendering()
    .Build();

var options = new ExtractionOptions { RenderPages = true, PageRenderDpi = 150 };

var result = await engine.ExtractAsync(
    @"C:\documents\deck.pptx",
    @"C:\scratch\deck",
    options,
    CancellationToken.None);
```

A few things are worth knowing:

- **Selection is still automatic.** DocDown prefers a backend that can render pages when pages were
  requested and one is available; the fully-managed rendering backend wins that tie over the
  guaranteed-content-only `powerpoint-openxml` backend by priority.
- **`--dpi` (or `PageRenderDpi`) controls resolution.** A higher DPI produces a larger, more
  detailed slide image and a larger file. The default is 150.
- **Notes stay factual.** If a slide cannot be rasterized, extraction still produces the layout and
  records a short note about what could not be completed; a failed slide count is recorded the same
  way and leaves the managed content untouched.
- **Fully managed, like the rest of the library.** `DemaConsulting.DocDown.PowerPoint.Rendering` depends on the
  `DemaConsulting.CanvasNet.Pptx`/`DemaConsulting.CanvasNet`/`DemaConsulting.CanvasNet.Charts`
  packages, which carry no native assets, so a reference works the same way on every supported
  platform and there is no RID-specific single-file publish concern.

## Extracting a Visio drawing

The Visio backend in `DemaConsulting.DocDown.Office` reads Visio drawings — both `.vsdx` and the macro-enabled `.vsdm`:

```csharp
using System.Threading;
using DemaConsulting.DocDown.Core;
using DemaConsulting.DocDown.Visio;

var engine = new DocDownBuilder()
    .AddVisio()
    .Build();

var result = await engine.ExtractAsync(
    documentPath: @"C:\documents\schematic.vsdm",
    scratchFolder: @"C:\scratch\schematic",
    cancellationToken: CancellationToken.None);
```

As with PowerPoint, `AddVisio()` registers two backends: the managed Open Packaging backend
`visio-openxml`, which needs no Visio installation, and `visio-com`, which can also rasterize pages
through Microsoft Visio.

## What the Visio package provides, and what it does not

The Visio backend extracts:

- **Page names and shape text**, in document order, for `.vsdx` and `.vsdm` alike.
- **Symbol-font glyphs recovered where the drawing proves what they are.** The backend corrects
  shape text only when the document's own font information proves the intended symbol.
- **Zero-information output suppressed, with counts stated plainly.** A bare callout number or a
  connector between two unnamed shapes is not promoted into a misleading narrative item.
- **Directed connector topology** — each page's connections are rendered as readable source-to-target
  relationships.
- **Honest endpoint labels** — labels come from the shape's own text when possible, otherwise from a
  clearly marked type-derived label or the shape id.
- **Rendered pages, when the automation backend is selected** — each foreground page is exported to
  `pages/` at the requested DPI.
- **Embedded images** — pictures stored in `ForeignData` shapes are written to `images/` with their
  true file extension and linked from the page that shows them. `sourcePages` records each referring
  page, and images reached only through a master are flagged `referencedByTemplate`.

The rendering limits match the PowerPoint package in spirit:

- **Rendering needs Windows and an installed Microsoft Visio.** If either is missing and pages were
  requested, the managed extraction still runs and a note records that page rendering was not
  completed.
- **A failed page is not fatal to the whole extraction.** Other pages still render, and a note
  identifies the page that could not be exported.
- **Background pages are not rendered.** The automation backend exports foreground pages only.
- **Metadata is thin.** The package reports the page count; it does not provide a full title and
  author record.

## Backends and how one is chosen

A host that handles every format registers the full menu. Each line says what it buys, so a host
that reads only some formats deletes the lines it does not need:

```csharp
using System;
using DemaConsulting.DocDown.Core;
using DemaConsulting.DocDown.Excel;
using DemaConsulting.DocDown.Pdf;
using DemaConsulting.DocDown.Pdf.Rendering;
using DemaConsulting.DocDown.PowerPoint;
using DemaConsulting.DocDown.PowerPoint.Rendering;
using DemaConsulting.DocDown.Visio;
using DemaConsulting.DocDown.Word;

var engine = new DocDownBuilder()
    .AddPdf()                 // .pdf  - text, embedded images, metadata
    .AddPdfRendering()        // .pdf  - page images (fully managed)
    .AddWord()                // .docx - text, tables, images; no page images
    .AddExcel()               // .xlsx - cells, formulas, charts; workbooks are never rendered
    .AddPowerPoint()          // .pptx - slide text and notes
    .AddPowerPointRendering() // .pptx - slide images (fully managed)
    .AddVisio()               // .vsdx, .vsdm - shape text and connections; page images need Visio
    .Build();

Console.WriteLine(engine.Extractors.Count); // 8 — AddVisio registers two backends
```

Register only the formats you need — each call is one visible edge to one package. PDF and
PowerPoint page images are produced by a fully-managed rasterizer with no platform restriction.
Visio page images are produced by driving Microsoft Visio over COM, so they are Windows-only and
require that application to be installed; without it the managed backend still extracts the text,
and `summary.txt` records that pages were not rendered.

Across all the format packages, `docdown --list-backends` reports eight registered backends:

| Backend id | Formats | Role |
| ---------- | ------- | ---- |
| `pdf` | `pdf` | Managed PDF backend |
| `pdf-rendering` | `pdf` | PDF backend with page rendering |
| `word-openxml` | `docx` | Managed Word backend |
| `excel-openxml` | `xlsx` | Managed Excel backend |
| `powerpoint-openxml` | `pptx` | Managed PowerPoint backend |
| `powerpoint-rendering` | `pptx` | PowerPoint slide renderer (fully managed) |
| `visio-openxml` | `vsdx`, `vsdm` | Managed Visio backend |
| `visio-com` | `vsdx`, `vsdm` | Visio page renderer on Windows |

`--list-backends` prints each backend in this shape:

```text
<id> - <name>
formats: ...
status: available
```

If a backend is not usable in the current environment, the last line becomes
`status: unavailable (reason)`. There is no extra feature-summary line.

The engine chooses exactly one backend per document. Selection is a pure function of the detected
format, the backends registered in the host, their availability in the current environment, and the
page-rendering request:

1. Detect the document format.
2. Keep the registered backends that apply to that format.
3. Keep the ones available in the current environment.
4. If pages were requested and a renderer is available, prefer a backend that can render pages.
5. Break any remaining tie by priority and then extractor id.

The practical consequences are:

- **A `visio-com` automation backend runs whenever pages were requested and Microsoft Visio is
  installed, in preference to the managed Visio backend.** Real Visio's own rendering fidelity is
  preferred over a managed renderer whenever it is genuinely available; when Visio is not
  installed, `visio-com` is simply unavailable and `visio-openxml` handles the extraction without
  rendered pages. PowerPoint has no COM backend and no environment dependency at all:
  `powerpoint-rendering` is fully managed, so it wins whenever pages are requested and it is
  registered, unconditionally.
- **A `.docx`, `.xlsx`, `.pptx`, `.vsdx`, or `.vsdm` is handled by a fully managed backend on every
  platform.** Page rendering is a fully-managed, every-platform add-on for PDFs and PowerPoint decks,
  and a Windows-only add-on for Visio drawings.
- **A `.doc`, `.xls`, `.ppt`, or `.vsd` is recognized as a legacy binary Office format and
  refused.** The failure explanation says so plainly.
- **When no backend matches, the failure explanation names the detected format.** For a well-known
  format it also names the format package that provides the backend, or states that legacy binary Office
  formats are unsupported.

# The Command-Line Tool

`docdown` drives the same engine from a shell or a pipeline. It registers its backends explicitly, so
it can be published as a single-file, trimmed executable.

## Extracting a document

```bash
docdown --input report.pdf --scratch ./out
```

When output is produced, the tool prints the absolute path to the produced `summary.txt` and exits
`0`:

```text
DocDown Tool version 1.2.3
Copyright (c) DEMA Consulting

Extraction produced the output layout.
2 note(s) recorded; see summary.txt for detail.
C:\work\out\summary.txt
```

The notes line appears only when notes were recorded. On an unreadable document or a bad argument,
the tool prints the failure explanation and exits `1`.

## Exit codes

- `0` — the extraction produced the output layout, even if notes were recorded.
- `1` — the extraction was unreadable, or an argument was invalid.

## Options

The tool provides the standard DEMA command-line vocabulary plus DocDown's extraction options. Run
`docdown --help` for the authoritative list.

| Option | Meaning |
| ------ | ------- |
| `--input <file>` | Document to extract (required for extraction) |
| `--scratch <dir>` | Scratch folder to write the extraction into (required) |
| `--pages` / `--no-pages` | Request or disable rendered page images |
| `--page-range <a-b>` | Restrict extraction to a page range |
| `--dpi <#>` | Page render DPI (range 36-1200) |
| `--no-images` | Do not extract embedded images |
| `--max-image-dim <#>` | Skip images exceeding this pixel dimension |
| `--max-image-bytes <#>` | Skip images exceeding this byte size |
| `--overwrite <clean\|overwrite>` | Scratch folder policy (`clean` is the default) |
| `--list-backends` | List registered backends with formats and availability |
| `--validate` | Run self-validation |
| `--results <file>` | Write validation results to a `.trx` or `.xml` file |
| `-v`, `--version` | Display version information |
| `-?`, `-h`, `--help` | Display help |
| `--silent` | Suppress console output |
| `--log <file>` | Write output to a log file |
| `--depth <#>` | Heading depth for markdown output (default 1) |

## Self-validation

`docdown --validate` runs the tool's own commands and the self-test cases every registered backend
contributes. It prints an environment header and per-test results and, when `--results` is given,
writes the outcome as TRX (`.trx`) or JUnit (`.xml`):

```bash
docdown --validate --results docdown-validate-windows.trx
```

If you use results files in a traceability pipeline, include the platform in the base file name so a
platform-filtered requirement can match it plainly, for example
`docdown-validate-windows.trx`, `docdown-validate-ubuntu.trx`, or
`docdown-validate-macos.trx`.

### What the cases cover

Each registered backend contributes its own cases. A case that cannot run in the current environment
is reported as `[SKIP]` with the reason, never as a failure, so `--validate` exits 0 on a machine
that legitimately cannot run it:

- `DocDownTool_Version` and `DocDownTool_Help` — the tool's own commands respond.
- `core.layout-invariance` and `core.manifest-schema` — every extraction writes the invariant output
  layout and a valid manifest.
- `pdf.parseRoundTrip`, `word.openxml.parseRoundTrip`, `visio.openxml.parseRoundTrip`,
  `powerpoint.openxml.parseRoundTrip`, and `excel.openxml.parseRoundTrip` — each managed backend
  reads an embedded document authored in the application whose format it reads.
- `pdf-rendering.renderRoundTrip` — the CanvasNet.Pdf rasterizer renders a page of an embedded PDF
  exported from Microsoft Word.
- `powerpoint-rendering.renderRoundTrip` — the CanvasNet.Pptx rasterizer renders a slide of an
  embedded PowerPoint presentation.
- `pdf.pageRendering`, `word.pageRendering`, `visio.pageRendering`, `powerpoint.pageRendering`, and
  `excel.pageRendering` — always skipped: these backends state that they do not render pages.
- `visio.com.available` — Microsoft Visio can be reached over COM on this machine.
- `visio.com.render` — Microsoft Visio renders the embedded drawing to a PNG through COM, and the
  Visio process the render started is gone afterwards.

Every case reads a document embedded in the package and authored in the application whose format it
exercises, rather than one DocDown wrote for itself to read: a backend that synthesized its own
document could only prove a library agreed with itself, never that it can read what the application
emits. The COM render case drives the same automation path an extraction uses and checks that a
real image of plausible size came back for every page. On a machine without Microsoft Visio —
including every non-Windows machine — the matching cases skip with a reason naming the missing
application.
