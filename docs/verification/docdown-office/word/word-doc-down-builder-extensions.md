## WordDocDownBuilderExtensions Verification Design

This document describes the unit-level verification strategy for `WordDocDownBuilderExtensions`, the
registration seam that adds the Word backend to a builder.

### Verification Approach

`WordDocDownBuilderExtensions` is verified through unit tests in
`WordDocDownBuilderExtensionsTests.cs` in `DemaConsulting.DocDown.Office.Tests`, exercising the one
extension method the package offers: `AddWord`.

Nothing is mocked: a real builder is used and a real engine is built from it, because the observable
behavior under test is exactly what a host observes. The registration's effect is asserted through
the engine's own descriptor list rather than through any internal state, so the test verifies the
promise made to a host rather than an implementation detail. The registration is asserted both
positively and exhaustively — the Open XML backend is present, and it is the only extractor — because
this package ships one backend and a second registered extractor would mean a backend re-entered the
package without anyone deciding it should.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: none; no document is extracted and no file is read
- **Filesystem**: none
- **Mocking**: none; a real builder and real engines are used
- **Isolation**: each test constructs its own builder

### Acceptance Criteria

Per IEC 62304 §5.5.2, a `WordDocDownBuilderExtensions` unit test run passes when `AddWord`
registers the Open XML backend on the resulting engine and registers nothing else; when the call
returns the same builder instance it was called on; and when a missing builder is rejected at the
call. A second registered extractor, a missing backend, a different builder instance returned, or a
deferred null check is a failure.

### Test Scenarios

#### The AddWord call registers the Open XML backend

**Test**: `AddWord_OnBuilder_RegistersOpenXmlBackend`

Proves an engine built from a builder with a single `AddWord` call carries a backend identified as
`word-openxml`, and exactly one extractor in total, so a host asking for Word support gets the
deterministic managed reader and nothing it did not ask for. Evidence for
`DocDownWord-WordDocDownBuilderExtensions-RegistersOpenXmlBackend`.

#### The builder is returned for chaining

**Test**: `AddWord_ReturnsSameBuilderForChaining`

Proves `AddWord` returns the same builder instance it was called on, so a host registering several
backends can express that as one readable configuration sequence.
Evidence for `DocDownWord-WordDocDownBuilderExtensions-ReturnsBuilderForChaining`.

#### A missing builder is rejected at the call

**Test**: `AddWord_NullBuilder_Throws`

Proves the failure happens where the mistake was made, naming the offending call site rather than
surfacing later against configuration the host wrote correctly. Evidence for
`DocDownWord-WordDocDownBuilderExtensions-RejectsNullBuilder`.
