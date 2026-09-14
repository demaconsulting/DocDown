# DocDown.Visio Verification Design

This document describes the system-level verification strategy for `DocDown.Visio`, the Visio
extraction package.

## Verification Approach

`DocDown.Visio` is verified through system-level integration tests in `DocDownVisioTests.cs` and
unit tests per unit, all in `DemaConsulting.DocDown.Office.Tests`, running on xUnit v3 across
net8.0, net9.0, and net10.0.

### Every extraction test reconciles against the filesystem

Every scenario that performs an extraction ends with layout assertions proving the invariant output
folders and files are present on disk. This is the highest-value assertion available to this package:
it makes a dishonest extraction a test failure rather than a review finding, and it applies to
`Produced` and `Unreadable` outcomes alike. The layout assertions cover the legacy `.vsd` refusal
scenario, so the full-layout claim on an unreadable result is machine-checked alongside the
successful ones. Each scenario additionally asserts the extraction outcome, the inventory counts
written into `summary.txt` and `manifest.json`, and the exact set of notes the run recorded.

### Two backends, one reader, one emitter

The package ships two backends. The managed Open Packaging backend reads a drawing into the
reader-neutral model and drives the content emitter, so every mapping decision is made in exactly
one place and can be proved from a hand-built model with no drawing behind it. The COM backend
reuses that same content path by delegation and adds rendered pages; everything it does apart from
talking to Microsoft Visio is exercised cross-platform by injecting a stub `IVisioAutomation`, so
content delegation, rendering-fact reconciliation, render-resolution pass-through, per-page note
reporting, and probe behavior are all proved in CI. The real COM adapter is the one boundary CI
cannot reach; its correctness in a deployed environment is proven by release-time self-tests.

### The directed topology is the headline property under test

A list of disconnected strings is not a schematic, so the suite's central assertion is that the
directed connector topology is recovered with no Visio present and rendered as a readable
`source -> target` edge list under each page name. The reader is proved to resolve connector
records into directed edges, keep page order, and scope each page's edges to that page's own
shapes. The page names and shape text — the identity of the modeled things — are proved to be
extracted from the file itself, and multi-line shape text is proved to survive intact.

### Reporting is limited to inventory and short notes

The current reporting model is also covered. `VisioContentEmitter` reports looked-for counts for
pages, labeled shapes, and connections, including zero, so an empty drawing is proved as an
inventory fact rather than an extraction shortfall. When DocDown attempted an additional step and
could not complete it, the suite proves a short note is recorded instead: one note when a render was
requested without an available renderer, one note when a single page could not be rendered through
and COM.
No Visio-specific legacy reporting terms remain.

### Test fixtures are generated; the self-test probe is committed

Every drawing the suite uses is built at test time by the in-memory Visio package synthesizer in
`TestData/VsdxFixtures.cs`, and the rendering path is driven by `TestData/StubVisioAutomation.cs`.
The legacy `.vsd` scenario writes a placeholder byte sequence whose extension drives format
detection, because the selection path never opens the file: no registered backend supports the
format. No binary `.vsdx` or `.vsd` is committed, so the repository stays text-only and every page
name, shape name, master name, image payload, and rendered payload in the fixtures is synthetic.

## Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Filesystem**: a per-test `TempScratch` folder holds both the generated input drawing and the
  extraction output
- **Inputs**: Visio Open Packaging drawings generated at test time by `VisioPackageBuilder`, plus a
  short byte sequence for the legacy `.vsd` refusal scenario; no committed binary fixtures and no
  network access
- **Mocking**: none for the managed integration scenarios — every test drives the real engine and
  the real Open Packaging backend; the COM backend is driven through an injected stub
  `IVisioAutomation` so the whole rendering path is exercised with no Microsoft Office present
- **Determinism**: a fixed timestamp is injected so repeated runs are byte-comparable
- **Isolation**: each test owns its temporary folder and cleans it on dispose

## Acceptance Criteria

Per IEC 62304 §5.7.2, a system-level test run passes when:

- Every scenario below passes on every operating system and runtime in the CI matrix, with no
  unexpected exception, wrong exception type, or wrong return value.
- Every extraction scenario produces the expected DocDown layout on disk, and the notes the run
  records are exactly the notes the scenario expects.
- Normal extractions return `Produced`, and unreadable drawings return `Unreadable` with the
  standard output layout still present.
- Every page's name, shape text, and directed connector topology reach the output, extracted from
  the file itself, with no Visio installation present.
- The rendering backend renders every page when the stub adapter succeeds and records a plain note
  for any page it could not render while continuing with the remaining pages.
