## OpenXml Subsystem Verification Design

This document describes the verification strategy for the OpenXml subsystem, the guaranteed content
backend: the extractor, the package reader, and the image reader.

### Verification Approach

The OpenXml subsystem is verified through unit tests in `OpenXml/VisioOpenXmlExtractorTests.cs`,
`OpenXml/VisioPackageReaderTests.cs`, and `OpenXml/VisioImageReaderTests.cs`, alongside the
system-level integration tests that drive the managed extractor end to end.

The extractor's selection identity, always-available probe, and self-test contribution are asserted
directly. The package reader is proved against drawings built at test time by `VisioPackageBuilder`:
it surfaces each page's name, extracts each shape's text, resolves each connector into a directed
edge in the direction its records name regardless of record order, preserves page order, scopes each
page's edges to that page's own shapes, classifies a text-less shape by its master name, recovers a
symbol-font glyph only within a run whose font declares it, and yields an empty page cleanly. The
image reader is proved to yield a page image while excluding the thumbnail and to flag a master
image as template furniture without a page.

The reader's master-name classification and symbol-font glyph recovery exceed the original Visio
intent, which asked only for page names, shape text, and directed edges; they are verified here
because the behavior is genuinely tested.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: Visio Open Packaging drawings synthesized in memory by `VisioPackageBuilder` —
  including master-typed schematics, parallel connectors, reused shape ids, symbol-font runs, and
  page/master/thumbnail image relationships — with no committed binary fixtures
- **Mocking**: none; the reader and image reader are pure over the package
- **Isolation**: each test builds its own package and reads it back

### Acceptance Criteria

Per IEC 62304 §5.6.2, an OpenXml subsystem test run passes when the extractor identifies the modern
Visio drawing formats, carries the managed backend's higher priority, reports `Available()` without
rendered-page support, and contributes a topology round-trip plus a rendering-skip case; the reader
surfaces page names, shape text, directed topology in document order scoped to each page, master
classification, and symbol-font recovery; and the image reader yields each distinct embedded image
once with its page association, excludes the thumbnail, and flags template furniture. Any dropped
page name, cross-linked edge, corrupted text, or thumbnail reported as content is a failure.

### Test Scenarios

The per-unit scenarios are given in the `VisioOpenXmlExtractor`, `VisioPackageReader`, and
`VisioImageReader` unit verification chapters, each naming the requirement it evidences.
