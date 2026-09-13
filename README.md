# DocDown

[![GitHub forks][badge-forks]][link-forks]
[![GitHub stars][badge-stars]][link-stars]
[![GitHub contributors][badge-contributors]][link-contributors]
[![License][badge-license]][link-license]
[![Build][badge-build]][link-build]
[![Quality Gate][badge-quality]][link-quality]
[![Security][badge-security]][link-security]
[![NuGet Core][badge-nuget]][link-nuget]
[![NuGet Pdf][badge-nuget-pdf]][link-nuget-pdf]
[![NuGet Tool][badge-nuget-tool]][link-nuget-tool]

DocDown is a family of .NET libraries and a command-line tool that break documents into a
predictable scratch-folder layout for multimodal AI agents and other automation. It reports what was
read, what was counted, and any plain-language notes about steps that could not be completed; it
does not judge whether document content is acceptable.

## Terms Used Here

Three words appear throughout this README and the user guide, and they are not interchangeable:

- **Backend** — the component that reads one document format, or renders its pages. `docdown
  --list-backends` prints them, `summary.txt` names the one that ran in its *Backend* section, and a
  single format may be served by more than one, such as PowerPoint's managed backend and its
  automation backend. This is the primary term.
- **Format package** — the NuGet package that ships one or more backends for a format, such as
  `DemaConsulting.DocDown.Word`. You install format packages; you select backends.
- **Extractor** — the name the API and the CLI give a backend where an identifier is needed:
  `DocDownBuilder.AddExtractor`, `result.SelectedExtractor`, and the extractor id used to break
  selection ties. Read it as the code-level spelling of *backend*.

## Which Package Do I Need?

Install one format package per format you actually read. Each format package brings
`DemaConsulting.DocDown.Core` with it as a transitive dependency, so ordinary use never references
Core directly — you reference it yourself only when you are writing a backend of your own.

| Format | Package (all prefixed `DemaConsulting.`) | Optional extra | Platform note |
| --- | --- | --- | --- |
| PDF `.pdf` | `DocDown.Pdf` | `DocDown.Pdf.Rendering` for page images | Managed; the add-on has native binaries |
| Word `.docx` | `DocDown.Word` | — | Managed; every platform |
| Excel `.xlsx` | `DocDown.Excel` | — | Managed; a workbook is not paginated |
| PowerPoint `.pptx` | `DocDown.PowerPoint` | — | Slide images need Windows and PowerPoint |
| Visio `.vsdx`, `.vsdm` | `DocDown.Visio` | — | Page images need Windows and Visio |
| Any format, from a shell | `DocDown.Tool` | — | Global or local tool manifest install |
| Your own backend | `DocDown.Core` | — | Abstractions only; extracts nothing itself |

```bash
dotnet add package DemaConsulting.DocDown.Pdf            # .pdf
dotnet add package DemaConsulting.DocDown.Pdf.Rendering  # optional: rasterize PDF pages
dotnet add package DemaConsulting.DocDown.Word           # .docx
dotnet add package DemaConsulting.DocDown.Excel          # .xlsx
dotnet add package DemaConsulting.DocDown.PowerPoint     # .pptx
dotnet add package DemaConsulting.DocDown.Visio          # .vsdx, .vsdm
```

Install the command-line tool globally, or into a local tool manifest so the version travels with
the repository:

```bash
dotnet tool install -g DemaConsulting.DocDown.Tool          # global
dotnet tool install --local DemaConsulting.DocDown.Tool     # local tool manifest
```

Only `DemaConsulting.DocDown.Pdf.Rendering` carries native binaries (PDFium and SkiaSharp, via
PDFtoImage). A framework-dependent reference works on every supported runtime identifier; a
self-contained single-file build of that package must be published per runtime identifier with
`dotnet publish -r <rid>`.

## Quick Start

### Library

Register only the backend you need, extract, and branch on the outcome. Nothing else is discovered:
registration is explicit and reflection-free, which is what keeps single-file publishing viable.

```csharp
using System;
using System.Threading;
using DocDown.Core;
using DocDown.Word;

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
format package you reference. The same example works for a workbook or a deck with no other change.
The annotated full menu is in *Registering Several Backends*, below.

### Command Line

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

The last line of output is always the absolute path to `summary.txt`, so `docdown ... | tail -1`
gives you the file to feed to an agent. The notes line appears only when notes were recorded. Exit
codes are the whole contract:

| Exit code | Meaning |
| --- | --- |
| `0` | The output layout was written, whether or not notes were recorded |
| `1` | The document was unreadable, the scratch folder was refused, or an argument was bad |

An unreadable document prints the failure explanation on standard error instead of a summary path,
and exits `1`:

```text
DocDown Tool version 1.2.3
Copyright (c) DEMA Consulting

