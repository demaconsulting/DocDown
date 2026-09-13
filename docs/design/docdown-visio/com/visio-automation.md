## VisioAutomation

![DocDown.Visio Structure](DocDownVisioView.svg)

### Purpose

`VisioAutomation` is the real COM automation adapter: the single unit that talks to Microsoft Visio over
late-bound IDispatch. Its single responsibility is to render a drawing's foreground pages to PNGs; it is
the whole untestable COM boundary and is deliberately mechanical, holding no extraction or gap policy.

### Data Model

`VisioAutomation` is an `internal sealed class` implementing `IVisioAutomation` (and therefore
`IDisposable`). It owns a Visio session for the duration of a render and releases it on disposal. It is
Windows-only and is never constructed off Windows.

### Key Methods

- **`IReadOnlyList<VisioRenderedPage> Render(string path, int dpi)`** — opens the drawing read-only,
  exports every foreground page to a PNG at the requested resolution — cropping to the drawing extent so
  pixel dimensions are read back from the written image — and returns one result per page in document
  order, each carrying either the page's PNG bytes or a per-page failure reason. The whole render runs in
  one session so no COM object outlives the call.
- **`Dispose()`** — releases the Visio session, so no COM object outlives the render and the owning process
  does not leak.

The observable seam contract — render each page at the requested resolution, return one PNG-or-reason
result per page, and release the session — is what the extractor consumes and what is exercised in CI
through the injected stub. The adapter's internal correctness (read-only open, the single session, the PNG
export, the deterministic teardown) is proven by the release-time self-test cases, not by CI.

### Error Handling

A per-page export fault is caught and turned into that page's failure reason rather than an exception, so a
single unrenderable page degrades the run rather than aborting it. A whole-session fault surfaces as a
`VisioExtractionException` the extractor's caller (Core) converts into a structured failure. The session is
released on every path.

### Dependencies

- **IVisioAutomation** — the seam it implements, and the `VisioRenderedPage` result type it returns.
- **VisioComDispatch** — the low-level IDispatch plumbing (activation, invocation, wrapper release) it is
  written on top of, so this unit reads as a session script.
- **Microsoft Visio** — reached over late-bound IDispatch; Windows-only and environment-dependent.

### Callers

`VisioComExtractor` constructs it through `CreateDefaultAutomation` and calls `Render` once per extraction
that requests rendered pages where Visio is available, disposing it after each render.
