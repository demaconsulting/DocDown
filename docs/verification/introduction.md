# Introduction

This document provides the verification design for DocDown, a family of .NET libraries and a
command-line tool that extract useful information from documents of many types into a scratch
folder, in a predictable layout designed to be fed to multimodal AI agents.

## Purpose

The purpose of this document is to serve as the verification design entry point and document how
requirements will be tested across all software items in this repository, covering the
DocDown.Core, DocDown.Pdf, DocDown.Pdf.Rendering, DocDown.Tool, DocDown.Word, DocDown.Excel,
DocDown.PowerPoint, and DocDown.Visio systems. This
documentation enables formal review by mapping every requirement to named test scenarios, supports
compliance auditing by providing clear traceability from requirements through verification design
to tests, and ensures test completeness can be assessed without reading implementation code.

This document is intended for:

- Software developers implementing and maintaining tests
- Code reviewers validating test completeness against requirements
- Compliance auditors tracing requirements through verification design to tests
- Quality assurance teams validating test coverage and scenario adequacy

## Scope

This document covers the verification design for the DocDown systems and their
constituent software items, specifically:

- **DocDown.Core (System)** — Shared abstractions and the implementation of the output contract,
  organized into three subsystems
- **Detection (Subsystem)** — Identifies a document's format and the evidence behind the
  identification
  - **FormatSniffer (Unit)** — Names a format from the file extension, falling back to a
    leading-byte content signature
- **Extraction (Subsystem)** — Registration, deterministic selection, and pipeline orchestration
  - **DocDownBuilder (Unit)** — Fluent builder that collects registrations and produces an engine
  - **ExtractorRegistry (Unit)** — Immutable snapshot of registered extractors with cached
    availability
  - **ExtractorSelector (Unit)** — Pure ranking function that chooses the best available extractor
    deterministically
  - **DocDownEngine (Unit)** — Public facade that orchestrates one extraction end to end
- **Output (Subsystem)** — The sole write path: scratch preparation, path containment, and artifact
  serialization
  - **ScratchFolder (Unit)** — Owns the output directory and the path-safety gate
  - **ExtractionSink (Unit)** — Allocates every path, writes bytes, and records notes and the
    content inventory
  - **ContentWriter (Unit)** — Finalizes `content.md` and any `parts/` files
  - **SummaryWriter (Unit)** — Serializes the human-readable `summary.txt`
  - **ManifestWriter (Unit)** — Serializes `manifest.json` from the recorded output, inventory,
    and notes
- **DocDown.Pdf (System)** — PDF text, embedded-image, and document-metadata extraction; flat, with
  no subsystems
  - **PdfDocumentExtractor (Unit)** — The backend the engine selects: availability, metadata,
    delegation, the content inventory, and notes
  - **PdfTextExtractor (Unit)** — Renders a page's glyphs into markdown paragraphs in reading
    order, with image links and page markers
  - **PdfImageExtractor (Unit)** — Writes the embedded images, labeling how each was produced and
    accounting for every one it could not deliver
  - **PdfDocDownBuilderExtensions (Unit)** — The reflection-free registration seam
- **DocDown.Pdf.Rendering (System)** — Optional PDF page rendering (rasterization); flat, with no
  subsystems, and the first and only DocDown package that carries native binaries
  - **PdfPageRenderingExtractor (Unit)** — The page-rendering backend selected when rendering is
    requested and available: delegates the managed aspects, rasterizes pages, and records notes for
    pages it cannot render
  - **PageRenderer (Unit)** — The single native-interop seam: rasterizes one page to PNG behind a
    process-wide lock and answers a cheap, non-throwing availability probe
  - **PdfRenderingDocDownBuilderExtensions (Unit)** — The reflection-free registration seam, free of
    any native-rasterizer type
- **DocDown.Tool (System)** — The `docdown` command-line tool; two subsystems and one direct unit
  - **Program (Unit, direct)** — The entry point: priority-ordered dispatch, banner and help,
    explicit engine registration, extraction and reporting, and the auxiliary commands
  - **Cli (Subsystem)** — Command-line parsing, validation, option mapping, and output routing
    - **Context (Unit)** — The parsed arguments and the silence-aware console and log channels
  - **SelfTest (Subsystem)** — The `--validate` self-validation
    - **Validation (Unit)** — The validation driver: environment header, in-process command checks,
      the backend self-test union, and TRX/JUnit output
    - **SelfTestAdapter (Unit)** — Maps Core's dependency-free self-test records into the TestResults
      model