DocDown does not support the legacy binary Office formats, so 'ppt' cannot be extracted by any
DocDown package; only the modern XML-based Office formats are supported.
```

## Outcomes and Notes

Every extraction ends in exactly one of two outcomes:

- **`Produced`** — the invariant output layout was written under the scratch folder.
- **`Unreadable`** — the document could not be read, or the scratch folder was refused.
  `result.Failure.Explanation` says why, and the CLI exits `1`.

`result.Notes` may contain entries on a **produced** extraction: each note is a short factual message
about a step DocDown attempted but could not finish, such as pages that were not rendered because no
renderer was registered. Notes are not failures, and a produced extraction with notes still exits
`0`.

## Supported Formats

| Format | Extensions | Package | Text, tables, images | Page images |
| --- | --- | --- | --- | --- |
| PDF | `.pdf` | `DocDown.Pdf` | Yes | With `DocDown.Pdf.Rendering` |
| Word | `.docx` | `DocDown.Word` | Yes | No |
| Excel | `.xlsx` | `DocDown.Excel` | Yes | Not applicable; not paginated |
| PowerPoint | `.pptx` | `DocDown.PowerPoint` | Yes | Windows, with PowerPoint |
| Visio | `.vsdx`, `.vsdm` | `DocDown.Visio` | Yes | Windows, with Visio |

Not supported today:

| Format | Extensions | What happens |
| --- | --- | --- |
| Legacy binary Office | `.doc`, `.xls`, `.ppt`, `.vsd` | Recognized, then refused as unsupported (`Unreadable`) |
| HTML | `.html`, `.htm` | Recognized; `DocDown.Html` is planned and not yet available |
| Anything else | — | Reported as an unrecognized format rather than guessed at |

## The Output Contract

When DocDown produces an extraction, it writes the same five artifacts under the scratch folder:

```text
out/
├── summary.txt      # what was extracted and where — paste this into the model context window
├── manifest.json    # machine-readable twin of summary.txt
├── metadata.json    # what the document asserts about itself, with provenance
├── content.md       # textual content as markdown, linking into images/
├── images/          # extracted embedded images
└── pages/           # rendered page images, when requested and available
```

- **`summary.txt`** — the short human- and agent-readable summary of what was extracted and where,
  including the absolute scratch path. It opens with a plain-English gist, records the Scratch,
  Source, Detected, Extracted, and Status fields, then summarizes the backend, environment,
  document metadata, layout, what **was** extracted, and anything DocDown could not finish reading.
  Counts are explicit, including a plain `0` for features the backend genuinely looked for, such
  as PowerPoint speaker notes in a deck that has none.
- **`manifest.json`** — the machine-readable twin of `summary.txt`. Its schema version is now
  `2.0`; it carries a `notes` string array and preserves per-image provenance, content inventory,
  and metadata-facing details. The top-level contract is smaller and focused on the produced
  extraction record.
- **`metadata.json`** — what the document asserts about itself, with per-field provenance and blank
  values omitted.
- **`content.md`** — the extracted textual content as markdown, linking to extracted images.
- **`images/` and `pages/`** — extracted embedded images and optional rendered page images.

`summary.txt` stays compact because it is the artifact a user pastes into an LLM context window.
Its sections are header and gist, Scratch, Source, Detected, Extracted, Status, optional Failure,
Backend, Environment, Document metadata, Layout, What WAS extracted, and Could not read. There is
no "What was NOT extracted", "Completeness", or "Diagnostics" section.

DocDown reports extraction details in only two ways:

1. **Inventory counts** — what is present, including an explicit `0` for anything the backend
   genuinely checked.
2. **Plain-language notes** — short factual messages about a step DocDown attempted but could not
   complete. A note carries only a message and no extra classification or follow-up fields, and it
   never characterizes the document.

The scratch folder is an explicit boundary. DocDown writes fixed artifact names beneath that root
and refuses unsafe or unusable scratch targets rather than guessing at another location.

Backends are registered explicitly rather than discovered by reflection or assembly scanning, so
the command-line tool can be published as a single-file executable.

### What to expect

Extraction is best-effort and environment-dependent. `ExtractAsync` also has an overload taking a
`DocumentSource` instead of a path, for documents that are not on disk; both take the scratch
folder, an optional `ExtractionOptions`, and a `CancellationToken`. On produced extractions,
`result.Notes` is an `IReadOnlyList<ExtractionNote>`; notes replace the earlier absence-report
collection.

For paginated formats, if page rendering was requested but no renderer is available, the layout is
still produced and a note explains that pages were not rendered. For a non-paginated format such as
an Excel workbook, a page request is honored with silence. When no backend matches a detected format,
the failure explanation names the format and, for a well-known format, the format package that
provides its backend, or states that a legacy binary Office format is unsupported.

## Registering Several Backends

One engine can serve every format a host handles. The full menu is below — each line says what it
buys, so delete the lines you do not need:

```csharp
using System;
using System.Threading;
using DocDown.Core;
using DocDown.Excel;
using DocDown.Pdf;
using DocDown.Pdf.Rendering;
using DocDown.PowerPoint;
using DocDown.Visio;
using DocDown.Word;

