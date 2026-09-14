### SelfTestAdapter Verification Design

This document describes the unit-level verification strategy for `SelfTestAdapter`, which maps Core's
self-test records into the TestResults model.

### Verification Approach

`SelfTestAdapter` is verified through unit tests in `SelfTest/SelfTestAdapterTests.cs` in
`DemaConsulting.DocDown.Tool.Tests`, with method names beginning with `SelfTestAdapter_`. The mapping
is pure, so the tests call the adapter directly and assert on the returned outcome or result — no
engine, no filesystem, and no serialization are involved, keeping the tests scoped to the mapping
itself.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net10.0, the single framework the tool ships on
- **Inputs**: constructed `SelfTestCase` and `SelfTestResult` values
- **Isolation**: each test constructs its own inputs

### Acceptance Criteria

Per IEC 62304 §5.5.2, a `SelfTestAdapter` unit test run passes when a passed status maps to a passed
outcome and a failed status to a failed outcome; when a skipped status maps to a not-executed outcome
distinct from a failure; and when a mapped result preserves the case name, category, duration, and
message, including a failure detail.

### Test Scenarios

#### Executed statuses map to their outcomes

**Tests**: `SelfTestAdapter_ToTestOutcome_Passed_ReturnsPassed`,
`SelfTestAdapter_ToTestOutcome_Failed_ReturnsFailed`

Prove a pass maps to a passed outcome and a failure to a failed outcome. Evidence for
`DocDownTool-SelfTestAdapter-MapsOutcomes`.

#### A skip maps to not-executed

**Test**: `SelfTestAdapter_ToTestOutcome_Skipped_ReturnsNotExecuted`

Proves a skip maps to a not-executed outcome that is not equal to a failure — the load-bearing
distinction a traceability pipeline relies on. Evidence for
`DocDownTool-SelfTestAdapter-SkippedIsNotExecuted`.

#### The mapping preserves case metadata

**Tests**: `SelfTestAdapter_ToTestResult_PreservesNameCategoryDurationAndMessage`,
`SelfTestAdapter_ToTestResult_FailedCase_CarriesMessage`

Prove the mapped result carries the case name, its category as the class name, the duration, the
outcome, and any skip reason or failure detail. Evidence for
`DocDownTool-SelfTestAdapter-PreservesMetadata`.
