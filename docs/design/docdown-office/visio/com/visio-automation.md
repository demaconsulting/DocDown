## VisioAutomation

![DocDown.Visio Structure](DocDownVisioView.svg)

### Purpose

`VisioAutomation` is the real COM automation adapter: the single unit that talks to Microsoft Visio
over late-bound IDispatch. Its single responsibility is to render a drawing's foreground pages to
PNGs. It is deliberately mechanical and holds no extraction or reporting policy.

### Data Model

`VisioAutomation` is an `internal sealed class` implementing `IVisioAutomation` and `IDisposable`.
It owns a Visio session for the duration of one render call and releases it before the call returns.
It is Windows-only and is never constructed off Windows.

### Key Methods

- **`IReadOnlyList<VisioRenderedPage> Render(string path, int dpi)`** — opens the drawing
  read-only, exports every foreground page to a PNG at the requested resolution, and returns one
  result per page in document order, each carrying either PNG bytes or a per-page failure reason.
  The whole render runs in one owned session.
- **`Dispose()`** — releases any retained resources. In practice the session is fully torn down
  before `Render` returns, so disposal has nothing left to release.

The observable seam contract — render each page at the requested resolution, return one
PNG-or-reason result per page, and release the session — is what the extractor consumes and what CI
proves through the injected stub. The adapter's internal correctness, including watchdog-bounded
session ownership and deterministic teardown, is proved by release-time self-tests.

### Error Handling

A per-page export fault is caught and turned into that page's failure reason rather than an
exception, so the caller can continue rendering the remaining pages. A whole-session fault surfaces
as a `VisioExtractionException` that Core converts into an unreadable result. The session is torn
down on every path.

### Dependencies

- **`IVisioAutomation`** — the seam it implements and the `VisioRenderedPage` result type it
  returns.
- **`VisioComDispatch`** — the low-level IDispatch plumbing it is written on top of.
- **Microsoft Visio** — reached over late-bound IDispatch; Windows-only and environment-dependent.

### Callers

`VisioComExtractor` constructs it through `CreateDefaultAutomation()` and calls `Render()` once per
extraction that requests rendered pages where Visio is available, disposing it after each render.
