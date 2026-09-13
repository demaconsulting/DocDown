## SkiaSharp Verification

This document provides the verification evidence for the SkiaSharp OTS software item. Requirements for
this OTS item are defined in the SkiaSharp OTS Software Requirements document.

### Required Functionality

SkiaSharp is the 2D graphics library that holds the rasterized page bitmap and encodes it to PNG. It
must encode a rasterized page bitmap to deterministic PNG bytes.

### Verification Approach

**SkiaSharp is verified by transitive evidence from the `DocDown.Pdf.Rendering` test suite.** Per the
software-items standard, a dedicated OTS test project is required only *if no other verification
evidence is available*. That is not the case: SkiaSharp performs the PNG encode of every rendered page,
and the tests named below assert on the PNG signature, header, and byte-for-byte reproducibility that
originate entirely in SkiaSharp's encode step. No `test/OtsSoftwareTests/` project is created, because
it would re-test the same encode path the rendering suite already exercises.

The evidence is limited to the specific tests that assert on SkiaSharp's PNG output — the single-page
render that checks the signature and header, and the determinism test that checks byte-for-byte
reproducibility. No broader set is claimed.

### Test Environment

The evidence is produced by the standard `DocDown.Pdf.Rendering` test run: xUnit v3 under the .NET SDK,
targeting net8.0, net9.0, and net10.0, across the CI operating-system matrix where the SkiaSharp native
backend is deployed. Every fixture is generated at test time.

### Test Scenarios

#### A rasterized page encodes to a valid, deterministic PNG

**Tests**: `PageRenderer_Render_SinglePage_ReturnsValidPng`,
`DocDownPdfRendering_Render_Deterministic_ProducesByteIdenticalPages`

The first proves SkiaSharp's encode produces bytes carrying the PNG signature and a readable header —
a valid PNG. The second proves two encodes of the same rendered page are byte-identical, which is the
determinism the PNG encode must provide for a consumer to diff two extractions meaningfully. Together
they are direct evidence that SkiaSharp encodes a rasterized page bitmap to deterministic PNG bytes.
Evidence for `DocDown-OTS-SkiaSharp`.
