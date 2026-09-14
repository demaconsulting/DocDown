## ExcelDocDownBuilderExtensions Verification Design

This document describes the unit-level verification strategy for `ExcelDocDownBuilderExtensions`, the
registration seam that adds the Excel backend to a `DocDownBuilder`.

### Verification Approach

`ExcelDocDownBuilderExtensions` is verified through unit tests in `ExcelDocDownBuilderExtensionsTests.cs`
in `DemaConsulting.DocDown.Excel.Tests`, with method names beginning with `AddExcel_`.

The unit is exercised directly: a builder is constructed, `AddExcel` is called, and the resulting
registration is inspected. The tests prove exactly one backend is added and it is the managed Open XML
backend, that the same builder is returned so registration can be chained, and that a null builder is
rejected at the point of the call.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: a `DocDownBuilder` constructed in the test
- **Filesystem**: none
- **Mocking**: none; the real builder is used
- **Isolation**: each test constructs its own builder

### Acceptance Criteria

Per IEC 62304 §5.5.2, an `ExcelDocDownBuilderExtensions` unit test run passes when `AddExcel` registers
exactly one further extractor and it is the Open XML Excel backend; when the builder returned is the one
passed in; and when a null builder is rejected with `ArgumentNullException`. Any additional or missing
registration, a different builder returned, or a deferred null failure is a failure.

### Test Scenarios

#### AddExcel registers the single Open XML backend

**Test**: `AddExcel_RegistersSingleOpenXmlBackend`

Proves `AddExcel` adds the managed Open XML backend and only that backend to the builder, so the set of
active backends is a decision readable in host code. Evidence for
`DocDownExcel-ExcelDocDownBuilderExtensions-RegistersOpenXmlBackend`.

#### AddExcel returns the same builder for chaining

**Test**: `AddExcel_ReturnsSameBuilder`

Proves `AddExcel` returns the builder it was called on, so several registrations read as one configuration
sequence. Evidence for `DocDownExcel-ExcelDocDownBuilderExtensions-ReturnsBuilderForChaining`.

#### AddExcel rejects a null builder

**Test**: `AddExcel_NullBuilder_Throws`

Proves a missing builder is rejected with `ArgumentNullException` at the point of the call, so the error
names the offending call site. Evidence for
`DocDownExcel-ExcelDocDownBuilderExtensions-RejectsNullBuilder`.
