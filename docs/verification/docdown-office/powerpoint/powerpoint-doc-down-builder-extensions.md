## PowerPointDocDownBuilderExtensions Verification Design

This document describes the unit-level verification strategy for `PowerPointDocDownBuilderExtensions`, the
reflection-free registration seam for the PowerPoint backend.

### Verification Approach

`PowerPointDocDownBuilderExtensions` is verified through unit tests in
`PowerPointDocDownBuilderExtensionsTests.cs` in `DemaConsulting.DocDown.Office.Tests`. The tests build a
real `DocDownBuilder`, call `AddPowerPoint`, and assert the resulting registration: that the managed
backend is present, that the call returns the same builder, and that a null builder is rejected at the
call site.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: a real `DocDownBuilder`; no filesystem and no network access
- **Mocking**: none; the real builder and real extractor descriptors are exercised
- **Isolation**: each test constructs its own builder

### Acceptance Criteria

Per IEC 62304 §5.5.2, a `PowerPointDocDownBuilderExtensions` unit test run passes when `AddPowerPoint`
registers exactly the managed Open XML backend, the method returns the builder it was called on, and a
null builder raises `ArgumentNullException` at the call site. Any missing backend, extra backend, or
deferred null failure is a failure.

### Test Scenarios

#### The managed backend is registered

**Test**: `AddPowerPoint_RegistersOpenXmlBackend`

Proves the call registers exactly the managed Open XML backend on the builder.
Evidence for `DocDownPowerPoint-PowerPointDocDownBuilderExtensions-RegistersManagedBackend`.

#### The call returns the same builder

**Test**: `AddPowerPoint_ReturnsSameBuilder`

Proves the method returns the builder it was called on, so registration reads as one fluent sequence.
Evidence for `DocDownPowerPoint-PowerPointDocDownBuilderExtensions-ReturnsBuilderForChaining`.

#### A null builder is rejected at the call site

**Test**: `AddPowerPoint_NullBuilder_Throws`

Proves a null builder raises `ArgumentNullException` where the call was made, rather than deferring the
failure to build time. Evidence for
`DocDownPowerPoint-PowerPointDocDownBuilderExtensions-RejectsNullBuilder`.
