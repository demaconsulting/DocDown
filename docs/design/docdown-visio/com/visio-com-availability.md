## VisioComAvailability

![DocDown.Visio Structure](DocDownVisioView.svg)

### Purpose

`VisioComAvailability` probes whether the Visio COM automation backend can run in the current environment.
Its single responsibility is a cheap, side-effect-free, non-throwing yes-or-no with a declarative reason,
so backend selection can keep the environment-dependent rendering backend honest before any drawing is
opened.

### Data Model

`VisioComAvailability` is an `internal static class`. It holds no state; the only mutable knowledge it
consults is the operating system and, on Windows, the registry.

### Key Methods

- **`ExtractorAvailability Probe(ExtractorCapabilities declared)`** — returns unavailable off Windows,
  naming the operating system; on Windows, returns available with the declared capabilities when the Visio
  ProgID resolves, and otherwise unavailable stating that Visio is not registered.
- **`bool IsVisioRegistered()`** (private, Windows-only) — reports whether the `Visio.Application` ProgID
  resolves, a registry lookup only that does not activate Visio; any fault is treated as "not registered"
  so the probe honors its no-throw obligation.

### Error Handling

The probe never throws: the non-Windows path returns unavailable without touching the registry, and the
registry lookup catches any fault and reports "not registered". Its unavailable reasons use the word
"available", never "install", so they state a fact about the environment the reader cannot act on rather
than instructing an installation.

### Dependencies

- **DocDown.Core** — `ExtractorAvailability` and `ExtractorCapabilities`.
- **System.Runtime.InteropServices** / **System.Runtime.Versioning** — the OS description and the
  Windows-only guard on the registry read.

### Callers

`VisioComExtractor.ProbeAvailability` calls `Probe` when the engine asks whether the COM backend can run,
and `VisioComExtractor.RunAvailable` calls it from the self-test case.
