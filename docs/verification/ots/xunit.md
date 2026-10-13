## xUnit Verification

This document provides the verification evidence for the xUnit OTS software item. Requirements
for this OTS item are defined in the xUnit OTS Software Requirements document.

### Required Functionality

xUnit v3 (xunit.v3 and xunit.runner.visualstudio) is the unit-testing framework used by every test
project in the repository. It discovers and runs all test methods and writes TRX result files that
feed into coverage reporting and requirements traceability. Passing tests confirm the framework is
functioning correctly.

### Verification Approach

xUnit is verified by transitive evidence from the repository's own tests. Each scenario below names
an ordinary DocDown test, in `DemaConsulting.DocDown.Core.Tests`, that xUnit must discover, execute,
and record in a TRX result file. There is no dedicated OTS test project: if xUnit did not discover
and execute these tests, no result would appear for them at all, and ReqStream would report the
requirement tests as missing. A passing pipeline run for all scenarios constitutes evidence that
both requirements are satisfied. The claim is limited to discovery, execution, and result
recording; nothing beyond that is asserted about the framework.

### Test Scenarios

#### DocDownCore_Extract_Failure_StillWritesSummaryAndManifest

**Scenario**: xUnit discovers and runs this `[Fact]`; the test verifies that an extraction whose
backend throws still returns an unreadable outcome and writes the full output layout.

**Expected**: xUnit executes the test, the test passes, and the result appears by name in the TRX
output.

**Requirement coverage**: `DocDown-OTS-xUnit-Execute`, `DocDown-OTS-xUnit-Report`.

#### DocDownCore_Extract_OptionsMutatedAfterCall_DoNotAffectResult

**Scenario**: xUnit discovers and runs this `[Fact]`; the test verifies that mutating the extraction
options after one call does not change that call's result.

**Expected**: xUnit executes the test, the test passes, and the result appears by name in the TRX
output.

**Requirement coverage**: `DocDown-OTS-xUnit-Execute`, `DocDown-OTS-xUnit-Report`.

#### DocDownCore_Extract_BackendUnavailable_ReasonAppearsInSummary

**Scenario**: xUnit discovers and runs this `[Fact]`; the test verifies that an unavailable
backend's reason appears verbatim in the summary when it is the only option.

**Expected**: xUnit executes the test, the test passes, and the result appears by name in the TRX
output.

**Requirement coverage**: `DocDown-OTS-xUnit-Execute`, `DocDown-OTS-xUnit-Report`.

### Requirements Coverage

- **`DocDown-OTS-xUnit-Execute`**: DocDownCore_Extract_Failure_StillWritesSummaryAndManifest,
  DocDownCore_Extract_OptionsMutatedAfterCall_DoNotAffectResult,
  DocDownCore_Extract_BackendUnavailable_ReasonAppearsInSummary
- **`DocDown-OTS-xUnit-Report`**: DocDownCore_Extract_Failure_StillWritesSummaryAndManifest,
  DocDownCore_Extract_OptionsMutatedAfterCall_DoNotAffectResult,
  DocDownCore_Extract_BackendUnavailable_ReasonAppearsInSummary
