# DocDown.PowerPoint.Rendering Verification Design

This document describes the system-level verification strategy for `DocDown.PowerPoint.Rendering`,
the optional PowerPoint slide-rendering package.

## Verification Approach

`DocDown.PowerPoint.Rendering` is verified through system-level integration tests in
`DocDownPowerPointRenderingTests.cs` and focused unit tests per unit, all in
`DemaConsulting.DocDown.PowerPoint.Rendering.Tests`, running on xUnit v3 across net8.0, net9.0, and
net10.0.

### The central scenario produces a real rendered slide

The highest-value scenario renders the embedded probe presentation end to end and asserts each
produced `pages/pageNNNN.png` is a valid PNG — the PNG signature and a header with plausible
dimensions for the requested DPI — not merely that a file appeared. A file that exists but is not a
decodable image would satisfy a weaker check while failing the actual promise, so the check reads
the image structure rather than the directory listing.

### Every extraction scenario confirms the invariant layout

Every scenario that performs an extraction ends with `ContractAssert.LayoutPresent`, proving the
standard DocDown output layout exists on disk. This applies to the clean render, the two selection
scenarios, the page-range and DPI scenarios, and the forced fault-isolation scenarios, so a missing
core artifact or silently missing `pages/` output becomes a test failure rather than a review
finding.

### Selection is exercised both ways

Because the engine selects one backend, two scenarios assert the selection outcome directly: with
page rendering requested the rendering backend must win, since it is always available; without it
requested the lighter managed backend must win so rasterization is never touched. Unconditional
availability itself is verified in a companion unit scenario that asserts the extractor always
reports rendered-page support.

### A per-slide rasterization fault, and a slide-count fault, are proven to become notes, not exceptions

A real CanvasNet.Pptx fault cannot be produced reliably on cue, so both fault-isolation paths are
exercised by injecting delegates that throw, through the extractor's internal injectable-delegate
constructors. One scenario proves a per-slide rasterization fault still completes as `Produced`,
records the one-sentence note `Slide N could not be rasterized.` for each faulting slide, produces no
image for that slide, and does not throw to the caller. A second scenario proves a slide-count fault
is reported once, as `Slides could not be counted, so no slide images were rendered.`, with the
delegated managed content (text, images, metadata) left intact.

### Test fixtures reuse the embedded self-test probe; no binary is duplicated

The test suite loads the real `Resources/probe.pptx` the production assembly embeds — the same file
`docdown --validate` rasterizes — via `DocDown.Core`'s public `SelfTestProbe.Load` helper, rather than
duplicating the binary in the test project. It is a real two-slide deck authored in Microsoft
PowerPoint, so the rasterizer is always exercised against a file a real producer wrote. The PNG
inspector reads only the signature and header, so no image-decoding dependency is added to the test
project.

## Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Rasterizer**: CanvasNet.Pptx, CanvasNet.Charts, and CanvasNet are fully managed and carry no
  runtime-identifier-specific asset, so the render scenarios exercise the real rasterizer on every
  operating system and framework in the CI matrix with no conditional skip path
- **Filesystem**: a per-test `TempScratch` folder holds the extraction output
- **Inputs**: the embedded `Resources/probe.pptx` the self-test reads, loaded through
  `SelfTestProbe.Load`; no other committed binary test fixtures and no network access
- **Determinism**: the run-varying timestamp line is normalized so repeated runs are byte-comparable
- **Isolation**: each test owns its temporary folder and cleans it on dispose

## Acceptance Criteria

Per IEC 62304 §5.7.2, a system-level test run passes when:

- Every scenario below passes on every operating system and runtime in the CI matrix, with no
  unexpected exception, wrong exception type, or wrong return value.
- Every rendered slide is a valid PNG of plausible dimensions, not merely a file that exists.
- Every extraction scenario ends with `ContractAssert.LayoutPresent`.
- No successful render scenario emits notes; a forced per-slide fault emits the plain note
  `Slide N could not be rasterized.` and still returns `Produced`; a forced slide-count fault emits
  the single note `Slides could not be counted, so no slide images were rendered.` and keeps the
  delegated managed content.
- A page-renderer (this backend) is selected whenever page rendering is requested, and it is not
  selected when it is not.
- The availability probe is cheap, non-throwing, and unconditionally reports rendered-page support.
- Each of the six platform requirements is satisfied by a source-filtered result from the matching
  operating system or runtime; a result from another platform does not count.
- Two renders of the same presentation produce byte-identical slide images.

## Test Scenarios

Each scenario corresponds to one system requirement and names the real test method that evidences it.
The CanvasNet.Pptx, CanvasNet.Charts, and CanvasNet OTS items are verified by transitive evidence from
these same scenarios; the exact tests are named in their OTS verification documents.

### The probe presentation renders to valid PNG slides

**Test**: `DocDownPowerPointRendering_Render_ProbePresentation_ProducesValidPngPages`

Proves the central promise: with both backends registered and rendering requested, this backend is
selected and every `pages/pageNNNN.png` is a valid PNG of plausible dimensions for the requested DPI.
Evidence for `DocDownPowerPointRendering-RendersSlidesToPng`, and the anchor for the platform
requirements.