var engine = new DocDownBuilder()
    .AddPdf()          // .pdf  - text, embedded images, metadata
    .AddPdfRendering() // .pdf  - page images (adds native binaries)
    .AddWord()         // .docx - text, tables, images; no page images
    .AddExcel()        // .xlsx - cells, formulas, charts; workbooks are never rendered
    .AddPowerPoint()   // .pptx - slide text and notes; slide images need PowerPoint
    .AddVisio()        // .vsdx, .vsdm - shape text and connections; page images need Visio
    .Build();

var options = new ExtractionOptions { RenderPages = true };

var result = await engine.ExtractAsync("manuals/sample-manual.pdf", "out", options, CancellationToken.None);

Console.WriteLine(result.Outcome);
Console.WriteLine($"Summary:  {result.SummaryPath}");
Console.WriteLine($"Manifest: {result.ManifestPath}");
Console.WriteLine($"Content:  {result.ContentPath}");
Console.WriteLine($"Images:   {result.ImagePaths.Count}, Pages: {result.PagePaths.Count}");
```

Register only the formats you need — each call is one visible edge to one package. Slide and page
images are produced by driving Microsoft Office over COM, so they are Windows-only and require that
application to be installed. Without it the managed backend still extracts the text, and
`summary.txt` records that pages were not rendered.

Selection stays deterministic: DocDown detects the format, keeps the backends that match it and are
available, prefers a page renderer when pages were requested and one is available, then breaks ties
by priority and extractor id.

## Command-Line Tool

`docdown` extracts a document into a scratch folder and prints the absolute path to the produced
`summary.txt`:

```bash
docdown --input report.pdf --scratch ./out
```

When output is produced, the tool prints:

```text
DocDown Tool version 1.2.3
Copyright (c) DEMA Consulting

Extraction produced the output layout.
2 note(s) recorded; see summary.txt for detail.
C:\work\out\summary.txt
```

The notes line is printed only when notes were recorded. On an unreadable document or a bad
argument, the tool prints the failure explanation and exits `1`.

Common options:

- `--input <file>` / `--scratch <dir>` — the document to extract and the folder to write into
- `--pages` / `--no-pages` — request rendered pages or disable them
- `--page-range <a-b>`, `--dpi <#>`, `--images <preserve|png>`, `--no-images`,
  `--max-image-dim <#>`, `--max-image-bytes <#>`, `--split <auto|single|part>` — extraction tuning
- `--overwrite <clean|overwrite>` — scratch folder policy (`clean` is the default)
- `--list-backends` — list registered backends as `<id> - <name>`, then `formats: ...` and
  `status: available|unavailable (reason)`
- `--validate [--results <file>.trx|.xml]` — run self-validation and optionally write TRX or JUnit
- `-v|--version`, `-?|-h|--help`, `--silent`, `--log <file>`, `--depth <#>` — the standard DEMA
  flags

Run `docdown --help` for the full list. Exit codes are `0` when output was produced, even if notes
were recorded, and `1` on an unreadable document or a bad argument.

## Documentation

Generated documentation includes:

- **Build Notes**: Release information and changes
- **User Guide**: Comprehensive usage documentation
- **Code Quality Report**: CodeQL and SonarCloud analysis results
- **Requirements**: Functional and non-functional requirements
- **Requirements Justifications**: Detailed requirement rationale
- **Trace Matrix**: Requirements to test traceability

## API Documentation

Detailed API documentation for all public types and members is distributed in the `api/` folder
of each NuGet package. `DocDownBuilder`, `DocDownEngine.ExtractAsync`, and every `Add…()`
registration method carry a complete, runnable example, so the sample is available offline with the
package you installed.

## Features

- **Uniform Output Layout**: The same five artifacts for every produced extraction
- **Inventory-First Reporting**: Explicit counts, including `0` for features the backend checked
- **Plain Notes**: Message-only extraction notes with no diagnostic taxonomy or acceptability
  judgment
- **Deterministic Backend Selection**: Detect format, keep available backends, prefer a page renderer
  when pages were requested and one is available, then break ties by priority and extractor id
- **PDF Extraction**: Text in reading order, embedded images, and document metadata, with no native
  dependency in the base package
- **Word Extraction**: Text, genuine GitHub-Flavored-Markdown tables, embedded images, a Document
  Control section drawn from headers and footers, and document metadata for `.docx`
- **Excel Extraction**: Every worksheet's cell values reproduced verbatim at full length, formulas
  preserved, chart caches surfaced as data tables, and embedded images linked from `content.md`