- **DocDown.Word (System)** — Word text, real tables, embedded-image, document-control, and
  document-metadata extraction; two subsystems and one direct unit
  - **WordDocDownBuilderExtensions (Unit, direct)** — The reflection-free registration seam for the
    Word backend
  - **Markdown (Subsystem)** — The reader-neutral document model and its projection onto markdown
    - **WordMarkdownWriter (Unit)** — Renders the model to a markdown flow: headings, lists, inline
      formatting, images, comments, footnotes, and the Document Control section
    - **WordTableWriter (Unit)** — Renders a table model as a GFM table, counting every flattened cell
    - **WordContentEmitter (Unit)** — The model-to-sink emission path
  - **OpenXml (Subsystem)** — The managed backend that reads a `.docx` through the Open XML SDK
    - **WordOpenXmlExtractor (Unit)** — The managed backend the engine selects and invokes,
      including page-rendering absence
    - **WordOpenXmlReader (Unit)** — Turns the Open XML DOM into the backend-neutral model
    - **WordOpenXmlImageReader (Unit)** — Yields each embedded image's bytes with passthrough
      provenance
- **DocDown.Excel (System)** — Workbook worksheet, cell-value (verbatim), formula, chart, drawing
  annotation, embedded-image, and document-metadata extraction; deliberately never renders; two
  subsystems and one direct unit
  - **ExcelDocDownBuilderExtensions (Unit, direct)** — The reflection-free registration seam for the
    Excel backend
  - **Markdown (Subsystem)** — The projection of the workbook model onto markdown
    - **ExcelContentEmitter (Unit)** — The model-to-sink emission path: sheet and chart parts,
      the verbatim listing, the additive grid table, inventory counts, and notes
    - **ExcelChartWriter (Unit)** — Renders a chart's cached data series as a bounded table
  - **OpenXml (Subsystem)** — The managed backend that reads an `.xlsx` through the Open XML SDK
    - **ExcelOpenXmlExtractor (Unit)** — The managed backend the engine selects and invokes,
      including page-rendering non-applicability
    - **ExcelOpenXmlReader (Unit)** — Turns the spreadsheet package into the backend-neutral model
    - **ExcelOpenXmlImageReader (Unit)** — Yields each embedded image's bytes and worksheet association
    - **ExcelChartReader (Unit)** — Recovers each chart's cached data series
    - **ExcelDrawingTextReader (Unit)** — Recovers the text of the drawing shapes over a worksheet
- **DocDown.PowerPoint (System)** — Slide text, slide-title, speaker-notes, slide-order, embedded-image,
  and document-metadata extraction, plus a rendered image of each slide when Microsoft PowerPoint is
  available; three subsystems (Com, Markdown, OpenXml) and one direct unit
  - **PowerPointDocDownBuilderExtensions (Unit, direct)** — The reflection-free registration seam for the
    PowerPoint backends
  - **Com (Subsystem)** — The rendering seam, active where Microsoft PowerPoint is installed
    - **PowerPointComExtractor (Unit)** — Delegates content to the managed backend and adds a rendered
      image of each slide over late-bound COM
    - **PowerPointComAvailability (Unit)** — The cheap, side-effect-free rendering-availability probe
    - **PowerPointAutomation (Unit)** — The real COM automation adapter, proven by release-time self-tests
  - **Markdown (Subsystem)** — The projection of the deck model onto markdown
    - **PowerPointContentEmitter (Unit)** — The model-to-sink emission path: per-slide title,
      text, speaker notes, inventory counts, and notes
  - **OpenXml (Subsystem)** — The guaranteed managed backend that reads a `.pptx` through the Open XML SDK
    - **PowerPointOpenXmlExtractor (Unit)** — The managed backend the engine selects and invokes,
      including the not-provided rendering fact
    - **PowerPointOpenXmlReader (Unit)** — Turns the presentation package into the backend-neutral model
    - **PowerPointOpenXmlImageReader (Unit)** — Yields each embedded image's bytes and slide association
