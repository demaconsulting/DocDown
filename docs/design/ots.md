# OTS Integration Design

This document describes the overall Off-The-Shelf (OTS) integration strategy for this repository.

## Overview

`DocDown.Core` itself has zero runtime NuGet dependencies — every subsystem is implemented
exclusively against the .NET Base Class Library (`System.Text.Json`, used for manifest serialization,
is in-box on the supported frameworks). Most OTS items listed below are build-time and
quality-pipeline tools, not runtime library dependencies: each provides one stage of the
documentation, requirements-traceability, testing, and quality-reporting pipeline invoked by
`build.ps1`, `lint.ps1`, and the `.github/workflows/build.yaml` CI workflow, and none of them is
linked into, or shipped with, a compiled NuGet package.

ApiMark is a partial exception worth stating precisely. It is a build-time tool like the others —
referenced with `PrivateAssets="All"`, contributing no assembly and no dependency to any package —
but unlike the others its *output* ships: the Markdown API reference it generates is packed into
the `api/` folder of each library's NuGet package. It is the tool that is absent from the shipped
artifact, not its product.

**PdfPig, PDFtoImage, the Open XML SDK, and TestResults are the exceptions, and deliberately so.**
PdfPig is a runtime library that `DocDown.Pdf` depends on and that therefore flows to consumers of
that package; PDFtoImage (with its transitive PDFium and SkiaSharp native stack) is the runtime
library the optional `DocDown.Pdf.Rendering` package depends on to rasterize pages; the Open XML SDK
(`DocumentFormat.OpenXml`, with its transitive `DocumentFormat.OpenXml.Framework` and
`System.IO.Packaging`) is the fully managed runtime library `DocDown.Word` depends on to read a
`.docx`; `TestResults` is a runtime library that `DocDown.Tool` uses to serialize its `--validate`
results. That difference changes what their integration designs must record — for PdfPig an exact
version pin, the frameworks it publishes assets for, the containment that keeps its types off a public
API, and the absence of native assets; for PDFtoImage the exact version pin, the deliberate presence
of native assets and their runtime-identifier resolution, PDFium's thread-unsafety, and the same
public-surface containment; for the Open XML SDK an exact version pin for restore determinism, and the
fact that it and its Framework companion are one independently-sourced component carrying no native
asset, unlike the separately-sourced pdfium and skiasharp; for TestResults the one-directional
dependency boundary that keeps it out of Core and Pdf — and it changes how they are verified: not from
a pipeline stage completing, but from the tests that exercise them on every run. Each is referenced
only by the one package that needs it, so `DocDown.Core` keeps its zero-runtime-NuGet-dependency
posture, `DocDown.Pdf` and `DocDown.Word` stay fully managed and runtime-identifier agnostic, and the
native stack lives only in the separate, opt-in rendering package.

## OTS Items

| OTS Item            | Purpose                                                                  |
| :------------------ | :----------------------------------------------------------------------- |
| ApiMark             | Generates the packaged gradual-disclosure Markdown API reference         |
| BuildMark           | Generates build-notes documentation from GitHub Actions metadata         |
| FileAssert          | Validates generated documents (HTML/PDF) against acceptance criteria     |
| Open XML SDK        | Reads WordprocessingML documents for the DocDown.Word extraction package |
| Pandoc              | Converts Markdown documentation to HTML                                  |
| PdfPig              | Parses PDF documents for the DocDown.Pdf extraction package              |
| PDFium              | Native page rasterizer for the DocDown.Pdf.Rendering package             |
| PDFtoImage          | Managed page-rasterization API for the DocDown.Pdf.Rendering package     |
| ReqStream           | Enforces requirements-to-test traceability                               |
| ReviewMark          | Enforces file review coverage and currency                               |
| SarifMark           | Converts CodeQL SARIF results into a markdown report                     |
| SkiaSharp           | Encodes rasterized pages to PNG for the DocDown.Pdf.Rendering package    |
| SonarMark           | Generates a SonarCloud quality report                                    |
| SysML2Tools         | Validates the SysML2 architecture model and renders its views to SVG     |
| System.IO.Packaging | Opens the OPC container of a .docx for the DocDown.Word package          |
| TestResults         | Serializes docdown self-validation results to TRX and JUnit              |
| VersionMark         | Captures and publishes tool-version information                          |
| WeasyPrint          | Converts HTML documentation to PDF                                       |
| xUnit               | Discovers and executes unit and integration tests                        |

Each item's individual design document (`docs/design/ots/{ots-name}.md`) records its Purpose,
Features Used, and Integration Pattern. Each item's requirements and verification evidence are
recorded in `docs/reqstream/ots/{ots-name}.yaml` and `docs/verification/ots/{ots-name}.md`
respectively.
