# DocDown.Office Verification Design

This document describes the system-level verification strategy for `DocDown.Office`, the Microsoft
Office extraction package.

## Verification Approach

`DocDown.Office` is verified through the per-format system tests described in the Word, Excel,
PowerPoint, and Visio subsystem verification documents, plus the system-level tests in
`DemaConsulting.DocDown.Office.Tests`, running on xUnit v3 across net8.0, net9.0, and net10.0.

Two properties belong to the system rather than to any one format, and are verified here.

### Registration is asserted as an exact set

`AddOffice()` stands in for four separate calls, so it is only safe if it registers what those four
registered. The test asserts the **whole set** of extractor identifiers, not that particular ones are
present. That direction matters: an exact set fails when a backend is silently dropped from the chain
as readily as when one is silently added, and a merge of four packages into one is exactly the change
that could drop one unnoticed.

The surviving per-format calls are verified too. `AddExcel()` alone must register one backend, not
six — that granularity is the stated reason those methods were kept, and an untested reason is a weak
one.

### The probe documents are asserted against the shipped assembly

Each backend's self-test reads a document embedded in the package. If a probe were dropped from the
package, every self-test for that format would fail at once and report the environment as broken when
only the packaging was. The tests load each probe from the assembly's own manifest and check it is
non-empty and begins with the Zip local file header that every Open XML package starts with.

This is a packaging assertion, not a content one: it answers whether the file shipped, not what is
inside it. What is inside it is answered by the self-tests that extract it.

## Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: the probe documents embedded in the package, authored in Microsoft Word, Excel,
  PowerPoint, and Visio and scrubbed of author metadata
- **Mocking**: none at this level; the registration tests build the real engine
- **Office automation**: not required. The COM backends register regardless of whether Microsoft
  Office is present; whether they are *selected* is decided by their availability probe, which is
  verified in the Com subsystem.

## Acceptance Criteria

- `AddOffice()` registers exactly the six backends the package ships.
- `AddOffice()` returns the same builder, so registration chains.
- A null builder is rejected rather than silently ignored.
- `AddExcel()` alone registers exactly one backend.
- All four probe documents load from the shipped assembly and are Open XML packages.

## Test Scenarios

### One call registers every Office backend

**Test**: `AddOffice_OnBuilder_RegistersEveryOfficeBackend`

Builds an engine through `AddOffice()` and asserts the ordered set of extractor identifiers is
exactly `excel-openxml`, `powerpoint-com`, `powerpoint-openxml`, `visio-com`, `visio-openxml`, and
`word-openxml`. Evidence for `DocDownOffice-Registration`.

### Registration chains and rejects a null builder

**Tests**: `AddOffice_ReturnsSameBuilderForChaining`, `AddOffice_NullBuilder_Throws`

Proves the method returns its argument so calls compose, and that a null builder raises
`ArgumentNullException` rather than being ignored. Evidence for `DocDownOffice-Registration`.

### A single format registers a single backend

**Test**: `AddExcel_Alone_RegistersOnlyTheExcelBackend`

Proves the per-format calls still register one format's backends on their own, so a host that reads
only spreadsheets carries one backend rather than six. Evidence for `DocDownOffice-Registration`.

### The package carries its probe documents

**Test**: `Probe_EmbeddedResource_LoadsAsAnOpenPackage`

Loads each of the four embedded probes from the shipped assembly and asserts it is non-empty and
begins with the Zip signature. Evidence for `DocDownOffice-SelfTestProbes`.
