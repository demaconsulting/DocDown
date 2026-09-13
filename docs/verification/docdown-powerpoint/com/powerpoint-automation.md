### PowerPointAutomation Verification Design

This document describes the unit-level verification strategy for `PowerPointAutomation`, the real
PowerPoint COM automation adapter — the single boundary CI cannot reach.

### Verification Approach

`PowerPointAutomation` talks to Microsoft PowerPoint over late-bound COM on Windows only, so it cannot
be exercised in the cross-platform CI matrix. Its verification is therefore split:

- **Observable seam contract (CI).** The contract the adapter must satisfy — render each slide at the
  requested resolution, return one PNG-or-reason result per slide, and release its session — is
  proved in CI through `Com/PowerPointComExtractorTests.cs`, which drives a stub
  `IPowerPointAutomation` and asserts the extractor consumes the seam's output correctly.
- **Real-adapter correctness (release-time self-tests).** The adapter's internal behavior — the
  read-only, no-window open; the watchdog-bounded single session; the point-to-pixel conversion; the
  PNG export; and the forced, deterministic process teardown that leaves no orphan — is proven by the
  release-time self-test cases run on a Windows machine with Microsoft PowerPoint present, not by CI.

### Test Environment

- **Framework (seam contract)**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0,
  driving a stub adapter on every platform
- **Environment (real adapter)**: a Windows machine with Microsoft PowerPoint installed, exercised by
  the self-test tool at release time
- **Mocking**: a stub `IPowerPointAutomation` in CI; the real adapter under the release-time
  self-tests
- **Isolation**: each seam-contract test builds its own deck and scratch folder

### Acceptance Criteria

Per IEC 62304 §5.5.2, a `PowerPointAutomation` verification passes when the seam contract holds in CI
— the requested resolution reaches the adapter, each slide yields exactly one PNG-or-reason result,
and the session is released after each render — and when the release-time self-tests confirm, on a
machine with PowerPoint present, that the adapter renders every slide read-only without prompting and
terminates its owned process deterministically. A resolution not passed through, a slide yielding
neither bytes nor a reason, a leaked COM object, or an orphaned process is a failure.

### Test Scenarios

#### Each slide renders at the requested resolution

**Test**: `PowerPointComExtractor_Extract_PassesRenderDpiToAutomation`

Proves the caller's render resolution reaches the automation seam. The point-to-pixel conversion and
PNG export at that resolution are proved by the release-time self-tests. Evidence for
`DocDownPowerPoint-Com-PowerPointAutomation-RendersEachSlideAtRequestedDpi`.

#### One result per slide, PNG bytes or a failure reason

**Test**: `PowerPointComExtractor_Extract_SlideRenderFails_ReportsNote`

Proves the seam returns one result per slide, each carrying either PNG bytes or a per-slide failure
reason, so a single unrenderable slide does not abort the remaining render work. Evidence for
`DocDownPowerPoint-Com-PowerPointAutomation-ProducesPerSlideResults`.

#### The session is owned and released

**Test**: `PowerPointComExtractor_Extract_ViaStub_WritesNotesAndRendersEverySlide`

Proves the adapter's session is disposed after the render. The forced, deterministic process teardown
that leaves no orphan is proved by the release-time process-release self-test through
`LastOwnedProcessId`. Evidence for
`DocDownPowerPoint-Com-PowerPointAutomation-ReleasesItsSession`.
