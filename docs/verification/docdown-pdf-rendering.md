# DocDown.Pdf.Rendering Verification Design

This document describes the system-level verification strategy for `DocDown.Pdf.Rendering`, the
optional PDF page-rendering package.

## Verification Approach

`DocDown.Pdf.Rendering` is verified through system-level integration tests in
`DocDownPdfRenderingTests.cs` and focused unit tests per unit, all in
`DemaConsulting.DocDown.Pdf.Rendering.Tests`, running on xUnit v3 across net8.0, net9.0, and net10.0.

### The central scenario produces a real rendered page

The highest-value scenario renders a generated PDF and asserts the produced `pages/page0001.png` is a
valid PNG — the PNG signature, and a header whose width and height are plausible for A4 at the
requested DPI and portrait-oriented — not merely that a file appeared. A file that exists but is not a
decodable image would satisfy a weaker check while failing the actual promise, so the check reads the
image structure rather than the directory listing.

### Every extraction scenario confirms the invariant layout

Every scenario that performs an extraction ends with `ContractAssert.LayoutPresent`, proving the
standard DocDown output layout exists on disk. This applies to the clean render, the two selection
scenarios, the page-range and DPI scenarios, and the forced page-failure unit scenario, so a missing
core artifact or silently missing `pages/` output becomes a test failure rather than a review
finding.

### Selection is exercised both ways, with availability deciding whether rendering can win

Because the engine selects one backend, two scenarios assert the selection outcome directly: with page
rendering requested the rendering backend must win when its availability probe reports rendered-page
support, and without it requested the lighter managed backend must win so no native code is touched.
Availability itself is verified in companion unit scenarios that assert the extractor reports
rendered-page support only when the native stack is usable and otherwise reports an unavailable reason.

### Native failure honesty is proven through an injected fault

A genuine PDFium fault cannot be produced on cue, so the per-page fault-isolation path is exercised by
injecting a rasterization function that throws. The scenario proves the run still completes as
`Produced`, records the one-sentence note `Page 1 could not be rasterized.`, produces no page, and
does not throw to the caller — the factual reporting the output contract requires of the one step
this package attempted and could not complete.

### Test fixtures are generated; the self-test probe is committed

Every PDF the suite uses is built at test time by the parser's own document writer, so the repository
stays text-only and no question arises about the provenance or licensing of a sample document. The PNG
inspector reads only the signature and header, so no image-decoding dependency is added to the test
project.

## Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Native stack**: the test host ships the PDFtoImage/PDFium/SkiaSharp native assets, so the render
  scenarios exercise the real rasterizer; where a host lacked the native stack the backend would
  report unavailable and the self-test case would skip
- **Filesystem**: a per-test `TempScratch` folder holds both the generated input and the output
- **Inputs**: PDFs generated at test time; no committed binary fixtures and no network access
- **Determinism**: the run-varying timestamp line is normalized so repeated runs are byte-comparable
- **Isolation**: each test owns its temporary folder and cleans it on dispose

## Acceptance Criteria

Per IEC 62304 §5.7.2, a system-level test run passes when:

- Every scenario below passes on every operating system and runtime in the CI matrix, with no
  unexpected exception, wrong exception type, or wrong return value.
- The rendered page is a valid PNG of plausible dimensions, not merely a file that exists.
- Every extraction scenario ends with `ContractAssert.LayoutPresent`.
- No successful render scenario emits notes; a forced per-page fault emits the plain note
  `Page N could not be rasterized.` and still returns `Produced`.
- The rendering backend is selected when and only when page rendering is requested and available.
- The availability probe is cheap, non-throwing, and reports rendered-page support only when the
  native stack is usable in the current environment.
- Each of the six platform requirements is satisfied by a source-filtered result from the matching
  operating system or runtime; a result from another platform does not count.
- Two renders of the same document with a fixed timestamp produce byte-identical page images.

## Test Scenarios

Each scenario corresponds to one system requirement and names the real test method that evidences it.
The three OTS items (PDFtoImage, PDFium, SkiaSharp) are verified by transitive evidence from these
same scenarios; the exact tests are named in each OTS verification document.

### A generated PDF renders to a valid PNG page

**Test**: `DocDownPdfRendering_Render_GeneratedPdf_ProducesValidPngPages`

Proves the central promise: with both backends registered and rendering requested, the rendering
backend is selected and `pages/page0001.png` is a valid PNG of plausible, portrait dimensions for A4
at 150 DPI. Evidence for `DocDownPdfRendering-RendersPagesToPng`, and the anchor for the platform
requirements.

