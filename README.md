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

## The Output Contract

When DocDown produces an extraction, it writes the same five artifacts under the scratch folder:

- **`summary.txt`** — the short human- and agent-readable summary of what was extracted and where,
  including the absolute scratch path. It opens with a plain-English gist, records the Scratch,
  Source, Detected, Extracted, and Status fields, then summarizes the backend, environment,
  document metadata, layout, what **was** extracted, and anything DocDown could not finish reading.
  Counts are explicit, including a plain `0` for features the extractor genuinely looked for, such
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

1. **Inventory counts** — what is present, including an explicit `0` for anything the reader
   genuinely checked.
2. **Plain-language notes** — short factual messages about a step DocDown attempted but could not
   complete. A note carries only a message and no extra classification or follow-up fields, and it
   never characterizes the document.

Outcomes are equally simple:

- **`Produced`** — the invariant output layout was written.
- **`Unreadable`** — the document could not be read or the scratch folder was refused. The failure
  explanation says why, and the CLI exits `1`.

The scratch folder is an explicit boundary. DocDown writes fixed artifact names beneath that root
and refuses unsafe or unusable scratch targets rather than guessing at another location.

Extractors are registered explicitly rather than discovered by reflection or assembly scanning, so
the command-line tool can be published as a single-file executable.

## Project Status

Eight packages are implemented and under active development:
`DemaConsulting.DocDown.Core`, the shared abstractions and output contract;
`DemaConsulting.DocDown.Pdf`, which extracts text, embedded images, and document metadata from
PDFs; `DemaConsulting.DocDown.Pdf.Rendering`, an optional add-on that rasterizes PDF pages to
images; `DemaConsulting.DocDown.Word`, which extracts text, real tables, embedded images, document
control, and metadata from Word documents; `DemaConsulting.DocDown.Excel`, which extracts every
worksheet's cell values, preserves formulas, recovers each chart's cached data series, and extracts
embedded images; `DemaConsulting.DocDown.PowerPoint`, which extracts slide text, titles, speaker
notes, and embedded images; `DemaConsulting.DocDown.Visio`, which extracts page names, shape text,
connector topology, and embedded images; and `DemaConsulting.DocDown.Tool`, the `docdown`
command-line tool.

`DocDown.Pdf` is fully managed and ships no native assets, so it does not render pages by itself.
When pages are requested without the rendering package, extraction still produces the layout and a
note says page rendering was not completed. `DocDown.Pdf.Rendering` is the one package that carries
native binaries (PDFium and SkiaSharp, via PDFtoImage): a framework-dependent install works on
supported runtime identifiers, but a self-contained single-file build is runtime-identifier
specific and must be published with `dotnet publish -r <rid>`.

`DocDown.Word` and `DocDown.Excel` are fully managed and read `.docx` and `.xlsx` on every platform
with no native dependency. Neither renders pages. `DocDown.PowerPoint` and `DocDown.Visio` extract
on every platform through managed backends and additionally rasterize slides and pages to PNG on
Windows when the corresponding Microsoft Office application is installed. Without it, the managed
content is still produced and, when page rendering was requested, a note records that the request
could not be completed. DocDown does not support the legacy binary Office formats (`.doc`, `.xls`,
`.ppt`, `.vsd`); it recognizes them and says so plainly. `DocDown.Html` is planned and not yet
available.

## Features

- **Uniform Output Layout**: The same five artifacts for every produced extraction
- **Inventory-First Reporting**: Explicit counts, including `0` for features the extractor checked
- **Plain Notes**: Message-only extraction notes with no diagnostic taxonomy or acceptability
  judgment
- **Deterministic Reader Selection**: Detect format, keep available readers, prefer a page renderer
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
- **Reflection-Free Registration**: Explicit extractor registration, suitable for single-file
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

## Installation

Install the libraries using the .NET CLI:

```bash
dotnet add package DemaConsulting.DocDown.Core
dotnet add package DemaConsulting.DocDown.Pdf
```

To extract Word documents, add the Word package. Its Open XML backend is fully managed and handles
`.docx` on every platform. The legacy binary `.doc` format is not supported by DocDown and is
refused with a plain explanation:

```bash
dotnet add package DemaConsulting.DocDown.Word
```

To extract Excel workbooks, PowerPoint decks, or Visio diagrams, add the matching packages. All
three are fully managed and extract content on every platform. PowerPoint and Visio additionally
rasterize slides and pages on Windows when the corresponding Microsoft Office application is
installed; when it is not, a page-rendering request still produces the layout and records a note.

```bash
dotnet add package DemaConsulting.DocDown.Excel
dotnet add package DemaConsulting.DocDown.PowerPoint
dotnet add package DemaConsulting.DocDown.Visio
```

To also rasterize PDF pages to images, add the optional rendering package. It carries native
binaries (PDFium and SkiaSharp), so a self-contained single-file build must be published per runtime
identifier (`dotnet publish -r <rid>`); a framework-dependent reference works on every supported
runtime:

```bash
dotnet add package DemaConsulting.DocDown.Pdf.Rendering
```

Install the command-line tool globally:

```bash
dotnet tool install -g DemaConsulting.DocDown.Tool
```

## Library Quick Start

Register the backends you want, run an extraction, and branch on the outcome. Registration is
explicit and reflection-free — you call `Add…()` once per format package you reference, and nothing
else is discovered or loaded — which is what keeps single-file publishing viable. Drop the lines for
formats you do not need.

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
    .AddPdf()
    .AddPdfRendering()
    .AddWord()
    .AddVisio()
    .AddPowerPoint()
    .AddExcel()
    .Build();

var options = new ExtractionOptions { RenderPages = true };

var result = await engine.ExtractAsync("report.pdf", "out", options, CancellationToken.None);

if (result.Outcome == ExtractionOutcome.Unreadable)
{
    Console.Error.WriteLine(result.Failure?.Explanation);
    return 1;
}

Console.WriteLine("Extraction produced the output layout.");
Console.WriteLine($"Summary:  {result.SummaryPath}");
Console.WriteLine($"Manifest: {result.ManifestPath}");
Console.WriteLine($"Content:  {result.ContentPath}");
Console.WriteLine($"Images:   {result.ImagePaths.Count}, Pages: {result.PagePaths.Count}");

foreach (var note in result.Notes)
{
    Console.WriteLine($"Note: {note.Message}");
}

return 0;
```

`ExtractAsync` also has an overload taking a `DocumentSource` instead of a path, for documents that
are not on disk; both take the scratch folder, an optional `ExtractionOptions`, and a
`CancellationToken`. On produced extractions, `result.Notes` is an
`IReadOnlyList<ExtractionNote>`; notes replace the earlier absence-report collection.

### What you get on disk

```text
out/
├── summary.txt      # what was extracted and where — paste this into the model context window
├── manifest.json    # machine-readable twin of summary.txt
├── metadata.json    # what the document asserts about itself, with provenance
├── content.md       # textual content as markdown, linking into images/
├── images/          # extracted embedded images
└── pages/           # rendered page images, when requested and available
```

`summary.txt` is the artifact you hand to the model: it names the absolute scratch folder path,
records the extraction status, inventories what is present, and lists any plain-language notes about
steps DocDown could not complete. `manifest.json` carries the same story in machine-readable form,
including `schemaVersion: "2.0"`, the `notes` array, and image provenance.

### What to expect

Extraction is best-effort and environment-dependent. `Produced` means the output layout was written;
`Unreadable` means the document or scratch folder could not be read and the failure explanation says
why. When `result.Notes` contains entries, each one is a short factual message about work DocDown
attempted but could not finish.

For paginated formats, if page rendering was requested but no renderer is available, the layout is
still produced and a note explains that pages were not rendered. For a non-paginated format such as
an Excel workbook, a page request is honored with silence. When no reader matches a detected format,
the failure explanation names the format and, for a well-known format, the package that provides its
extractor or states that a legacy binary Office format is unsupported.

For the full type and member reference, see [API Documentation](#api-documentation).

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
of the NuGet package. For a working end-to-end example, see
[Library Quick Start](#library-quick-start).

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
