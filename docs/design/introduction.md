# Introduction

This document provides the detailed design for DocDown, a family of .NET libraries and a
command-line tool that extract useful information from documents of many types into a scratch
folder, in a predictable layout designed to be fed to multimodal AI agents.

## Purpose

The purpose of this document is to serve as the design entry point and provide detailed design
specifications for the DocDown software. This documentation enables formal code review by
providing implementation specifications, supports compliance auditing by maintaining clear
traceability from requirements through design to code, aids maintenance by documenting system
structure and interactions, and ensures quality assurance through detailed technical
specifications.

This document is intended for:

- Software developers implementing and maintaining the system
- Code reviewers validating implementation against design
- Compliance auditors tracing requirements through design to implementation
- Quality assurance teams validating system behavior

## The Output Contract

The output contract is the invariant that motivates the decomposition of the whole product, so it
is stated here rather than inside any one system chapter.

Every extraction, regardless of source format, produces the same five artifacts in the scratch
folder:

- **`summary.txt`** — a human- and LLM-readable write-up of what was extracted and where,
  including the absolute path to the scratch folder.
- **`manifest.json`** — the machine-readable twin of `summary.txt`.
- **`metadata.json`** — what the document asserts about itself, with per-field provenance and blank
  values omitted; always written.
- **`content.md`** — the textual content as markdown, linking to the extracted images.
- **`images/` and `pages/`** — extracted image resources and optional rendered page images.

A sixth artifact is written **conditionally**:

- **`review-comments.md`** — the reviewer commentary the document carries, written only when the
  document actually carries reviewer comments or annotations.

The conditionality is deliberate and is itself part of the contract. Reviewer commentary is
editorial conversation *about* a document rather than the document's own content, so interleaving it
into `content.md` would force every consumer to filter one out of the other. An *empty*
`review-comments.md`, meanwhile, would assert that a review took place and produced nothing — a
different claim from the document never having been commented on. The absence of comments is
therefore reported as the absence of the file, and, because silence is not an answer, as an explicit
`not present - none were written` line in the `summary.txt` layout block and a null
`reviewCommentsPath` in `manifest.json`. A consumer can test for the file rather than parse it to
learn the same thing.

Each comment records its author (or states that the document records none), its body, and a
**location**: one precomputed, human-readable display string naming where in the document the comment
sits. It is deliberately not a structured address, because the in-scope formats locate a comment in
genuinely different terms, and any shared structure would be mostly-null fields that each consumer
would have to re-render anyway. The backend that knows the format composes the phrase once, in its
own vocabulary:

| Format | `Location` convention | Example |
| --- | --- | --- |
| Word | enclosing heading plus the commented-on words | `§Overview — "the system…"` |
| Excel | sheet and cell reference | `Sheet1!B7` |
| PowerPoint | slide ordinal | `Slide 4` |
| PDF | page ordinal | `Page 12` |

Three principles constrain every design decision in this document:

- **The output layout is invariant; an extraction is either produced or unreadable.** What can be
  extracted legitimately varies with the operating system and the installed applications. A
  consumer may therefore rely on the *shape* of produced output without treating the result as a
  quality grade. The one artifact whose *presence* varies, `review-comments.md`, varies with the
  document rather than with the environment, and its presence or absence is stated explicitly in
  both `summary.txt` and `manifest.json` — so the invariant a consumer relies on is preserved.
- **Reporting is factual and limited.** The content inventory counts what the extractor looked for,
  including deliberate zeros, and notes record only steps DocDown attempted but could not
  complete.
- **Extractors are registered explicitly.** No reflection or assembly scanning is used to
  discover extractors, so the command-line tool can be published as a single-file executable.

## Scope

This document covers the detailed design of the DocDown systems and their constituent software
items, specifically:

- **DemaConsulting.DocDown.Core (System)** — Shared abstractions and the implementation of the output contract,
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
  - **ImageTextSelector (Unit)** — Chooses which of the texts a document offered for an image is
    the most direct, so an image link carries the document's own words rather than invented ones
