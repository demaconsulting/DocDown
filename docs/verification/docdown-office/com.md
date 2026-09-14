# DocDown.Office Com Verification Design

This document describes the verification strategy for the Com subsystem of `DocDown.Office`: the
availability probe and the composition helpers the PowerPoint and Visio automation backends share.

## Verification Approach

The subsystem is verified through the unit tests in
`DemaConsulting.DocDown.Office.Tests/PowerPoint/Com` and
`DemaConsulting.DocDown.Office.Tests/Visio/Com`, running on xUnit v3 across net8.0, net9.0, and
net10.0. Because the shared types are reached through each backend's own named entry point, they are
verified through those entry points rather than directly.

### The probe is verified where the application is absent, not only where it is present

Every CI runner lacks Microsoft Office, so the unavailable path is the one CI exercises — and it is
the path that matters most, because it is what a user without Office will hit. The tests assert the
probe returns a result rather than throwing, and that off Windows the result is unavailable and names
the operating system.

That the *available* path works is proven functionally instead, by the COM render self-tests passing
on a machine where Visio and PowerPoint are installed.

### Reasons are checked for what they must not say

A reason is asserted not to instruct an installation. This is a wording rule with a reason behind it:
the reader of a summary is usually an agent that cannot install anything, so a reason it cannot act
on should state a fact about the environment rather than issue an instruction.

### Composition is verified through a stub, not through Office

`ComposingDelegatedSink` and `DelegatedExtractionContext` are exercised by driving a COM extractor
through a stub automation adapter. That makes the suppression behavior testable on every platform,
including the runners with no Office at all, and isolates it from whether a real render succeeds.

## Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Mocking**: a stub automation adapter stands in for the COM boundary
- **Office automation**: not required by these tests; the availability tests assert the unavailable
  path, and the composition tests use the stub
- **Coverage**: the automation adapters and dispatch helpers these types sit beside are excluded from
  coverage measurement, because every statement in them runs inside an Office application over COM
  that no CI runner has. That exclusion is the repository's Code Coverage Policy applied to an
  interop seam, and it does not extend to the types verified here, which are fully measured.

## Acceptance Criteria

- The probe never throws, on any platform.
- Off Windows, the probe reports unavailable and names the operating system.
- No unavailable reason instructs an installation.
- A delegated managed run's "rendering not provided" fact is suppressed when the COM run rendered,
  and nothing else the delegate wrote is altered.

## Test Scenarios

### The probe answers without throwing and without instructing an installation

**Tests**: `PowerPointComAvailability_Probe_NeverThrowsAndNeverInstructsInstallation`,
`VisioComAvailability_Probe_NeverInstructsInstallation`

Calls the probe and asserts it returned rather than threw, and that the reason, when unavailable,
contains no instruction to install. Evidence for `DocDownOffice-Com-Availability`.

### Off Windows the probe reports unavailable

**Tests**: `PowerPointComAvailability_Probe_OffWindows_IsUnavailable`,
`VisioComAvailability_Probe_OffWindows_IsUnavailable`

Asserts that on a non-Windows platform the result is unavailable and its reason names the operating
system, so the reader learns why rather than only that. Evidence for
`DocDownOffice-Com-Availability`.

### The delegate's rendering statement does not contradict the rendering

**Tests**: `PowerPointComExtractor_Extract_ViaStub_SuppressesContradictoryPageRenderingFact`,
`VisioComExtractor_Extract_ViaStub_SuppressesContradictoryPageRenderingFact`

Drives a COM extractor through the stub adapter and asserts the managed delegate's
`<backend>.pageRendering` unavailable fact does not appear in the output, while the rendering fact
the COM run contributed does. Evidence for `DocDownOffice-Com-DelegatedComposition`.
