## PowerPointComAvailability Verification Design

This document describes the unit-level verification strategy for `PowerPointComAvailability`, the cheap,
side-effect-free rendering-availability probe.

### Verification Approach

`PowerPointComAvailability` is verified through unit tests in `Com/PowerPointComAvailabilityTests.cs` in
`DemaConsulting.DocDown.PowerPoint.Tests`. The probe is asserted directly, including its off-Windows path,
which CI reaches wherever it runs on Linux and macOS. The tests confirm it never throws, never instructs an
installation, and reports unavailable off Windows with a declarative reason.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: none beyond the ambient operating system; no filesystem and no network access
- **Mocking**: none; the real environment probe is exercised
- **Isolation**: each test calls the static probe independently

### Acceptance Criteria

Per IEC 62304 §5.5.2, a `PowerPointComAvailability` unit test run passes when the probe never throws, never
returns a reason containing an installation instruction, and reports unavailable with a declarative,
operating-system-naming reason off Windows. Any throw, install-instructing reason, or false-available result
off Windows is a failure.

### Test Scenarios

#### The probe never throws and never instructs an installation

**Test**: `PowerPointComAvailability_Probe_NeverThrowsAndNeverInstructsInstallation`

Proves the probe completes without throwing and returns no reason that instructs an installation, honoring
the no-throw obligation and the declarative-reason convention. Evidence for
`DocDownPowerPoint-Com-PowerPointComAvailability-ProbesWithoutSideEffects`.

#### The probe reports unavailable off Windows with a declarative reason

**Test**: `PowerPointComAvailability_Probe_OffWindows_IsUnavailable`

Proves the probe reports unavailable off Windows, naming the operating system as the reason the reader cannot
act on. Evidence for `DocDownPowerPoint-Com-PowerPointComAvailability-ReportsDeclarativeReason`.