- **DemaConsulting.DocDown.Pdf (System)** — PDF text, embedded-image, and document-metadata extraction; flat, with
  no subsystems, because there is one architectural boundary here rather than several
  - **PdfDocumentExtractor (Unit)** — The backend the engine selects: availability, metadata,
    delegation, the content inventory, and notes
  - **PdfTextExtractor (Unit)** — Renders a page's glyphs into markdown paragraphs in reading
    order, with image links and page markers
  - **PdfImageExtractor (Unit)** — Writes the embedded images, labeling how each was produced and
    accounting for every one it could not deliver
  - **PdfDocDownBuilderExtensions (Unit)** — The reflection-free registration seam
- **DemaConsulting.DocDown.Pdf.Rendering (System)** — Optional PDF page rendering (rasterization); flat, with no
  subsystems, and fully managed like the rest of DocDown
  - **PdfPageRenderingExtractor (Unit)** — The page-rendering backend the engine selects when
    rendering is requested: delegates the managed aspects, rasterizes pages, and
    records notes for pages it cannot render
  - **PageRenderer (Unit)** — The single rasterization seam: opens a CanvasNet.Pdf document per
    call and rasterizes one page to a PNG
  - **PdfRenderingDocDownBuilderExtensions (Unit)** — The reflection-free registration seam,
    carrying no CanvasNet type on its surface
- **DemaConsulting.DocDown.Tool (System)** — The `docdown` command-line tool; two subsystems and one direct unit
  - **Program (Unit, direct)** — The entry point: priority-ordered dispatch, banner and help,
    explicit engine registration, extraction and reporting, and the auxiliary commands
  - **Cli (Subsystem)** — Command-line parsing, validation, option mapping, and output routing
    - **Context (Unit)** — The parsed arguments and the silence-aware console and log channels
  - **SelfTest (Subsystem)** — The `--validate` self-validation
    - **Validation (Unit)** — The validation driver: environment header, in-process command checks,
      the backend self-test union, and TRX/JUnit output
    - **SelfTestAdapter (Unit)** — Maps Core's dependency-free self-test records into the TestResults
      model
- **DemaConsulting.DocDown.Office (System)** — Word, Excel, PowerPoint, and Visio extraction; four format
  subsystems
  - **Word (Subsystem)** — Word text, real tables, embedded images, reviewer comments, footnotes,
    document-control content, and document metadata, through the managed Open XML SDK
  - **Excel (Subsystem)** — Worksheet cell values recorded verbatim, the formulas behind computed
    cells, cached chart data, drawing annotations, embedded images, and workbook metadata
  - **PowerPoint (Subsystem)** — Slide text, titles, speaker notes, slide order, embedded images,
    and metadata through a managed backend; slide images are a separate, opt-in concern provided by
    the `DemaConsulting.DocDown.PowerPoint.Rendering` system
  - **Visio (Subsystem)** — Page names, shape text, and directed-connector topology through a
    managed Open Packaging backend; page images are a separate, opt-in concern provided by the
    `DemaConsulting.DocDown.Visio.Rendering` system
- **DemaConsulting.DocDown.Visio.Rendering (System)** — Optional Visio page rendering (rasterization); flat, with no
  subsystems, and fully managed like the rest of DocDown
  - **VisioPageRenderingExtractor (Unit)** — The page-rendering backend the engine selects when
    rendering is requested: delegates the managed aspects, rasterizes pages, and records notes for
    pages it cannot render
  - **PageRenderer (Unit)** — The single rasterization seam: opens a CanvasNet.Vsdx document per
    call and rasterizes one page to a PNG
  - **VisioRenderingDocDownBuilderExtensions (Unit)** — The reflection-free registration seam,
    carrying no CanvasNet type on its surface

The following OTS items are also covered:

- **BuildMark** — build-notes documentation tool
- **CanvasNet** — fully-managed 2D canvas/codec library, a runtime dependency of the Pdf, PowerPoint, and
  Visio rendering packages used for PNG encoding
- **CanvasNet.Pdf** — fully-managed PDF rasterization API, the runtime dependency of
  DemaConsulting.DocDown.Pdf.Rendering used to rasterize pages. It and CanvasNet carry no native assets
