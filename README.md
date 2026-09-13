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

DocDown is a family of .NET libraries and a command-line tool that extract useful information from
documents of many types into a scratch folder, in a predictable layout designed to be fed to
multimodal AI agents.

## The Output Contract

Every extraction, regardless of source format, produces the same five artifacts in the scratch
folder:

- **`summary.txt`** — a human- and LLM-readable write-up of what was extracted and where,
  including the absolute path to the scratch folder. It opens with a one-sentence plain-English
  description of the document, outlines what `content.md` contains (headings, tables, comments,
  speaker notes, and the like), and describes the extracted images in aggregate rather than one
  line each. This is what a user pastes into an LLM context window, so it is kept short; the
  per-image inventory and the full environment record live in `manifest.json`.
- **`manifest.json`** — the machine-readable twin of `summary.txt`, and the authoritative record
  of per-image provenance, the content outline, and every backend's availability.
- **`metadata.json`** — what the document asserts about itself (creator, dates, revision, and
  the like), with per-field provenance and blank values omitted. Always written; when the
  document supplied nothing readable it says so rather than emitting an empty object.
- **`content.md`** — the textual content as markdown, linking to the extracted images.
- **`images/` and `pages/`** — extracted image resources and optional rendered page images.

Two principles govern that contract:

- **The layout is invariant; the content is best-effort.** What can be extracted legitimately
  varies with the operating system, the installed applications, and the available native binaries.
- **Reporting is honest.** Whatever was *not* extracted is stated explicitly, with a reason. An
  absent folder never has to be interpreted.

Extractors are registered explicitly rather than discovered by reflection or assembly scanning, so
the command-line tool can be published as a single-file executable.

## Project Status

Eight packages are implemented and under active development: `DemaConsulting.DocDown.Core`, the
shared abstractions and the output contract; `DemaConsulting.DocDown.Pdf`, which extracts text,
embedded images, and document metadata from PDFs; `DemaConsulting.DocDown.Pdf.Rendering`, an
optional add-on that rasterizes PDF pages to images; `DemaConsulting.DocDown.Word`, which extracts
text, real tables, embedded images, document control, and metadata from Word documents;
`DemaConsulting.DocDown.Excel`, which extracts every worksheet's cell values, preserves formulas,
and extracts embedded images;
`DemaConsulting.DocDown.PowerPoint`, which extracts slide text, titles, speaker notes, and embedded
images; `DemaConsulting.DocDown.Visio`, which extracts page names, shape text, connector topology,
and embedded images; and
`DemaConsulting.DocDown.Tool`, the `docdown` command-line tool. `DocDown.Pdf` is fully managed and
ships no native assets, so it does not render pages by itself; requesting rendered pages without the
rendering package degrades with an explanation in `summary.txt`. `DocDown.Pdf.Rendering` is the one
package that carries native binaries (PDFium and SkiaSharp, via PDFtoImage): a framework-dependent
install works on every runtime identifier, but a self-contained single-file build is
runtime-identifier specific and must be published with `dotnet publish -r <rid>`. `DocDown.Word` and
`DocDown.Excel` are fully managed, with a single Open XML backend that reads a `.docx` or `.xlsx` on
every platform with no native dependency, and neither renders pages. `DocDown.PowerPoint` and
`DocDown.Visio` extract on every platform through a managed backend, and additionally rasterize
slides and pages to PNG on Windows when the corresponding Microsoft Office application is installed;
without it, the managed content is still delivered and the missing images are reported as a gap.
DocDown does not support the legacy binary Office formats (`.doc`, `.xls`, `.ppt`, `.vsd`); it
recognizes them and says so plainly rather than reporting an unrecognized file. `DocDown.Html` is
planned and not yet available.

## Features

- **Uniform Output Layout**: The same five artifacts for every supported source format
- **PDF Extraction**: Text in reading order, embedded images, and document metadata, with no native
  dependency of any kind
- **Word Extraction**: Text, genuine GitHub-Flavored-Markdown tables, embedded images, a Document
  Control section drawn from headers and footers, and document metadata — fully managed for `.docx`;
  the legacy binary `.doc` format is not supported
