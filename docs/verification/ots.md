# OTS Verification Evidence

This document describes the overall Off-The-Shelf (OTS) verification strategy for this repository.

## Overview

Every OTS item used to build and verify this repository is verified through a combination of
self-validation CLI flags (where the tool provides a `--validate` or equivalent self-test mode)
and pipeline-evidence-based verification (where a passing CI pipeline run, having produced and
validated the expected output at each stage, constitutes proof that the tool executed correctly).

**PdfPig, PDFtoImage, the Open XML SDK, and TestResults are verified differently, and deliberately
so.** None is a pipeline stage. PdfPig is a runtime library on the critical path of every
`DocDown.Pdf` extraction, so its evidence is transitive from the extraction test suite. PDFtoImage
(with its transitive PDFium and SkiaSharp native stack) is on the critical path of every rendered
page, so its evidence is transitive from the `DocDown.Pdf.Rendering` render tests, each of which names
the exact tests that exercise it. The Open XML SDK is on the critical path of every Word, Excel and
PowerPoint extraction, so its evidence is transitive from those extraction tests, which name the
exact tests that exercise it. `System.IO.Packaging` is referenced and called directly by
`DocDown.Office` to open an OPC container — it is how the Visio backend reads a `.vsdx` without the
SDK at all — so its evidence comes from the Visio package-reader tests as well as the Word suite.
TestResults is the library
`DocDown.Tool` uses to serialize its `--validate` results, so its evidence is transitive from the
tool's own serialization tests. A dedicated OTS test project is required only where no other evidence
is available, which is not the case for any of them; the reasoning is set out in full in each item's
verification document.

Each item's individual verification document (`docs/verification/ots/{ots-name}.md`) records the
detailed approach and named test scenarios.

## OTS Items

| OTS Item            | Verification Approach                                                           |
| :------------------ | :------------------------------------------------------------------------------ |
| ApiMark             | Self-validation CLI suite: the named ApiMark_DotNetGeneration test              |
| BuildMark           | Self-validation CLI suite plus pipeline evidence via build-notes document       |
| FileAssert          | Self-validation CLI suite plus transitive evidence from document assertions     |
| Open XML SDK        | Transitive evidence from the Word, Excel and PowerPoint test suites             |
| Pandoc              | Pipeline evidence: FileAssert assertions on each generated HTML document        |
| PdfPig              | Transitive evidence from the DocDown.Pdf extraction and unit test suites        |
| PDFtoImage          | Transitive evidence from the DocDown.Pdf.Rendering render tests                 |
| ReqStream           | Self-validation CLI suite plus pipeline evidence via --enforce traceability     |
| ReviewMark          | Self-validation CLI suite plus pipeline evidence via review plan/report         |
| SarifMark           | Self-validation CLI suite plus pipeline evidence via SARIF markdown report      |
| SonarMark           | Self-validation CLI suite plus pipeline evidence via SonarCloud report          |
| SysML2Tools         | Self-validation CLI suite plus pipeline evidence via lint and rendered SVGs     |
| System.IO.Packaging | Direct evidence from the Visio package-reader and Word extraction tests         |
| TestResults         | Transitive evidence from the DocDown.Tool --validate tests                      |
| VersionMark         | Self-validation CLI suite plus pipeline evidence via version data               |
| WeasyPrint          | Pipeline evidence: FileAssert assertions on each generated PDF document         |
| xUnit               | Self-validation via discovery, execution, and TRX reporting of tests            |
