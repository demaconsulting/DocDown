## VisioComAvailability Verification Design

This document describes the unit-level verification strategy for `VisioComAvailability`, the
rendering-availability probe.

### Verification Approach

`VisioComAvailability` is verified through unit tests in `Com/VisioComAvailabilityTests.cs` in
`DemaConsulting.DocDown.Visio.Tests`. The probe is asserted directly, including its off-Windows path, which
CI reaches wherever it runs on Linux and macOS, and the property that no reason it returns instructs an
installation.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: none beyond the current environment; no filesystem or network access
- **Mocking**: none; the probe reads the real operating system and, on Windows, the real registry
- **Isolation**: each test asserts one property of the probe result

### Acceptance Criteria

Per IEC 62304 §5.5.2, a `VisioComAvailability` unit test run passes when the probe answers cheaply without
throwing and without instructing an installation, and reports unavailable off Windows with a declarative
reason that names the operating system. A throwing probe or an install-instructing reason is a failure.

### Test Scenarios

#### The probe never instructs an installation

**Test**: `VisioComAvailability_Probe_NeverInstructsInstallation`

Proves the probe answers without throwing and that no reason it returns instructs an installation — it
states a fact about the environment the reader cannot act on. Evidence for
`DocDownVisio-Com-VisioComAvailability-ProbesWithoutSideEffects`.

#### The probe reports unavailable off Windows with a declarative reason

**Test**: `VisioComAvailability_Probe_OffWindows_IsUnavailable`

Proves the probe reports unavailable off Windows with a declarative reason naming the operating system,
never an installation instruction. Evidence for
`DocDownVisio-Com-VisioComAvailability-ReportsDeclarativeReason`.
