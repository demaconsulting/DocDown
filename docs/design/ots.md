# OTS Integration Design

This document describes the overall Off-The-Shelf (OTS) integration strategy for this repository.

## Overview

`DemaConsulting.DocDown.Core` itself has zero runtime NuGet dependencies — every subsystem is implemented
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

**PdfPig, CanvasNet.Pdf, CanvasNet, the Open XML SDK, and TestResults are the exceptions, and
deliberately so.**
PdfPig is a runtime library that `DemaConsulting.DocDown.Pdf` depends on and that therefore flows to consumers of
that package; CanvasNet.Pdf and CanvasNet are the fully-managed runtime libraries the optional
`DemaConsulting.DocDown.Pdf.Rendering` package depends on to rasterize pages; the Open XML SDK
(`DocumentFormat.OpenXml`, with its transitive `DocumentFormat.OpenXml.Framework`) is the fully
managed runtime library `DemaConsulting.DocDown.Office` depends on to read an Office document, alongside
`System.IO.Packaging`, which it references directly to open an OPC container; `TestResults` is a
runtime library that `DemaConsulting.DocDown.Tool` uses to serialize its `--validate`
results. That difference changes what their integration designs must record — for PdfPig an exact
version pin, the frameworks it publishes assets for, the containment that keeps its types off a public
API, and the absence of native assets; for CanvasNet.Pdf and CanvasNet the exact version pin and the
same public-surface containment, and the confirmed absence of native assets in their own dependency
graph; for the Open XML SDK an exact version pin for restore determinism, and the
fact that it and its Framework companion are one independently-sourced component carrying no native
asset; for TestResults the one-directional
dependency boundary that keeps it out of Core and Pdf — and it changes how they are verified: not from
a pipeline stage completing, but from the tests that exercise them on every run. Each is referenced
only by the one package that needs it, so `DemaConsulting.DocDown.Core` keeps its zero-runtime-NuGet-dependency
posture, and `DemaConsulting.DocDown.Pdf`, `DemaConsulting.DocDown.Pdf.Rendering`, and
`DemaConsulting.DocDown.Office` all stay fully managed and
runtime-identifier agnostic.

## OTS Items

| OTS Item            | Purpose                                                                                  |
| :------------------ | :--------------------------------------------------------------------------------------- |
| ApiMark             | Generates the packaged gradual-disclosure Markdown API reference                         |
| BuildMark           | Generates build-notes documentation from GitHub Actions metadata                         |
| CanvasNet           | Fully-managed 2D canvas/codec library used for PNG encoding                              |
| CanvasNet.Pdf       | Fully-managed PDF rasterization API for the DemaConsulting.DocDown.Pdf.Rendering package |
| FileAssert          | Validates generated documents (HTML/PDF) against acceptance criteria                     |
| Open XML SDK        | Reads Office documents for the DemaConsulting.DocDown.Office extraction package          |
| Pandoc              | Converts Markdown documentation to HTML                                                  |
| PdfPig              | Parses PDF documents for the DemaConsulting.DocDown.Pdf extraction package               |
| ReqStream           | Enforces requirements-to-test traceability                                               |
| ReviewMark          | Enforces file review coverage and currency                                               |
| SarifMark           | Converts CodeQL SARIF results into a markdown report                                     |
| SonarMark           | Generates a SonarCloud quality report                                                    |
| SysML2Tools         | Validates the SysML2 architecture model and renders its views to SVG                     |
| System.IO.Packaging | Opens an Office document's OPC container for DemaConsulting.DocDown.Office               |
| TestResults         | Serializes docdown self-validation results to TRX and JUnit                              |
| VersionMark         | Captures and publishes tool-version information                                          |

| ApiMark             | Generates the packaged gradual-disclosure Markdown API reference                         |
| BuildMark           | Generates build-notes documentation from GitHub Actions metadata                         |
| CanvasNet           | Fully-managed 2D canvas/codec library used for PNG encoding                              |
| CanvasNet.Pdf       | Fully-managed PDF rasterization API for the DemaConsulting.DocDown.Pdf.Rendering package |
| FileAssert          | Validates generated documents (HTML/PDF) against acceptance criteria                     |
| Open XML SDK        | Reads Office documents for the DemaConsulting.DocDown.Office extraction package          |
| Pandoc              | Converts Markdown documentation to HTML                                                  |
| PdfPig              | Parses PDF documents for the DemaConsulting.DocDown.Pdf extraction package               |
| ReqStream           | Enforces requirements-to-test traceability                                               |
| ReviewMark          | Enforces file review coverage and currency                                               |
| SarifMark           | Converts CodeQL SARIF results into a markdown report                                     |
| SonarMark           | Generates a SonarCloud quality report                                                    |
| SysML2Tools         | Validates the SysML2 architecture model and renders its views to SVG                     |
| System.IO.Packaging | Opens an Office document's OPC container for DemaConsulting.DocDown.Office               |
| TestResults         | Serializes docdown self-validation results to TRX and JUnit                              |
| VersionMark         | Captures and publishes tool-version information                                          |
| WeasyPrint          | Converts HTML documentation to PDF                                        |
| xUnit               | Discovers and executes unit and integration tests                         |

Each item's individual design document (`docs/design/ots/{ots-name}.md`) records its Purpose,
Features Used, and Integration Pattern. Each item's requirements and verification evidence are
recorded in `docs/reqstream/ots/{ots-name}.yaml` and `docs/verification/ots/{ots-name}.md`
respectively.
