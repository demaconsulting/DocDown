## OpenXml Subsystem Verification Design

This document describes the verification strategy for the OpenXml subsystem, the guaranteed content
backend: the extractor, the package reader, and the image reader.

### Verification Approach

The OpenXml subsystem is verified through unit tests in `OpenXml/VisioOpenXmlExtractorTests.cs`,
`OpenXml/VisioPackageReaderTests.cs`, and `OpenXml/VisioImageReaderTests.cs` in
`DemaConsulting.DocDown.Visio.Tests`, alongside the system-level integration tests that drive the extractor
end to end.

The extractor's descriptor, unconditional availability, and self-test contribution are asserted directly.
The package reader is proved against drawings built at test time by `VisioPackageBuilder`: it surfaces each
page's name, extracts each shape's text, resolves each connector into a directed edge in the direction its
records name (regardless of record order, one edge per parallel connector), preserves page order, scopes
each page's edges to that page's own shapes so a reused shape id does not cross-link, classifies a text-less
shape by its master name (falling back to the universal name, and carrying none for a shape with no master,
a dangling reference, or a drawing with no masters part), recovers a symbol-font glyph only within a run
whose font declares it, and yields an empty page cleanly. The image reader is proved to yield a page image
while excluding the thumbnail and to flag a master image as template furniture without a page.

The reader's master-name classification and symbol-font glyph recovery **exceed the Visio intent**, which
asked for page names, shape text, and the directed edges; those scenarios are verified here because the
behavior is genuinely tested, and are covered as tested-but-additional behavior.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: Visio Open Packaging drawings synthesized in memory by `VisioPackageBuilder` — including
  master-typed schematics, parallel connectors, reused shape ids, symbol-font runs, and page/master/thumbnail
  image relationships — with no committed binary fixtures
- **Mocking**: none; the reader and image reader are pure over the package
- **Isolation**: each test builds its own package and reads it back

### Acceptance Criteria

Per IEC 62304 §5.6.2, an OpenXml subsystem test run passes when the extractor declares exactly the
deliverable capabilities and not rendered-pages, probes available unconditionally, and contributes a
topology round-trip and a reasoned page-rendering skip; the reader surfaces page names, shape text, the
directed topology in document order scoped to each page, the master classification, and the symbol-font
recovery, wrapping a package it cannot open or missing pages part in a structured failure; and the image reader
yields each distinct embedded image once with its page association, excludes the thumbnail, and flags
template furniture. Any dropped page name, cross-linked edge, corrupted text, mis-scoped connector, or
thumbnail reported as content is a failure.

### Test Scenarios

The per-unit scenarios are given in the `VisioOpenXmlExtractor`, `VisioPackageReader`, and `VisioImageReader`
unit verification chapters, each naming the requirement it evidences.