### Repeated rendering is byte-identical

**Test**: `DocDownPowerPointRendering_Render_Deterministic_ProducesByteIdenticalPages`

Proves two renders of the same presentation in the same environment produce byte-identical slide
images, so a consumer can diff two extractions meaningfully. Evidence for
`DocDownPowerPointRendering-Determinism`.

### The rendering backend wins when pages are requested

**Test**: `DocDownPowerPointRendering_Select_PagesRequested_RenderingBackendWins`

Proves selection prefers the rendering backend over the non-rendering managed backend when page
rendering is requested, that slides are produced, and that a clean render completes without notes.
Evidence for `DocDownPowerPointRendering-SelectedWhenPagesRequested`.

### The managed backend wins when pages are not requested

**Test**: `DocDownPowerPointRendering_Select_PagesNotRequested_BaseBackendWins`

Proves that with rendering not requested the lighter managed backend wins and produces no slide
images, so a plain extraction touches no rasterization code. Evidence for
`DocDownPowerPointRendering-BaseSelectedWhenNotRequested`.

### A requested page range restricts which slides render

**Test**: `DocDownPowerPointRendering_Render_PageRange_RestrictsRenderedPages`

Proves only the in-range slides are rasterized, named by their slide number. Evidence for
`DocDownPowerPointRendering-HonorsPageRange`.

### A higher DPI produces a larger slide image

**Test**: `DocDownPowerPointRendering_Render_HigherDpi_ProducesLargerPage`

Proves the requested DPI controls the raster: a higher-DPI render is strictly larger in both
dimensions than a lower-DPI render of the same slide. Evidence for
`DocDownPowerPointRendering-HonorsDpi`.

### Availability reporting is unconditional, cheap, and honest

**Test**: `PowerPointPageRenderingExtractor_ProbeAvailability_AnyEnvironment_ReportsUnconditionallyAvailable`

Proves the extractor always reports rendered-page support, cheaply and without throwing, since
CanvasNet.Pptx is fully managed and carries no runtime-identifier-specific asset. Evidence for
`DocDownPowerPointRendering-ReportsRenderedPagesAvailability` and
`DocDownPowerPointRendering-ProbeCheapAndSafe`.

### The managed aspects are delivered by delegation

**Test**: `PowerPointPageRenderingExtractor_ExtractAsync_RenderRequested_WritesPagePngs`

Proves the backend delegates the managed aspects to `PowerPointOpenXmlExtractor` — content, images,
and metadata are written — and adds valid slide PNGs, so both the delegation and the rasterization
happen in one run. Evidence for `DocDownPowerPointRendering-ManagedInputsDelegated` and
`DocDownPowerPointRendering-RendersSlidesToPng`.

### A per-slide render fault becomes a note without throwing

**Test**: `PowerPointPageRenderingExtractor_ExtractAsync_SlideRenderFaults_ReportsNoteWithoutThrowing`

Proves a faulting rasterization function records the plain note `Slide N could not be rasterized.`
for each affected slide, produces no image for that slide, still returns `Produced`, and does not
throw to the caller. Evidence for `DocDownPowerPointRendering-PerSlideFailureReportedAsNote`.

### A slide-count fault becomes a single note and preserves delegated content

**Test**: `PowerPointPageRenderingExtractor_ExtractAsync_SlideCountFaults_ReportsNoteAndKeepsDelegatedContent`

Proves a faulting slide-count function is caught once, records the single note
`Slides could not be counted, so no slide images were rendered.`, still returns `Produced`, and keeps
the delegated managed content the base extractor already wrote. Evidence for
`DocDownPowerPointRendering-SlideCountFailureReportedAsNote`.

### Concurrent renders do not interfere

**Test**: `SlideRenderer_Render_ConcurrentCalls_AllProduceValidPng`

Proves several renders driven in parallel all produce valid PNGs — the observable proof that opening
an independent document per call, with no shared mutable state in this package, lets concurrent
callers succeed without corrupting one another's output. Evidence for
`DocDownPowerPointRendering-ConcurrentRendersDoNotInterfere`.

### The registration seam is explicit, chainable, and CanvasNet-type-free

**Tests**: `AddPowerPointRendering_OnBuilder_RegistersRenderingExtractor`,
`AddPowerPointRendering_OnBuilder_ReturnsSameBuilderForChaining`,
`AddPowerPointRendering_NullBuilder_Throws`, `PublicApi_AllPublicMembers_ExposeNoCanvasNetTypes`

Prove the seam registers exactly the rendering extractor, returns the builder for chaining, rejects a
null builder at the call site, and exposes no CanvasNet.Pptx, CanvasNet.Charts, or CanvasNet type on
the public surface. Evidence for `DocDownPowerPointRendering-Registration`.

### The self-test proves rendering genuinely rasterizes, unconditionally

**Test**: `PowerPointPageRenderingExtractor_GetSelfTestCases_AnyEnvironment_PassesRenderCase`

Proves the backend contributes a distinctly named render round-trip case that rasterizes the embedded
probe presentation and passes in every environment, since CanvasNet.Pptx carries no
environment-dependent failure mode. Evidence for `DocDownPowerPointRendering-SelfValidation`.