### Repeated rendering is byte-identical

**Test**: `DocDownPdfRendering_Render_Deterministic_ProducesByteIdenticalPages`

Proves two renders of the same document in the same environment produce byte-identical page images, so
a consumer can diff two extractions meaningfully. Evidence for `DocDownPdfRendering-Determinism`.

### The rendering backend wins when pages are requested

**Test**: `DocDownPdfRendering_Select_PagesRequested_RenderingBackendWins`

Proves selection prefers the rendering backend when page rendering is requested, that pages are
produced, and that a clean render completes without notes. Evidence for
`DocDownPdfRendering-SelectedWhenPagesRequested`.

### The managed backend wins when pages are not requested

**Test**: `DocDownPdfRendering_Select_PagesNotRequested_BaseBackendWins`

Proves that with rendering not requested the lighter managed backend wins on the identifier tie-break
and produces no pages, so a plain extraction touches no native code. Evidence for
`DocDownPdfRendering-BaseSelectedWhenNotRequested`.

### A requested page range restricts which pages render

**Test**: `DocDownPdfRendering_Render_PageRange_RestrictsRenderedPages`

Proves only the in-range pages are rasterized, named by their document page number. Evidence for
`DocDownPdfRendering-HonorsPageRange`.

### A higher DPI produces a larger page

**Test**: `DocDownPdfRendering_Render_HigherDpi_ProducesLargerPage`

Proves the requested DPI controls the raster: a higher-DPI render is strictly larger in both
dimensions than a lower-DPI render of the same document. Evidence for `DocDownPdfRendering-HonorsDpi`.

### Availability reporting is cheap, honest, and selection-relevant

**Tests**:
`PdfPageRenderingExtractor_ProbeAvailability_AnyEnvironment_ReflectsNativeStackWithoutThrowing`,
`PageRenderer_ProbeAvailability_AnyEnvironment_ReflectsUsabilityWithoutThrowing`,
`NativeProbeResult_Unavailable_CarriesReason`

The first test proves the extractor reports rendered-page support only when the native stack is usable
and otherwise reports an unavailable reason; the second proves the native seam itself answers
cheaply and without throwing; the third proves the unavailable result carries a displayable reason.
Evidence for `DocDownPdfRendering-ReportsRenderedPagesAvailability`,
`DocDownPdfRendering-ProbeCheapAndSafe`, and
`DocDownPdfRendering-ProbeReportsUnavailableWithReason`.

### The managed aspects are delivered by delegation

**Test**: `PdfPageRenderingExtractor_ExtractAsync_RenderRequested_WritesPagePngs`

Proves the backend delegates the managed aspects — `content.md` is written — and adds a valid page
PNG, so both the delegation and the rasterization happen in one run. Evidence for
`DocDownPdfRendering-ManagedInputsDelegated`.

### A per-page render fault becomes a note without throwing

**Test**: `PdfPageRenderingExtractor_ExtractAsync_PageRenderFaults_ReportsNoteWithoutThrowing`

Proves a faulting rasterization records the plain note `Page 1 could not be rasterized.`, produces no
page, still returns `Produced`, and does not throw to the caller. Evidence for
`DocDownPdfRendering-PerPageFailureReportedAsNote`.

### Native calls are serialized

**Test**: `PageRenderer_Render_ConcurrentCalls_AllProduceValidPng`

Proves several renders driven in parallel all produce valid PNGs, which is the observable proof that
the process-wide lock keeps concurrent callers from corrupting PDFium's shared native state. Evidence
for `DocDownPdfRendering-NativeCallsSerialized`.

### The registration seam is explicit, chainable, and native-type-free

**Tests**: `AddPdfRendering_OnBuilder_RegistersRenderingExtractor`,
`AddPdfRendering_OnBuilder_ReturnsSameBuilderForChaining`, `AddPdfRendering_NullBuilder_Throws`,
`PublicApi_AllPublicMembers_ExposeNoNativeRendererTypes`

Prove the seam registers exactly the rendering extractor, returns the builder for chaining, rejects a
null builder at the call site, and exposes no PDFtoImage, PDFium, or SkiaSharp type on the public
surface. Evidence for `DocDownPdfRendering-Registration`.

### The self-test proves the native stack genuinely rasterizes

**Test**: `PdfPageRenderingExtractor_GetSelfTestCases_AnyEnvironment_ReportsHonestRenderCaseStatus`

Proves the backend contributes a distinctly named render round-trip case that rasterizes a document it
builds itself and reports pass or skip honestly for the current environment. Evidence for
`DocDownPdfRendering-SelfValidation`.