- **PowerPoint Extraction**: Slide text, titles, speaker notes, and embedded images linked from
  `content.md` at each slide that shows them
- **Visio Extraction**: Page names, shape text, connector topology, and embedded images linked from
  `content.md` under the page that shows them
- **Optional Page Rendering**: PDF pages, PowerPoint slides, and Visio pages can be rasterized when
  the matching renderer is available and pages were requested
- **Per-Field Metadata Provenance**: `metadata.json` records where each metadata value came from
- **Reflection-Free Registration**: Explicit backend registration, suitable for single-file
  publishing
- **Multi-Platform Support**: Builds and runs on Windows, Linux, and macOS
- **Multi-Runtime Support**: Targets .NET 8, 9, and 10
- **xUnit v3**: Modern unit testing with xUnit framework version 3
- **Comprehensive CI/CD**: GitHub Actions workflows with quality checks and builds
- **Linting Enforcement**: markdownlint, cspell, and yamllint enforced on every CI run
- **Continuous Compliance**: Compliance evidence generated automatically on every CI run, following
  the [Continuous Compliance][link-continuous-compliance] methodology
- **SonarCloud Integration**: Quality gate and security analysis on every build
- **Documentation Generation**: Automated build notes, user guide, code quality reports,
  requirements, justifications, and trace matrix
- **Requirements Traceability**: Requirements linked to passing tests with an auto-generated trace
  matrix

## Project Status

Eight packages are implemented and under active development: `DemaConsulting.DocDown.Core`, the
shared abstractions and output contract; the five format packages and the one rendering add-on
listed in the package table above; and `DemaConsulting.DocDown.Tool`, the `docdown` command-line
tool. `DocDown.Html` is planned and not yet available.

`DocDown.Pdf` is fully managed and ships no native assets, so it does not render pages by itself.
When pages are requested without the rendering package, extraction still produces the layout and a
note says page rendering was not completed. `DocDown.PowerPoint` and `DocDown.Visio` extract on
every platform through managed backends and additionally rasterize slides and pages to PNG on
Windows when the corresponding Microsoft Office application is installed; without it, the managed
content is still produced and, when page rendering was requested, a note records that the request
could not be completed.

## Contributing

Contributions are welcome. See [CONTRIBUTING.md][link-contributing] for development setup, coding
standards, and the pull request process.

## License

Copyright (c) DEMA Consulting. Licensed under the MIT License. See [LICENSE][link-license] for
details.

By contributing to this project, you agree that your contributions will be licensed under the MIT
License.

<!-- Badge References -->
[badge-forks]: https://img.shields.io/github/forks/demaconsulting/DocDown?style=plastic
[badge-stars]: https://img.shields.io/github/stars/demaconsulting/DocDown?style=plastic
[badge-contributors]: https://img.shields.io/github/contributors/demaconsulting/DocDown?style=plastic
[badge-license]: https://img.shields.io/github/license/demaconsulting/DocDown?style=plastic
[badge-build]: https://img.shields.io/github/actions/workflow/status/demaconsulting/DocDown/build_on_push.yaml
[badge-quality]: https://sonarcloud.io/api/project_badges/measure?project=demaconsulting_DocDown&metric=alert_status
[badge-security]: https://sonarcloud.io/api/project_badges/measure?project=demaconsulting_DocDown&metric=security_rating
[badge-nuget]: https://img.shields.io/nuget/v/DemaConsulting.DocDown.Core?style=plastic
[badge-nuget-pdf]: https://img.shields.io/nuget/v/DemaConsulting.DocDown.Pdf?style=plastic
[badge-nuget-tool]: https://img.shields.io/nuget/v/DemaConsulting.DocDown.Tool?style=plastic

<!-- Link References -->
[link-forks]: https://github.com/demaconsulting/DocDown/network/members
[link-stars]: https://github.com/demaconsulting/DocDown/stargazers
[link-contributors]: https://github.com/demaconsulting/DocDown/graphs/contributors
[link-license]: https://github.com/demaconsulting/DocDown/blob/main/LICENSE
[link-build]: https://github.com/demaconsulting/DocDown/actions/workflows/build_on_push.yaml
[link-quality]: https://sonarcloud.io/dashboard?id=demaconsulting_DocDown
[link-security]: https://sonarcloud.io/dashboard?id=demaconsulting_DocDown
[link-nuget]: https://www.nuget.org/packages/DemaConsulting.DocDown.Core
[link-nuget-pdf]: https://www.nuget.org/packages/DemaConsulting.DocDown.Pdf
[link-nuget-tool]: https://www.nuget.org/packages/DemaConsulting.DocDown.Tool
[link-continuous-compliance]: https://github.com/demaconsulting/ContinuousCompliance
[link-contributing]: https://github.com/demaconsulting/DocDown/blob/main/CONTRIBUTING.md
