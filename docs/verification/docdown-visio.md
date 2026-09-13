# DocDown.Visio Verification Design

This document describes the system-level verification strategy for `DocDown.Visio`, the Visio extraction
package.

## Verification Approach

`DocDown.Visio` is verified through system-level integration tests in `DocDownVisioTests.cs`, and unit
tests per unit, all in `DemaConsulting.DocDown.Visio.Tests`, running on xUnit v3 across net8.0, net9.0,
and net10.0.

### Every extraction test reconciles against the filesystem

Every scenario that performs an extraction ends with Core's contract verifier over the produced folder,
which reports any disagreement between what the manifest claims and what is on disk. This is the
highest-value assertion available to this package: it makes a dishonest extraction a test failure rather
than a review finding, and it applies to degraded and failed runs as well as clean ones. The
reconciliation covers the legacy `.vsd` refusal scenario, so the full-layout claim on a structured failure
is machine-checked alongside the successful ones.

### Two backends, one reader, one emitter

The package ships two backends. The managed Open Packaging backend reads a drawing into the reader-neutral
model and drives the content emitter, so every mapping decision is made in exactly one place and can be
proved from a hand-built model with no drawing behind it. The COM backend reuses that same content path by
delegation and adds a rendered image of each page; everything it does apart from talking to Microsoft
Visio is exercised cross-platform by injecting a stub `IVisioAutomation`, so topology delegation, per-page
fault isolation, rendering-fact reconciliation, render-resolution pass-through, and outcome mapping are all
proved in CI. The real COM adapter is the one boundary CI cannot reach; its correctness in a deployed
environment is proven by release-time self-tests. A legacy binary `.vsd` has no reader here and none
anywhere in DocDown, so the request fails with a structured, reasoned outcome — never with an exception,
and never with a remedy that promises a capability that does not exist.

### The directed topology is the headline property under test

A list of disconnected strings is not a schematic, so the suite's central assertion is that the directed
connector topology is recovered with no Visio present and rendered as a readable `source -> target` edge
list under each page name. The reader is proved to resolve a connector into a directed edge in the
direction its begin and end records name, regardless of the order the records appear, to yield one edge per
parallel connector, and to scope each page's edges to that page's own shapes so a reused shape id does not
cross-link. The page names and shape text — the identity of the modeled things — are proved to be
extracted from the file itself, and multi-line shape text is proved to survive intact.

### Endpoint-label provenance is covered as tested-but-additional behavior

The forward-trace from the Visio intent asks for the directed source-to-target edges and no more. The
shipped implementation **exceeds** that: it records whether each endpoint's label is the shape's own text,
its master type, or only a bare shape id, publishes the labeling convention, and reports the honest
endpoint coverage. The suite proves this endpoint-label provenance — the shape labeler's precedence, the
parenthesized type-and-id form, the refusal of connective masters, the published convention, and the
coverage counts — because the behavior is genuinely tested; it is covered as an additional honesty measure,
not because the intent called for it. The requirement `DocDownVisio-TopologyProvenance` states that plainly.
The reader's symbol-font glyph recovery and its master-name classification are likewise covered as
tested-but-additional behavior. *(See the developer report for the full forward-trace findings.)*

### Rendering is captured when available and honest when not

The COM backend renders every foreground page through the injected stub and adds one page per page; a page
the renderer cannot produce becomes a counted, named `pages` gap and a `VISIO0004` diagnostic while the
remaining pages still render. When rendering is requested on a host without Visio, the run degrades with a
counted `pages` gap naming the reason while the directed topology is still delivered — the guaranteed
content is never hidden behind the render request. The availability probe is proved to report unavailable
off Windows with a declarative reason that never instructs an installation, so a machine without Visio
produces an honest selection outcome rather than a broken render.

### Fixtures are generated, never committed

Every drawing the suite uses is built at test time by the in-memory Visio package synthesizer in
`TestData/VsdxFixtures.cs` (over `VisioPackageBuilder`), and the rendering path is driven by
`TestData/StubVisioAutomation.cs`. The legacy `.vsd` scenario writes a placeholder byte sequence whose
extension drives format detection, because the selection path never opens the file: no registered backend
supports the format. No binary `.vsdx` or `.vsd` is committed, so the repository stays text-only and no
question arises about the provenance or licensing of a sample drawing. Every page name, shape name, master
name, part number, and rendered payload in the fixtures is synthetic.

## Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Filesystem**: a per-test `TempScratch` folder holds both the generated input drawing and the
  extraction output
- **Inputs**: Visio Open Packaging drawings generated at test time by `VisioPackageBuilder`, plus a short
  byte sequence for the legacy `.vsd` refusal scenario; no committed binary fixtures and no network access
- **Mocking**: none for the managed integration scenarios — every test drives the real engine and the real
  Open Packaging backend; the COM backend is driven through an injected stub `IVisioAutomation` so the
  whole rendering path is exercised with no Microsoft Office present
- **Determinism**: a fixed timestamp is injected so repeated runs are byte-comparable
- **Isolation**: each test owns its temporary folder and cleans it on dispose

## Acceptance Criteria

Per IEC 62304 §5.7.2, a system-level test run passes when:

- Every scenario below passes on every operating system and runtime in the CI matrix, with no unexpected
  exception, wrong exception type, or wrong return value.
- Every extraction scenario ends with the contract verifier reporting zero violations.
- No adverse drawing — legacy, empty, or unreadable — causes an exception to escape to the caller, and
  every one of them still produces the full output layout.
- Every page's name, shape text, and directed connector topology reach the output, extracted from the file
  itself, with no Visio installation present.
- The rendering backend renders every page when the stub adapter succeeds, and reports a counted, named gap
  for any page it cannot render while continuing with the rest.
- A render requested without Visio present degrades with a counted `pages` gap naming the reason while the
  directed topology is still delivered.
- The legacy binary `.vsd` format is refused with a coded structured failure whose remedy states that
  DocDown does not support legacy binary formats, never an exception and never an instruction the reader
  could act on and fail at.
- Each of the six platform requirements is satisfied by a source-filtered result from the matching
  operating system or runtime; a result from another platform does not count.

## Test Scenarios

Each scenario corresponds to one system requirement and names the real test method that evidences it.
Platform requirements are covered by the source-filtered runs of the selection scenario.

### The default engine registers both Visio backends

**Test**: `AddVisio_RegistersOpenXmlAndComBackends`

Proves the one-liner the host uses to add Visio support registers both the managed Open Packaging backend
and the COM automation backend on the resulting engine, so the set of active backends is a decision
readable in host code rather than a deployment accident. Evidence for `DocDownVisio-Registration`.

### The Open Packaging backend is selected and produces the contract layout

**Test**: `DocDownVisio_Extract_Vsdx_SelectsOpenXml`

Proves a clean extraction: the managed Open Packaging backend is selected for a `.vsdx`, the full output
layout is produced, and the contract verifier reports no violations. This is also the anchor for the
platform requirements and for the no-installation guarantee. Evidence for `DocDownVisio-Extraction`,
`DocDownVisio-NoInstallationRequired`, and under source filters the six `DocDownVisio-Platform-*`
requirements.

### Every page name is surfaced

**Test**: `DocDownVisio_Extract_TwoPages_SurfacesEveryPageName`

Proves the name of every page of a multi-page drawing reaches the output as its heading, so a reader can
navigate and cite the drawing by page name. Evidence for `DocDownVisio-PageNames`.

### Shape text is extracted

**Test**: `VisioPackageReader_Read_WashSystem_ExtractsShapeText`

Proves the text of every shape that carries text is read from the file, so the identity of the modeled
things reaches the output with no rendering. Evidence for `DocDownVisio-ShapeText`.

### The directed topology is recovered

**Test**: `DocDownVisio_Extract_WashSystem_RecoversDirectedTopology`

Proves the directed connector topology is recovered with no Visio present and rendered as a readable
`source -> target` edge list under the page name — the engineering content. Evidence for
`DocDownVisio-Topology`.

### Endpoint-label provenance is published (intent-exceeding; covered because tested)

**Test**: `VisioShapeLabeler_Convention_DescribesEveryRenderedForm`

