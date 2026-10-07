## CanvasNet.Charts Verification

This document provides the verification evidence for the CanvasNet.Charts OTS software item.
Requirements for this OTS item are defined in the CanvasNet.Charts OTS Software Requirements
document.

### Required Functionality

CanvasNet.Charts is the managed chart-rendering library CanvasNet.Pptx uses internally to rasterize
an embedded chart on a slide. `DocDown.PowerPoint.Rendering` calls no member of it directly; it must
only be present, resolve at restore/publish time, and carry no native asset.

### Verification Approach

**CanvasNet.Charts is verified by transitive evidence from the `DocDown.PowerPoint.Rendering` test
suite.** No type from CanvasNet.Charts is named in this package's code, so there is no direct call
site to test. Its correctness as a chart renderer is CanvasNet.Pptx's own concern; this package's
only stake in it is that the dependency resolves and that the package graph as a whole produces a
real, decodable slide PNG, which the end-to-end rendering test below confirms. No
`test/OtsSoftwareTests/` project is created, because there is no direct call to re-test.

### Test Environment

The evidence is produced by the standard `DocDown.PowerPoint.Rendering` test run: xUnit v3 under the
.NET SDK, targeting net8.0, net9.0, and net10.0, across the full CI operating-system matrix.

### Test Scenarios

#### Package resolves and the dependent render path still produces a valid PNG

**Tests**: `DocDownPowerPointRendering_Render_ProbePresentation_ProducesValidPngPages`

This end-to-end test renders the embedded probe presentation through the full package graph,
including `CanvasNet.Charts` as a resolved dependency of `CanvasNet.Pptx`, and confirms the result is
a valid, decodable PNG. Evidence for `DocDown-OTS-CanvasNetCharts`.