- **Excel Extraction**: Every worksheet's cell values reproduced verbatim at full length, with
  formulas preserved and embedded images extracted and linked from `content.md` under the worksheet
  that shows them — fully managed for `.xlsx`. A dense region also renders as a grid table; a value
  a Markdown table cannot carry is shown there as `…` and a note beside the table says so and points
  at the cell listing that holds the full value
- **PowerPoint Extraction**: Slide text, titles, speaker notes, and embedded images linked from
  `content.md` at each slide that shows them — fully managed for `.pptx`, with optional slide
  rasterization on Windows when PowerPoint is installed
- **Visio Extraction**: Page names, shape text, connector topology, and embedded images linked from
  `content.md` under the page that shows them, recovered without Visio installed — fully managed for
  `.vsdx` and the macro-enabled `.vsdm`, with optional page rasterization on Windows when Visio is
  installed. Multi-line shape text (part numbers, fittings lists, valve tables) stays intact inside
  its list item, symbol-font arrows are recovered where the shape's own font proves what they are,
  and content that carries no information — a bare callout number, an edge between two unnamed
  shapes — is omitted with the omitted count reported
- **Optional Page Rendering**: An opt-in package rasterizes PDF pages to PNG images when requested;
  it carries native binaries, so a self-contained single-file build is runtime-identifier specific
- **Honest Degradation**: Gaps in an extraction are enumerated with reasons, never silently omitted
- **Reflection-Free Registration**: Explicit extractor registration, suitable for single-file publishing
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
- **Requirements Traceability**: Requirements linked to passing tests with auto-generated trace matrix

## Installation

Install the libraries using the .NET CLI:

```bash
dotnet add package DemaConsulting.DocDown.Core
dotnet add package DemaConsulting.DocDown.Pdf
```

To extract Word documents, add the Word package. Its Open XML backend is fully managed and handles
`.docx` on every platform; the legacy binary `.doc` format is not supported by DocDown, and a `.doc`
is refused with an explanation that says so:

```bash
dotnet add package DemaConsulting.DocDown.Word
```

To extract Excel workbooks, PowerPoint decks, or Visio diagrams, add the matching packages. All
three are fully managed and extract content on every platform; PowerPoint and Visio additionally
rasterize slides and pages on Windows when the corresponding Microsoft Office application is
installed, and report a gap when it is not:

```bash
dotnet add package DemaConsulting.DocDown.Excel
dotnet add package DemaConsulting.DocDown.PowerPoint
dotnet add package DemaConsulting.DocDown.Visio
```

To also rasterize PDF pages to images, add the optional rendering package. It carries native
binaries (PDFium and SkiaSharp), so a self-contained single-file build must be published per runtime
identifier (`dotnet publish -r <rid>`); a framework-dependent reference works on every runtime:

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
using DocDown.Core;
using DocDown.Excel;
using DocDown.Pdf;
using DocDown.Pdf.Rendering;
using DocDown.PowerPoint;
using DocDown.Visio;
using DocDown.Word;

// Register only the format packages you reference. Registration is explicit and
// reflection-free, so the result can be published as a single file.
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

if (result.Outcome == ExtractionOutcome.Failed)
{
    // A structured explanation: headline, per-candidate verdicts, and a remedy.
    Console.Error.WriteLine(result.Failure?.Explanation);
    return 1;
}

// Succeeded or Degraded: the output layout exists and is complete in shape.
Console.WriteLine($"Outcome: {result.Outcome}");
Console.WriteLine($"Summary:  {result.SummaryPath}");   // paste this file into the model context
Console.WriteLine($"Manifest: {result.ManifestPath}");  // machine-readable twin of the summary
Console.WriteLine($"Content:  {result.ContentPath}");   // markdown text, links to images/
Console.WriteLine($"Images:   {result.ImagePaths.Count}, Pages: {result.PagePaths.Count}");