- A render requested without an available renderer records a plain note while the managed content
  still reaches the output.
- Embedded images are written and linked in the encoding the drawing stored them in, which is
  recorded as a plain note while the source bytes are preserved.
- The legacy binary `.vsd` format is refused with an unreadable result whose explanation states that
  the format is unsupported, never by an exception escaping to the caller and never by an
  install-instructing message.
- Each of the six platform requirements is satisfied by a source-filtered result from the matching
  operating system or runtime; a result from another platform does not count.

## Test Scenarios

Each scenario corresponds to one system requirement and names the real test method that evidences
it. Platform requirements are covered by the source-filtered runs of the selection scenario.

### The default engine registers both Visio backends

**Test**: `AddVisio_RegistersOpenXmlAndComBackends`

Proves the one-liner the host uses to add Visio support registers both the managed Open Packaging
backend and the COM automation backend on the resulting engine. Evidence for
`DocDownVisio-Registration`.

### The Open Packaging backend is selected and produces the contract layout

**Test**: `DocDownVisio_Extract_Vsdx_SelectsOpenXml`

Proves a normal extraction: the managed backend is selected for a `.vsdx`, the full output layout
is produced, and the run completes as `Produced` with no notes recorded. This is also the anchor for
the platform requirements and for the no-installation guarantee. Evidence for
`DocDownVisio-Extraction` and, under source filters, the six `DocDownVisio-Platform-*`
requirements.

### Every page name is surfaced

**Test**: `DocDownVisio_Extract_TwoPages_SurfacesEveryPageName`

Proves the name of every page of a multi-page drawing reaches the output as its heading. Evidence
for `DocDownVisio-PageNames`.

### Shape text is extracted

**Test**: `VisioPackageReader_Read_WashSystem_ExtractsShapeText`

Proves the text of every shape that carries text is read from the file. Evidence for
`DocDownVisio-ShapeText`.

### The directed topology is recovered

**Test**: `DocDownVisio_Extract_WashSystem_RecoversDirectedTopology`

Proves the directed connector topology is recovered with no Visio present and rendered as a readable
`source -> target` edge list under the page name. Evidence for `DocDownVisio-Topology`.

### Pages render when Visio is available

**Test**: `VisioComExtractor_Extract_ViaStub_WritesTopologyAndRendersEveryPage`

Proves the COM backend delegates the guaranteed content and adds one rendered page per page through
the automation seam, disposing the session. Evidence for `DocDownVisio-PageRendering`.

### A render requested without Visio records a plain note

**Test**: `DocDownVisio_Extract_RenderRequestedWithoutVisio_RecordsNoteButKeepsTopology`

Proves that on a host without an available Visio renderer, a render request records a plain note
while the directed topology is still delivered in full. Evidence for
`DocDownVisio-RenderingRequestNote`.

### A page that cannot be rendered records a plain note

**Test**: `VisioComExtractor_Extract_PageRenderFails_ReportsNote`

Proves a page that fails to render is reported with a one-sentence note while the remaining pages
still render. Evidence for `DocDownVisio-Com-VisioComExtractor-ReportsPageRenderFailureNotes`.

### Rendering availability is honest and instructs no installation

**Test**: `VisioComAvailability_Probe_NeverInstructsInstallation`

Proves the availability probe never instructs an installation. When Microsoft Visio is available,
the same result reports rendered-page support. Evidence for `DocDownVisio-RenderingAvailability`.

### Embedded images are written into the output layout

**Tests**: `DocDownVisio_Extract_PageWithVectorImage_ProducesLayoutAndEmfImage`,
`VisioContentEmitter_Emit_WithRasterImages_WritesThroughSink`

Prove embedded images are written through the sink into the output layout for both system and unit
scenarios. Evidence for `DocDownVisio-EmbeddedImages`.

### A legacy binary drawing is refused as unreadable

**Test**: `DocDownVisio_Extract_LegacyVsd_ReturnsUnreadableFailure`

Proves an engine with the Visio package registered returns an unreadable result for `.vsd`, with an
explanation that states plainly that the legacy binary format is unsupported. Evidence for
`DocDownVisio-LegacyFormatRefusal`.

### Self-validation cases are exposed and run

**Test**: `VisioOpenXmlExtractor_SelfTestCases_RoundTripPassesAndRenderingSkipped`

Proves the managed backend contributes its self-test cases, that the round-trip case genuinely
passes in the environment under test, and that the page-rendering case reports a skip with a reason.
Evidence for `DocDownVisio-SelfValidation`.
