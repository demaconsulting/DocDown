# DocDown

<!-- IMPORTANT: All links in this file must be absolute URLs.
     This file is distributed in packages and relative links will not resolve. -->

[![GitHub forks](https://img.shields.io/github/forks/demaconsulting/DocDown?style=plastic)](https://github.com/demaconsulting/DocDown/network/members)
[![GitHub stars](https://img.shields.io/github/stars/demaconsulting/DocDown?style=plastic)](https://github.com/demaconsulting/DocDown/stargazers)
[![GitHub contributors](https://img.shields.io/github/contributors/demaconsulting/DocDown?style=plastic)](https://github.com/demaconsulting/DocDown/graphs/contributors)
[![License](https://img.shields.io/github/license/demaconsulting/DocDown?style=plastic)](https://github.com/demaconsulting/DocDown/blob/main/LICENSE)
[![Build](https://img.shields.io/github/actions/workflow/status/demaconsulting/DocDown/build_on_push.yaml)](https://github.com/demaconsulting/DocDown/actions/workflows/build_on_push.yaml)
[![Quality Gate](https://sonarcloud.io/api/project_badges/measure?project=demaconsulting_DocDown&metric=alert_status)](https://sonarcloud.io/dashboard?id=demaconsulting_DocDown)
[![Security](https://sonarcloud.io/api/project_badges/measure?project=demaconsulting_DocDown&metric=security_rating)](https://sonarcloud.io/dashboard?id=demaconsulting_DocDown)
[![NuGet](https://img.shields.io/nuget/v/DemaConsulting.DocDown.Core?style=plastic)](https://www.nuget.org/packages/DemaConsulting.DocDown.Core)

## Overview

DocDown is a family of .NET libraries and a command-line tool that extract useful information from
documents (PDF, Word, Excel, PowerPoint, Visio) into a scratch folder, in a predictable layout
designed to be fed to multimodal AI agents. It reports what was read, what was counted, and any
plain-language notes about steps that could not be completed; it does not judge whether document
content is acceptable.

## Features

- 📄 **Many Formats** - PDF, Word, Excel, PowerPoint, and Visio
- 📦 **Invariant Output Layout** - `summary.txt`, `manifest.json`, `metadata.json`, `content.md`,
  `images/`, and `pages/` for every document
- 🖼️ **Page Images** - Optional fully managed rendering of PDF pages, slides, and Visio pages
- 💬 **Review Comments** - `review-comments.md` written when the document carries reviewer comments
- 📝 **Honest Reporting** - Anything that could not be extracted is reported with a reason, never
  silently omitted
- 🧩 **Pluggable Backends** - Explicit, reflection-free registration; write your own backend on the
  Core abstractions
- 🌐 **Cross-Platform** - Fully managed libraries for .NET 8, 9, and 10; the tool runs on .NET 10
- ✅ **Self-Validation** - Built-in qualification tests for regulated environments

## Installation

### Libraries

Install one format package per format you read; each brings `DemaConsulting.DocDown.Core` with it.

```bash
dotnet add package DemaConsulting.DocDown.Pdf      # .pdf
dotnet add package DemaConsulting.DocDown.Office   # .docx, .xlsx, .pptx, .vsdx, .vsdm
```

Page images come from optional add-on packages: `DemaConsulting.DocDown.Pdf.Rendering`,
`DemaConsulting.DocDown.PowerPoint.Rendering`, and `DemaConsulting.DocDown.Visio.Rendering`.

### Command-Line Tool

Requires the [.NET 10 SDK or runtime](https://dotnet.microsoft.com/download).

```bash
dotnet tool install --global DemaConsulting.DocDown.Tool
docdown --version
```

Or install into a local tool manifest so the version travels with the repository:

```bash
dotnet new tool-manifest  # if you don't have a tool manifest already
dotnet tool install DemaConsulting.DocDown.Tool
dotnet docdown --version
```

## Usage

### Command Line

```bash
docdown --input report.pdf --scratch ./out
```

The last line of output is the absolute path to the produced `summary.txt`, ready to hand to an
agent. The tool exits `0` when the output layout was written (even if notes were recorded) and `1`
when the document was unreadable or an argument was bad. Run `docdown --help` for all options, and
`docdown --validate` to run self-validation.

### Library

```csharp
using System;
using System.Threading;
using DemaConsulting.DocDown.Core;
using DemaConsulting.DocDown.Word;

var engine = new DocDownBuilder()
    .AddWord()
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

Console.WriteLine(result.SummaryPath);
return 0;
```

### Output Layout

```text
out/
├── summary.txt         # what was extracted and where - paste this into the model context window
├── manifest.json       # machine-readable twin of summary.txt
├── metadata.json       # what the document asserts about itself, with provenance
├── content.md          # textual content as markdown, linking into images/
├── review-comments.md  # reviewer comments - only when the document has any
├── images/             # extracted embedded images
└── pages/              # rendered page images, when requested and available
```

## User Guide

The DocDown User Guide, covering every format, the output contract, backend selection, and the tool
options, is available on the
[DocDown releases page](https://github.com/demaconsulting/DocDown/releases).

## Contributing

Contributions are welcome! Please see our
[Contributing Guide](https://github.com/demaconsulting/DocDown/blob/main/CONTRIBUTING.md) for
development setup, coding standards, and submission guidelines. Also review our
[Code of Conduct](https://github.com/demaconsulting/DocDown/blob/main/CODE_OF_CONDUCT.md).

For bug reports, feature requests, and questions, please use
[GitHub Issues](https://github.com/demaconsulting/DocDown/issues).

## License

This project is licensed under the MIT License — see the
[LICENSE](https://github.com/demaconsulting/DocDown/blob/main/LICENSE) file for details.

## Support

- 🐛 **Report Bugs**: [GitHub Issues](https://github.com/demaconsulting/DocDown/issues)
- 💡 **Request Features**: [GitHub Issues](https://github.com/demaconsulting/DocDown/issues)
- 🤝 **Contributing**: [Contributing Guide](https://github.com/demaconsulting/DocDown/blob/main/CONTRIBUTING.md)

## Project Practices

- 🔍 **Linting Enforcement** - markdownlint, cspell, and yamllint enforced on every CI run
- 📋 **Continuous Compliance** - Compliance evidence generated automatically on every CI run,
  following the [Continuous Compliance](https://github.com/demaconsulting/ContinuousCompliance) methodology
- ☁️ **SonarCloud Integration** - Quality gate and security analysis on every build
- 🔗 **Requirements Traceability** - Requirements linked to passing tests with auto-generated trace matrix

## Security

For security concerns and vulnerability reporting, please see our
[Security Policy](https://github.com/demaconsulting/DocDown/blob/main/SECURITY.md).

## Acknowledgements

DocDown is built with the following open-source projects:

- [.NET](https://dotnet.microsoft.com/) - Cross-platform framework for building applications
- [DocumentFormat.OpenXml](https://github.com/dotnet/Open-XML-SDK) - Office Open XML parsing
- [PdfPig](https://github.com/UglyToad/PdfPig) - Managed PDF parsing
- [CanvasNet](https://github.com/demaconsulting/CanvasNet) - Managed page rendering
- [DemaConsulting.TestResults](https://github.com/demaconsulting/TestResults) - TRX and JUnit XML test results library
