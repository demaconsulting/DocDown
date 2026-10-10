## VisioDocDownBuilderExtensions Verification Design

This document describes the unit-level verification strategy for `VisioDocDownBuilderExtensions`, the
registration seam.

### Verification Approach

`VisioDocDownBuilderExtensions` is verified through unit tests in `VisioDocDownBuilderExtensionsTests.cs`
in `DemaConsulting.DocDown.Office.Tests`. The tests build a real `DocDownBuilder`, call `AddVisio`, and
assert the resulting registration: the managed backend present, the same builder returned, and a null
builder rejected at the call site.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: a real `DocDownBuilder`; no filesystem or network access
- **Mocking**: none; the real builder is used
- **Isolation**: each test builds its own builder

### Acceptance Criteria

Per IEC 62304 §5.5.2, a `VisioDocDownBuilderExtensions` unit test run passes when `AddVisio` registers the
managed Open Packaging backend, returns the same builder it was called on, and rejects a null builder
with `ArgumentNullException` at the point of the call.

### Test Scenarios

#### The managed backend is registered

**Test**: `AddVisio_RegistersOpenXmlBackend`

Proves the single call adds the managed backend as a deferred factory. Evidence for
`DocDownVisio-VisioDocDownBuilderExtensions-RegistersManagedBackend`.

#### The builder is returned for chaining

**Test**: `AddVisio_ReturnsSameBuilder`

Proves `AddVisio` returns the same builder so registration reads as one configuration sequence. Evidence
for `DocDownVisio-VisioDocDownBuilderExtensions-ReturnsBuilderForChaining`.

#### A null builder is rejected at the call site

**Test**: `AddVisio_NullBuilder_Throws`

Proves a null builder is rejected with `ArgumentNullException` at the point of the call. Evidence for
`DocDownVisio-VisioDocDownBuilderExtensions-RejectsNullBuilder`.