- **CanvasNet.Vsdx** — fully-managed Visio rasterization API, the runtime dependency of
  DemaConsulting.DocDown.Visio.Rendering used to rasterize pages; it carries no native assets
- **FileAssert** — document assertion tool
- **Open XML SDK** — managed Open XML reader/writer, a runtime dependency of DemaConsulting.DocDown.Office
  shipped to consumers rather than a build-time tool
- **Pandoc** — Markdown-to-HTML conversion tool
- **PdfPig** — managed PDF parser, a runtime dependency of DemaConsulting.DocDown.Pdf shipped to consumers rather
  than a build-time tool
- **ReqStream** — requirements traceability tool
- **ReviewMark** — file review enforcement tool
- **SarifMark** — SARIF report conversion tool
- **SonarMark** — SonarCloud quality report tool
- **SysML2Tools** — architecture model lint and diagram rendering tool
- **System.IO.Packaging** — managed Open Packaging Conventions container reader, referenced and
  called directly by DemaConsulting.DocDown.Office to open an Office document's container
- **TestResults** — test-results serialization library, the one runtime dependency of DemaConsulting.DocDown.Tool
- **VersionMark** — tool-version documentation tool
- **WeasyPrint** — HTML-to-PDF conversion tool
- **xUnit** — unit-testing framework

Version applicability: This design applies to all versions of DocDown.

The following topics are explicitly excluded from this design documentation:

- The remaining format-specific extraction library (HTML), which is
  planned but not yet implemented; it will be added to this document as a system when it is
  delivered
- External library internals and third-party OTS components
- Build pipeline configuration and CI/CD processes
- Deployment, packaging, and distribution mechanisms
- Infrastructure and hosting environment details
- Test projects and test infrastructure

## Software Structure

The software structure is modeled in SysML2 under `docs/sysml2/` and rendered to the
diagram below by SysML2Tools as part of the build pipeline. AI agents should query the
SysML2 model directly (see the `sysml2tools-query` skill) rather than parsing this
diagram or the prose below.

![Software Structure](SoftwareStructureView.svg)

DemaConsulting.DocDown.Core is organized into three subsystems that form a one-directional pipeline: **Detection**
identifies a document's format, **Extraction** registers backends and selects the best available one
deterministically, and **Output** is the sole write path that produces the invariant scratch-folder
layout. Each subsystem is a distinct architectural boundary with its own public surface, and the
subsystems collaborate only through immutable value types. Eleven software units sit under these
subsystems; a larger set of supporting value, contract, and enumeration types — including
`ExtractionNote` — is documented inline within each subsystem's design document rather than as
separate units.

DemaConsulting.DocDown.Pdf sits alongside DemaConsulting.DocDown.Core as the second system and the
first real extraction backend. It
is flat - four units, no subsystems - because it spans one architectural boundary, the PDF, rather
than several. It depends on DemaConsulting.DocDown.Core for the extraction contract and on the PdfPig OTS parser for
PDF structure, and a host joins the two with a single explicit registration call.

DemaConsulting.DocDown.Pdf.Rendering is the third system: the optional page-rendering backend. It too is flat -
three units, no subsystems - because it spans one boundary, rasterization. It depends on
DemaConsulting.DocDown.Core for the contract and on DemaConsulting.DocDown.Pdf for the managed
text/image/metadata extraction it
delegates to, and it wraps the fully-managed CanvasNet.Pdf and CanvasNet OTS APIs in a single
rasterization seam. Because selection is deterministic, the rendering backend is chosen over the
managed backend only when page rendering is requested; rendering is always available, since
CanvasNet.Pdf carries no native assets.

DemaConsulting.DocDown.Tool is the fourth system: the `docdown` command-line tool, a thin executable shell over
DemaConsulting.DocDown.Core and the registered backends. It has two subsystems - Cli, which owns the command line,
and SelfTest, which drives the `--validate` self-validation - plus Program, the entry point, as a
direct unit. It registers its backends explicitly - `AddPdf().AddPdfRendering().AddWord()` - so it
can be published as a single-file executable. Every backend it carries, including PDF page
rendering, is fully managed and runtime-identifier agnostic, so both a framework-dependent
`dotnet tool install -g` and a self-contained single-file publish stay portable across platforms;
trimming and AOT are still left off as unverified for this dependency graph. It is the only package
that references the DemaConsulting.TestResults OTS library, which keeps DemaConsulting.DocDown.Core free of
runtime dependencies.

