## PowerPointDocDownBuilderExtensions Verification Design

This document describes the unit-level verification strategy for `PowerPointDocDownBuilderExtensions`, the
reflection-free registration seam for the PowerPoint backends.

### Verification Approach

`PowerPointDocDownBuilderExtensions` is verified through unit tests in
`PowerPointDocDownBuilderExtensionsTests.cs` in `DemaConsulting.DocDown.Office.Tests`. The tests build a
real `DocDownBuilder`, call `AddPowerPoint`, and assert the resulting registration: that both backends are
present, that the managed backend outranks the COM backend, that the call returns the same builder, and that
a null builder is rejected at the call site.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: a real `DocDownBuilder`; no filesystem and no network access
- **Mocking**: none; the real builder and real extractor descriptors are exercised
- **Isolation**: each test constructs its own builder

### Acceptance Criteria

Per IEC 62304 §5.5.2, a `PowerPointDocDownBuilderExtensions` unit test run passes when `AddPowerPoint`
registers exactly the managed Open XML and COM automation backends, the managed backend's selection priority
is higher than the COM backend's, the method returns the builder it was called on, and a null builder raises
`ArgumentNullException` at the call site. Any missing backend, wrong priority order, or deferred null failure
is a failure.

### Test Scenarios

#### Both backends are registered

**Test**: `AddPowerPoint_RegistersOpenXmlAndComBackends`

Proves the call registers the managed Open XML backend and the COM automation backend on the builder.
Evidence for `DocDownPowerPoint-PowerPointDocDownBuilderExtensions-RegistersBothBackends`.

#### The managed backend outranks the COM backend

**Test**: `AddPowerPoint_ManagedBackend_HasHigherPriorityThanCom`

Proves the managed backend is registered at a higher selection priority than the COM backend, so it is the
default for any extraction that does not request rendered pages. Evidence for
`DocDownPowerPoint-PowerPointDocDownBuilderExtensions-OrdersManagedAboveCom`.

#### The call returns the same builder

**Test**: `AddPowerPoint_ReturnsSameBuilder`

Proves the method returns the builder it was called on, so registration reads as one fluent sequence.
Evidence for `DocDownPowerPoint-PowerPointDocDownBuilderExtensions-ReturnsBuilderForChaining`.

#### A null builder is rejected at the call site

**Test**: `AddPowerPoint_NullBuilder_Throws`

Proves a null builder raises `ArgumentNullException` where the call was made, rather than deferring the
failure to build time. Evidence for
`DocDownPowerPoint-PowerPointDocDownBuilderExtensions-RejectsNullBuilder`.
