## CanvasNet.Charts

### Purpose

`DemaConsulting.CanvasNet.Charts` is the managed chart-rendering library `CanvasNet.Pptx` uses
internally to rasterize an embedded chart on a slide. `DocDown.PowerPoint.Rendering` does not call
it directly or name any of its types; it is referenced only so the dependency resolves at restore
and publish time for the `CanvasNet.Pptx` version this package pins. It was chosen — indirectly, as
part of adopting `CanvasNet.Pptx` — because it is fully managed with no native asset and MIT
licensed, compatible with this repository's MIT license.

### Features Used

None directly. `DocDown.PowerPoint.Rendering` references `CanvasNet.Charts` as a package reference
only to pin its version alongside `CanvasNet.Pptx` and `CanvasNet`; no member of `CanvasNet.Charts`
is called from this package's code. `CanvasNet.Pptx` uses it internally when a slide embeds a chart.

### Integration Pattern

`CanvasNet.Charts` is referenced as a real runtime dependency of the `DocDown.PowerPoint.Rendering`
package so the three CanvasNet packages (`CanvasNet`, `CanvasNet.Pptx`, `CanvasNet.Charts`) resolve
to the same pinned version at restore time, rather than letting NuGet pick a potentially mismatched
transitive version of `CanvasNet.Charts` through `CanvasNet.Pptx` alone.

**Version pinning.** Pinned to the same exact version as `CanvasNet.Pptx` and `CanvasNet`, for the
same reproducibility reason: this is a pre-1.0, beta-labeled release, and an independently floating
transitive reference could desynchronize from the version `CanvasNet.Pptx` was built and tested
against.

**Containment as a risk control.** No `CanvasNet.Charts` type is named anywhere in this package's
code, so there is nothing to contain beyond the package reference itself; the same reflection test
that fails the build on a `CanvasNet.Pptx` or `CanvasNet` type reaching the public surface also
covers `CanvasNet.Charts`.

**Native assets.** None. `CanvasNet.Charts` is fully managed, matching `CanvasNet.Pptx` and
`CanvasNet`. `DocDown.PowerPoint.Rendering`'s published output contains no native binary of any
kind.

### Licensing

`CanvasNet.Charts` is MIT, published by DEMA Consulting — the same license and maintainer as
`CanvasNet.Pptx` and `CanvasNet`. No copyleft license appears anywhere in the graph.
