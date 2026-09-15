## VisioComAvailability

![DocDown.Visio Structure](VisioView.svg)

### Purpose

`VisioComAvailability` probes whether the Visio COM automation backend can run in the current
environment. Its single responsibility is a cheap, side-effect-free, non-throwing yes-or-no result
with a declarative reason and rendered-page support flag, so backend selection can stay honest
before any drawing is opened.

### Data Model

`VisioComAvailability` is an `internal static class`. It holds no state; the only mutable knowledge
it consults is the operating system and, on Windows, the registry.

### Key Methods

- **`ExtractorAvailability Probe()`** — returns `Unavailable(...)` off Windows, naming the operating
  system; on Windows, returns `Available(providesRenderedPages: true)` when the `Visio.Application`
  ProgID resolves, and otherwise `Unavailable(...)` stating that Microsoft Visio is not registered.
The probe itself lives in the shared `OfficeComAvailability`, which Visio and PowerPoint use in
common: the two probes differed only in the application name and the ProgID, so those are arguments
rather than two copies of the logic. This type names Visio's own entry point and supplies them.

### Error Handling

The probe never throws. The non-Windows path returns unavailable without touching the registry, and
the registry lookup treats any fault as "not registered". Unavailable reasons use the word
"available", never "install", so they state a fact about the environment rather than giving an
instruction.

### Dependencies

- **DocDown.Core** — `ExtractorAvailability`.
- **System.Runtime.InteropServices** / **System.Runtime.Versioning** — the OS description and the
  Windows-only guard on the registry read.

### Callers

`VisioComExtractor.ProbeAvailability` calls `Probe()` when the engine asks whether the COM backend
can run, and `VisioComExtractor.RunAvailable` calls it from the self-test case.
