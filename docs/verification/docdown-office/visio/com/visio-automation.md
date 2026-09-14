## VisioAutomation Verification Design

This document describes the unit-level verification strategy for `VisioAutomation`, the real COM
automation adapter.

### Verification Approach

`VisioAutomation` is the single untestable boundary: it talks to Microsoft Visio over late-bound COM
on Windows only, so CI cannot exercise it directly. Its observable seam contract — render each
foreground page at the requested resolution, return one PNG-or-reason result per page, and release
its session — is proved in CI through the `VisioComExtractor` unit tests, which drive an injected
stub `IVisioAutomation` and assert the extractor consumes the seam's output correctly.

The adapter's internal correctness — read-only open, single session, point-to-pixel PNG export, and
deterministic session teardown — is proven by release-time self-tests, not by CI. There is
therefore no dedicated CI test class for this unit.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0 for seam
  contract coverage; release-time self-tests on a Windows host with Microsoft Visio for internal
  behavior
- **Inputs**: for the seam contract, the stub-driven `VisioComExtractor` tests; for the internal
  behavior, a real drawing rendered by a real Visio session at release time
- **Mocking**: the seam is exercised through the injected stub in the extractor tests; the real
  adapter is never mocked, only self-tested
- **Isolation**: the seam-contract tests own their drawing and scratch folder

### Acceptance Criteria

Per IEC 62304 §5.5.2, a `VisioAutomation` verification passes when the seam contract holds in CI —
the requested resolution reaches the seam, one result per page is produced, a per-page failure is
returned as a failure reason rather than an exception, and the session is released — and when the
release-time self-test confirms the real adapter opens read-only, exports every foreground page to a
PNG, and tears its session down deterministically.

### Test Scenarios

#### Each page renders at the requested resolution

**Test**: `VisioComExtractor_Extract_PassesRenderDpiToAutomation`

Proves the requested resolution reaches the automation seam. The point-to-pixel export at that
resolution is proved by the release-time self-tests. Evidence for
`DocDownVisio-Com-VisioAutomation-RendersEachPageAtRequestedDpi`.

#### One result per page, PNG or failure reason

**Test**: `VisioComExtractor_Extract_PageRenderFails_ReportsNote`

Proves the seam returns one result per page, each carrying either PNG bytes or a per-page failure
reason, so a single unrenderable page does not prevent the remaining pages from being returned.
Evidence for `DocDownVisio-Com-VisioAutomation-ProducesPerPageResults`.

#### The session is owned and released

**Test**: `VisioComExtractor_Extract_ViaStub_WritesTopologyAndRendersEveryPage`

Proves the seam is disposed after each render so no COM object outlives the render. The forced,
deterministic teardown of the real adapter is proved by the release-time self-tests. Evidence for
`DocDownVisio-Com-VisioAutomation-ReleasesItsSession`.