Proves the labeling convention describes every rendered label form, so a reader can tell an authored name
from a master-type classification from a bare shape id. This exceeds the Visio intent, which asked only for
the directed edges; it is covered because it is genuinely tested. Evidence for
`DocDownVisio-TopologyProvenance`.

### Pages render when Visio is available

**Test**: `VisioComExtractor_Extract_ViaStub_WritesTopologyAndRendersEveryPage`

Proves the COM backend delegates the guaranteed content — the topology — and adds one rendered page per
page through the automation seam, disposing the session. Evidence for `DocDownVisio-PageRendering`.

### A render requested without Visio degrades honestly

**Test**: `DocDownVisio_Extract_RenderRequestedWithoutVisio_DegradesWithCountedPagesGapButKeepsTopology`

Proves that on a host without Visio a render request degrades the run with a counted `pages` gap naming the
reason, while the directed topology is still delivered in full — the absence is stated, never silently
omitted. Evidence for `DocDownVisio-RenderingUnavailableGap`.

### A page that cannot be rendered is a counted gap

**Test**: `VisioComExtractor_Extract_PageRenderFails_ReportsCountedGap`

Proves a page that fails to render becomes a counted `pages` gap and a `VISIO0004` diagnostic while the
remaining pages still render — never a silent absence. Evidence for `DocDownVisio-PageRenderFidelity`.

### Rendering availability is honest and instructs no installation

**Test**: `VisioComAvailability_Probe_NeverInstructsInstallation`

Proves the availability probe never instructs an installation, so an environment without Visio is reported
as an honest fact. Evidence for `DocDownVisio-RenderingAvailability`.

### Embedded images are written and linked from the page

**Test**: `VisioContentEmitter_Emit_WithRasterImages_WritesThroughSink`

Proves a drawing's embedded raster images are written through the sink and linked from the page that shows
them, with the page association recorded and the package thumbnail excluded. Evidence for
`DocDownVisio-EmbeddedImages`.

### A vector image is written as-is with a caveat

**Test**: `DocDownVisio_Extract_PageWithVectorImage_Succeeds`

Proves an EMF or WMF metafile is written unchanged, `VISIO0003` records the readability caveat as an
informational diagnostic, and no images gap is opened — a well-formed vector-bearing drawing does not
degrade. Evidence for `DocDownVisio-VectorImages`.

### An empty drawing degrades with a counted gap

**Test**: `VisioContentEmitter_Emit_EmptyDrawing_ReportsGap`

Proves a drawing with no pages degrades with a counted gap naming the absence, so an empty content document
is a stated fact rather than a mystery. Evidence for `DocDownVisio-EmptyDrawing`.

### The managed backend declares exactly the deliverable capabilities

**Test**: `VisioOpenXmlExtractor_Descriptor_MatchesContract`

Proves the managed descriptor's identity, priority, supported formats, and declared capabilities — text,
embedded images, document metadata, and document structure — match the deliverable set, and — as an express
counterpart — that the rendered-pages capability is not declared. The COM descriptor's full superset
including rendered-pages is proved by `VisioComExtractor_Descriptor_MatchesContract`. Evidence for
`DocDownVisio-DeclaredCapabilities`.

### A legacy binary drawing is refused with an honest remedy

**Test**: `DocDownVisio_Extract_LegacyVsd_FailsWithUnsupportedFormatRemedy`

Proves an engine with the Visio package registered fails a `.vsd` request with a structured failure and a
remedy that names the format and states plainly that DocDown does not support the legacy binary Office
formats — with no package named, no installation instruction, and no environment precondition, because no
such route exists. The full contract layout is still produced and reconciles cleanly. Evidence for
`DocDownVisio-LegacyFormatRefusal`.

### Self-validation cases are exposed and run

**Test**: `VisioOpenXmlExtractor_SelfTestCases_RoundTripPassesAndRenderingSkipped`

Proves the managed backend contributes its cases, that the topology round-trip case genuinely passes in the
environment under test, and that the page-rendering capability the managed backend does not claim reports a
skip with a reason rather than a failure — the property a traceability pipeline depends on. Evidence for
`DocDownVisio-SelfValidation`.