- **DocDown.Visio (System)** — Page-name, shape-text, and directed-connector-topology extraction, plus
  embedded-image and document-metadata extraction and a rendered image of each page when Microsoft Visio
  is available; three subsystems (Com, Markdown, OpenXml) and one direct unit
  - **VisioDocDownBuilderExtensions (Unit, direct)** — The reflection-free registration seam for the
    Visio backends
  - **Com (Subsystem)** — The rendering seam, active where Microsoft Visio is installed
    - **VisioComExtractor (Unit)** — Delegates content to the managed backend and adds a rendered
      image of each page over late-bound COM
    - **VisioComAvailability (Unit)** — The cheap, side-effect-free rendering-availability probe
    - **VisioAutomation (Unit)** — The real COM automation adapter, proven by release-time self-tests
  - **Markdown (Subsystem)** — The projection of the drawing model onto markdown
    - **VisioContentEmitter (Unit)** — The model-to-sink emission path: per-page name, shape
      text, directed topology, inventory counts, and notes
    - **VisioShapeLabeler (Unit)** — Decides how each topology endpoint is named from what the drawing
      says about it
  - **OpenXml (Subsystem)** — The guaranteed managed backend that reads a `.vsdx`/`.vsdm` through
    `System.IO.Packaging`
    - **VisioOpenXmlExtractor (Unit)** — The managed backend the engine selects and invokes,
      including the not-provided rendering fact
    - **VisioPackageReader (Unit)** — Turns the Visio package into the backend-neutral model, resolving
      page names, shape text, and the directed topology
    - **VisioImageReader (Unit)** — Yields each embedded image's bytes and page association

Across these systems, an extraction either writes the invariant layout (`Produced`) or it does not
(`Unreadable`). Verification therefore checks two reporting surfaces inside produced output: the
content inventory, including deliberate zeros for looked-for content, and short notes about steps
DocDown attempted but could not complete.

The following OTS items are also covered:

- **BuildMark** — build-notes documentation tool
- **FileAssert** — document assertion tool
- **Open XML SDK** — managed WordprocessingML reader/writer, verified by transitive evidence from the
  DocDown.Word extraction tests rather than from a pipeline stage
- **Pandoc** — Markdown-to-HTML conversion tool
- **PdfPig** — managed PDF parser, verified by transitive evidence from the DocDown.Pdf test suites
  rather than from a pipeline stage
- **PDFium** — native page rasterizer, verified by transitive evidence from the DocDown.Pdf.Rendering
  render and probe tests
- **PDFtoImage** — managed page-rasterization API, verified by transitive evidence from the
  DocDown.Pdf.Rendering render tests
- **ReqStream** — requirements traceability tool
- **ReviewMark** — file review enforcement tool
- **SarifMark** — SARIF report conversion tool
- **SkiaSharp** — PNG encoder, verified by transitive evidence from the DocDown.Pdf.Rendering render
  and determinism tests
- **SonarMark** — SonarCloud quality report tool
- **SysML2Tools** — architecture model lint and diagram rendering tool
- **System.IO.Packaging** — managed OPC container reader, verified by transitive evidence from the
  DocDown.Word extraction tests
- **TestResults** — test-results serialization library, verified by transitive evidence from the
  DocDown.Tool `--validate` tests
- **VersionMark** — tool-version documentation tool
- **WeasyPrint** — HTML-to-PDF conversion tool
- **xUnit** — unit-testing framework

This verification documentation covers the same software items as the design documentation.

Version applicability: This verification design applies to all versions of DocDown.

The following topics are explicitly excluded from this verification documentation:

- The remaining format-specific extraction library (HTML), which is
  planned but not yet implemented
- Build pipeline and CI/CD process testing
- Infrastructure and hosting environment testing
- Test projects and test infrastructure, including the shared `DemaConsulting.DocDown.TestSupport` project

## Companion Artifact Structure

Each software item covered by this document has corresponding artifacts in parallel directory
trees. In-house items have artifacts in these parallel locations:

- Requirements: `docs/reqstream/{system}/.../{item}.yaml` (kebab-case)
- Design docs: `docs/design/{system}/.../{item}.md` (kebab-case)
- Verification design: `docs/verification/{system}/.../{item}.md` (kebab-case)
- Source code: `src/{System}/.../{Item}.cs` (PascalCase for C#)
- Tests: `test/{System}.Tests/.../{Item}Tests.cs` (PascalCase for C#)

OTS items have parallel artifacts in:

- Requirements: `docs/reqstream/ots/{ots-name}.yaml` (kebab-case)
- Verification: `docs/verification/ots/{ots-name}.md` (kebab-case)

Review-sets: defined in `.reviewmark.yaml`

## References

- DocDown User Guide — the compiled User Guide document for this repository.
- DocDown Repository — the DocDown source repository hosted on GitHub.