DemaConsulting.DocDown.Office is the fifth system: the Microsoft Office extraction backends, and the second family of
formats after PDF. It is one package holding four format subsystems — Word, Excel, PowerPoint and
Visio. Those four were four packages once. They share a
dependency, a release cadence and an audience, and splitting them bought a consumer nothing but four
references to keep in step. The
namespaces did not move, so `DemaConsulting.DocDown.Word` and its siblings still hold the same types.

Word and Excel are fully managed and read `.docx` and `.xlsx` on every platform with no native
dependency. PowerPoint is also fully managed: it reads `.pptx` text, structure, images and metadata
anywhere with no native dependency, and slide rendering is a separate, opt-in concern handled by the
`DemaConsulting.DocDown.PowerPoint.Rendering` system rather than by a backend in this package. Visio
is fully managed as well: it reads `.vsdx`/`.vsdm` text, structure, images and metadata anywhere with
no native dependency, and page rendering is a separate, opt-in concern handled by the
`DemaConsulting.DocDown.Visio.Rendering` system rather than by a backend in this package. The package
depends on DemaConsulting.DocDown.Core, the Open XML SDK and System.IO.Packaging; it ships no
native asset, so it stays runtime-identifier agnostic end to end. The legacy binary formats — `.doc`,
`.xls`, `.ppt` and `.vsd` — are not supported by DocDown at all.

## Folder Layout

The source folder structure mirrors the software structure. The SysML2 model under `docs/sysml2/`
is the authoritative record of that structure; this listing is a reading aid.