// Degraded is normal, not an error: every absence is reported with a reason.
foreach (var gap in result.Gaps)
{
    Console.WriteLine($"Gap: {gap.Target} - {gap.Reason}");
}

return 0;
```

`ExtractAsync` also has an overload taking a `DocumentSource` instead of a path, for documents that
are not on disk; both take the scratch folder, an optional `ExtractionOptions`, and a
`CancellationToken`. Adverse conditions come back as data on the result — only argument faults and
cancellation throw.

### What you get on disk

```text
out/
├── summary.txt      # what was extracted and where — paste this into the model context window
├── manifest.json    # machine-readable twin of summary.txt
├── metadata.json    # what the document asserts about itself (creator, dates, revision), with provenance
├── content.md       # textual content as markdown, linking into images/
├── images/          # extracted embedded images
└── pages/           # rendered page images, when requested and available
```

`summary.txt` is the artifact you hand to the model: it names the absolute scratch folder path and
every extracted piece, so the agent knows what exists and where to find it without guessing. Because
it competes with real document content for the model's context, it stays deliberately compact — it
opens with a one-line description, outlines what `content.md` holds rather than only how large it
is, and points at `manifest.json` for the per-image inventory and the availability of the backends
that did not run.

### What to expect honestly

Extraction is best-effort and environment-dependent. The layout above is invariant, but the content
is not: a `Degraded` outcome means some requested aspect could not be produced, and every such
absence appears in `result.Gaps` and in `summary.txt` with a reason. In particular, page rendering
for Visio and PowerPoint requires the corresponding Microsoft Office application installed on
Windows, and PDF page rasterization requires the optional `DemaConsulting.DocDown.Pdf.Rendering`
package. Without them, extraction still succeeds — it just reports the missing pages and says why.

For the full type and member reference, see [API Documentation](#api-documentation).

## Command-Line Tool

`docdown` extracts a document into a scratch folder and prints the absolute path to the produced
`summary.txt`:

```bash
docdown --input report.pdf --scratch ./out
```

On success (or a degraded extraction with reported gaps) the tool prints the absolute `summary.txt`
path and exits 0. On a failure it renders the structured explanation — a headline, the per-candidate
verdicts, and a remedy — rather than a stack trace, and exits 1.

Common options:

- `--input <file>` / `--scratch <dir>` — the document to extract and the folder to write into
- `--pages` / `--no-pages` / `--require-pages` — request rendered pages, disable them, or fail
  rather than degrade when they are unavailable
- `--page-range <a-b>`, `--dpi <#>`, `--images <preserve|png>`, `--no-images`,
  `--max-image-dim <#>`, `--max-image-bytes <#>`, `--split <auto|single|part>` — extraction tuning
- `--backend <id>` — force a specific extractor backend (never falls back)
- `--overwrite <require-empty|clean|overwrite|unique>` — scratch folder policy
- `--list-backends` — list registered backends with availability
- `--verify <dir>` — verify an existing scratch folder against its manifest
- `--validate [--results <file>.trx|.xml]` — run self-validation and optionally write TRX or JUnit
- `-v|--version`, `-?|-h|--help`, `--silent`, `--log <file>`, `--depth <#>` — the standard DEMA flags

Run `docdown --help` for the full list. Exit codes: `0` on success or a degraded extraction, `1` on
a failed extraction or a bad argument.

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

Copyright (c) DEMA Consulting. Licensed under the MIT License. See [LICENSE][link-license] for details.

By contributing to this project, you agree that your contributions will be licensed under the MIT License.

<!-- Badge References -->
[badge-forks]: https://img.shields.io/github/forks/demaconsulting/DocDown?style=plastic
[badge-stars]: https://img.shields.io/github/stars/demaconsulting/DocDown?style=plastic
[badge-contributors]: https://img.shields.io/github/contributors/demaconsulting/DocDown?style=plastic
[badge-license]: https://img.shields.io/github/license/demaconsulting/DocDown?style=plastic
[badge-build]: https://img.shields.io/github/actions/workflow/status/demaconsulting/DocDown/build_on_push.yaml?style=plastic
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
