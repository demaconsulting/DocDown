### PowerPointComAvailability

![DocDown.PowerPoint Structure](PowerPointView.svg)

### Purpose

`PowerPointComAvailability` probes whether the PowerPoint COM automation backend can run in the current
environment. Its single responsibility is a cheap, side-effect-free availability decision: it checks
the operating system first and then, on Windows, whether Microsoft PowerPoint is registered, so the
COM backend is selected only where it can actually render.

### Data Model

`PowerPointComAvailability` is an `internal static class`. It holds no state and reads only the
environment — the operating system and, on Windows, the registry through a ProgID lookup.

### Key Methods

- **`ExtractorAvailability Probe()`** — returns `Unavailable(reason)` naming the operating system when
  not on Windows; on Windows, returns `Available(providesRenderedPages: true)` when the
  `PowerPoint.Application` ProgID resolves and `Unavailable(reason)` otherwise. Cheap, side-effect
  free, and never throws.
The probe itself lives in the shared `OfficeComAvailability`, which PowerPoint and Visio use in
common: the two probes differed only in the application name and the ProgID, so those are arguments
rather than two copies of the logic. This type names PowerPoint's own entry point and supplies them.

### Error Handling

The probe never throws: the non-Windows path returns a declarative unavailable result, and the Windows
ProgID lookup catches every fault and reports "not registered". Its unavailable reasons use the word
"available", never "install", so they state a fact about the environment the reader cannot act on
rather than instructing an installation.

### Dependencies

- **DocDown.Core** — `ExtractorAvailability`.
- **System.Runtime.InteropServices** / **System.Runtime.Versioning** — `RuntimeInformation` for the
  reason text and the `SupportedOSPlatform` guard on the Windows-only lookup.

### Callers

`PowerPointComExtractor.ProbeAvailability` calls `Probe` when an automation factory is present, and
`PowerPointComExtractor.RunAvailable` calls it from the self-test case.