```text
DemaConsulting.DocDown.Core/
├── NamespaceDoc.cs                  — Documentation: the namespace summary ApiMark renders
├── Detection/
│   ├── DocumentFormat.cs                — Value type: format identifier and media type, with well-known formats
│   ├── FormatSniffer.cs                 — Unit: names a format from the file extension, falling back to a content signature
├── Extraction/
│   ├── DocDownBuilder.cs                — Unit: fluent builder collecting registrations and defaults
│   ├── DocDownEngine.cs                 — Unit: public facade that orchestrates one extraction end to end
│   ├── DocumentSource.cs                — Source: a file- or stream-backed document input
│   ├── ExtractionContext.cs             — Internal: the concrete extraction context
│   ├── ExtractionOptions.cs             — Options: mutable request configuration with a Clone method
│   ├── ExtractionResult.cs              — Result: the full outcome returned to the caller
│   ├── ExtractorRegistry.cs             — Unit: immutable snapshot of registered extractors with cached availability
│   ├── ExtractorSelector.cs             — Unit: pure ranking function that selects the best available extractor
│   ├── IDocumentExtractor.cs            — Interface: the backend contract implemented by extractor packages
│   ├── OpcMetadataMapper.cs             — Mapper: shared OPC core-property snapshot to DocumentMetadata
│   ├── SelfTestProbe.cs                 — Internal: reads a backend's embedded self-test probe document from its assembly
│   ├── SelfTestResult.cs                — Value type: a self-test status, message, and duration
├── Output/
│   ├── ArtifactInventory.cs             — Helper: the manifest-accounted file inventory used for safe scratch reuse
│   ├── ContentWriter.cs                 — Unit: finalizes content.md and any parts/ files
│   ├── EmbeddedImageWriter.cs           — Unit: writes an embedded image and its supporting value types
│   ├── ExtractionManifest.cs            — DTO graph: the manifest.json serialization model
│   ├── ExtractionSink.cs                — Unit: allocates paths, writes bytes, and records notes and content inventory
│   ├── ImageDimensions.cs               — Internal: reads pixel dimensions from image headers without decoding
│   ├── ImageLinkText.cs                 — Internal: the alt text policy for an inline markdown image link
│   ├── ImageTextSelector.cs             — Unit: chooses the most direct image text a document offered
│   ├── ManifestWriter.cs                — Unit: serializes manifest.json from recorded output, inventory, and notes
│   ├── MetadataWriter.cs                — Writer: serializes metadata.json from self-reported document metadata
│   ├── PartResourceLinkRewriter.cs      — Internal: rewrites resource links so they resolve from a content part's folder
│   ├── ScratchFolder.cs                 — Unit: owns the output directory and enforces safe scratch reuse
│   ├── SummaryWriter.cs                 — Unit: serializes the human-readable summary.txt

DemaConsulting.DocDown.Office/
├── OfficeDocDownBuilderExtensions.cs — Unit: registers every Office backend in one call
├── Excel/
│   ├── ExcelDocDownBuilderExtensions.cs — Unit: the reflection-free AddExcel registration seam
│   ├── NamespaceDoc.cs                  — Documentation: the namespace summary ApiMark renders
├── Excel/Markdown/
│   ├── ExcelChartWriter.cs              — Unit: renders a chart's cached series as a table
│   ├── ExcelContentEmitter.cs           — Unit: emits sheet and chart parts, inventory, and notes
│   ├── NamespaceDoc.cs                  — Documentation: the namespace summary ApiMark renders
├── Excel/OpenXml/
│   ├── ExcelChartReader.cs              — Unit: recovers each chart's cached data series
│   ├── ExcelDocumentModel.cs            — Value type: the reader-neutral workbook, sheet, and cell model
│   ├── ExcelDrawingTextReader.cs        — Unit: recovers the text of drawing shapes over a worksheet
│   ├── ExcelOpenXmlExtractor.cs         — Unit: the managed backend the engine selects and invokes
│   ├── ExcelOpenXmlImageReader.cs       — Unit: yields embedded image bytes and worksheet association
│   ├── ExcelOpenXmlReader.cs            — Unit: turns the spreadsheet package into the backend-neutral model
│   ├── NamespaceDoc.cs                  — Documentation: the namespace summary ApiMark renders
├── PowerPoint/
│   ├── NamespaceDoc.cs                  — Documentation: the namespace summary ApiMark renders
│   ├── PowerPointDocDownBuilderExtensions.cs — Unit: the reflection-free AddPowerPoint registration seam
├── PowerPoint/Markdown/
│   ├── NamespaceDoc.cs                  — Documentation: the namespace summary ApiMark renders
│   ├── PowerPointContentEmitter.cs      — Unit: the model-to-sink emission path, per-slide content, inventory, and notes
├── PowerPoint/OpenXml/
│   ├── NamespaceDoc.cs                  — Documentation: the namespace summary ApiMark renders
│   ├── PowerPointOpenXmlExtractor.cs    — Unit: the managed backend the engine selects and invokes
│   ├── PowerPointOpenXmlImageReader.cs  — Unit: yields embedded image bytes and slide association
│   ├── PowerPointOpenXmlReader.cs       — Unit: turns the presentation package into the backend-neutral model
├── Visio/
│   ├── NamespaceDoc.cs                  — Documentation: the namespace summary ApiMark renders
│   ├── VisioDocDownBuilderExtensions.cs — Unit: the reflection-free AddVisio registration seam
├── Visio/Markdown/
│   ├── NamespaceDoc.cs                  — Documentation: the namespace summary ApiMark renders
│   ├── VisioContentEmitter.cs           — Unit: the model-to-sink emission path, per-page content, inventory, and notes
│   ├── VisioShapeLabeler.cs             — Unit: the endpoint-labeling decision and its provenance types
├── Visio/OpenXml/
│   ├── NamespaceDoc.cs                  — Documentation: the namespace summary ApiMark renders
│   ├── VisioImageReader.cs              — Unit: yields embedded image bytes and page association
│   ├── VisioOpenXmlExtractor.cs         — Unit: the managed backend the engine selects and invokes
│   ├── VisioPackageReader.cs            — Unit: turns the Visio package into the backend-neutral model
├── Word/
│   ├── NamespaceDoc.cs                  — Documentation: the namespace summary ApiMark renders
│   ├── WordDocDownBuilderExtensions.cs  — Unit: the reflection-free AddWord registration seam
├── Word/Markdown/
│   ├── NamespaceDoc.cs                  — Documentation: the namespace summary ApiMark renders
│   ├── WordContentEmitter.cs            — Unit: the model-to-sink emission path
│   ├── WordDocumentModel.cs             — Value type: the reader-neutral document model
│   ├── WordMarkdownWriter.cs            — Unit: renders the document model to a markdown flow
│   ├── WordTableWriter.cs               — Unit: renders a table model as a GFM table, counting flattened cells
├── Word/OpenXml/
│   ├── NamespaceDoc.cs                  — Documentation: the namespace summary ApiMark renders
│   ├── WordOpenXmlExtractor.cs          — Unit: the managed backend the engine selects and invokes
│   ├── WordOpenXmlImageReader.cs        — Unit: yields embedded image bytes with passthrough provenance
│   ├── WordOpenXmlReader.cs             — Unit: turns the Open XML DOM into the backend-neutral model

DemaConsulting.DocDown.Pdf/
├── NamespaceDoc.cs                  — Documentation: the namespace summary ApiMark renders
├── PdfDocDownBuilderExtensions.cs   — Unit: the reflection-free AddPdf registration seam
├── PdfDocumentExtractor.cs          — Unit: orchestrates PDF extraction, content inventory, and notes
├── PdfImageExtractor.cs             — Unit: writes embedded images and records notes when one cannot be delivered
├── PdfTextExtractor.cs              — Unit: renders a page's glyphs into markdown in reading order

DemaConsulting.DocDown.Pdf.Rendering/
├── NamespaceDoc.cs                  — Documentation: the namespace summary ApiMark renders
├── PageRenderer.cs                  — Unit: the single rasterization seam over CanvasNet.Pdf
├── PdfPageRenderingExtractor.cs     — Unit: page-rendering backend; delegates, rasterizes, and records notes
├── PdfRenderingDocDownBuilderExtensions.cs — Unit: the reflection-free AddPdfRendering registration seam

DemaConsulting.DocDown.Tool/
├── Program.cs                       — Unit: entry point, priority dispatch, banner/help, extraction and reporting
├── Cli/
│   ├── Context.cs                       — Unit: parsed arguments, option mapping, and silence-aware console/log output
├── SelfTest/
│   ├── SelfTestAdapter.cs               — Unit: maps Core self-test records into the TestResults model
│   ├── Validation.cs                    — Unit: the --validate driver: header, in-process checks, self-test union, TRX/JUnit
```

