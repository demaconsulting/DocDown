## PowerPointAutomation

![DocDown.PowerPoint Structure](DocDownPowerPointView.svg)

### Purpose

`PowerPointAutomation` is the real PowerPoint COM automation adapter: the single unit that talks to
Microsoft PowerPoint over late-bound IDispatch. Its single responsibility is mechanical rendering — it
activates a PowerPoint session, opens the deck read-only, exports every slide to a PNG at the requested
resolution, and tears the session down deterministically. It is the whole untestable COM boundary and holds
no extraction or gap policy; which slides render, how failures become gaps, and how content is delegated
all live in the cross-platform-tested `PowerPointComExtractor`.

### Data Model

`PowerPointAutomation` is an `internal sealed class` implementing `IPowerPointAutomation`, marked
`[SupportedOSPlatform("windows")]`. It holds a five-minute render timeout, the PowerPoint automation
constants it needs (`ppAlertsNone`, `msoAutomationSecurityForceDisable`, the tri-state values), and a
`LastOwnedProcessId` exposed so the release-time process-release self-test can assert the started process
has exited.

### Key Methods

- **`IReadOnlyList<PowerPointRenderedSlide> Render(string path, int dpi)`** — the whole render. It runs one
  watchdog-guarded session through `PowerPointComDispatch.RunSession` and returns one result per slide.
  Precondition: `path` non-null and non-empty. The owned process id is captured even on a fault so the
  watchdog can terminate a hung host.
- **`RunOneShotSession`** (private) — activates PowerPoint, suppresses alerts, disables macros and add-ins,
  opens the deck read-only, exports every slide, closes the presentation, and — in `finally` — quits and
  releases every runtime callable wrapper in reverse acquisition order.
- **`OpenReadOnly`** / **`OpenNamedFallback`** (private) — open the deck read-only, without a window and
  without adding it to the recent-file list, falling back to named arguments when the positional open
  returns nothing.
- **`ExportSlides`** / **`ExportSlide`** (private) — convert the slide dimensions to pixels at the requested
  DPI and export each slide to a PNG in a scratch folder, isolating each slide's fault into a failure
  reason so one bad slide does not abort the render.
- **`ToPixels`** (private) — convert a point dimension to pixels at the given resolution, clamped to at
  least one pixel.
- **`QuitAndRelease`** / **`TryDeleteDirectory`** (private) — quit the application and release every wrapper
  regardless of a late quit failure, and delete the scratch folder, ignoring any cleanup fault.
- **`Dispose`** — a no-op: the session is fully torn down before `Render` returns, so nothing is retained.

### Error Handling

Every per-slide export fault is caught and returned as a `PowerPointRenderedSlide` failure reason rather
than thrown (so `CA1031` is deliberately suppressed there), and cleanup and teardown faults are swallowed
because a leftover temp file or a late quit must never mask the render outcome. An open that returns nothing
raises a `PowerPointExtractionException`. The whole session is watchdog-bounded, so a modal-dialog hang is
turned into a forced process termination rather than an indefinite stall.

### Dependencies

- **IPowerPointAutomation** — the seam it implements, and the `PowerPointRenderedSlide` result type.
- **PowerPointComDispatch** — the low-level IDispatch plumbing, single-instance activation, watchdog, and
  forced process termination.
- **PowerPointExtractionException** — raised when PowerPoint returns no presentation on open.
- **System.Runtime.Versioning** — the `SupportedOSPlatform` guard.

### Callers

`PowerPointComExtractor.CreateDefaultAutomation` constructs it on Windows, through the default automation
factory, and `RenderSlidesAsync` calls `Render`. Its correctness in a deployed environment is proven by the
release-time self-test cases, not by CI — the stub `IPowerPointAutomation` stands in for it everywhere
else.