## Code Coverage Policy

`[ExcludeFromCodeCoverage]` is applied only to interop adapters — the thin seams that call into
out-of-process applications and cannot be exercised in continuous integration.
It is never applied to decision logic, so that reported coverage remains an honest measure of what
the test suite actually verifies.

## Document Conventions

Throughout this document:

- Class names, method names, property names, and file names appear in `monospace` font.
- The word **shall** denotes a design constraint that the implementation must satisfy.
- Section headings within each unit chapter follow a consistent structure: overview, data model,
  methods/algorithms, and interactions with other units.
- Text tables are used in preference to diagrams, which may not render in all PDF viewers.

## Companion Artifact Structure

Each software item has corresponding artifacts in parallel directory trees:

- Requirements: `docs/reqstream/{system}/.../{item}.yaml` (kebab-case)
- Design docs: `docs/design/{system}/.../{item}.md` (kebab-case)
- Verification design: `docs/verification/{system}/.../{item}.md` (kebab-case)
- Source code: `src/{System}/.../{Item}.cs` (PascalCase for C#)
- Tests: `test/{System}.Tests/.../{Item}Tests.cs` (PascalCase for C#)
- SysML2 model: `docs/sysml2/model/{system}/.../{item}.sysml` (kebab-case)
- Review-sets: defined in `.reviewmark.yaml`

## References

- DocDown User Guide — the compiled User Guide document for this repository.
- DocDown Repository — the DocDown source repository hosted on GitHub.
