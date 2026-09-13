### DocDownBuilder Verification Design

This document describes the unit-level verification strategy for `DocDownBuilder`, the fluent builder
that collects backend registrations and default options and produces an immutable engine snapshot.

#### Verification Approach

`DocDownBuilder` is verified in isolation through unit tests in `DocDownBuilderTests.cs` under the
`Extraction` folder of `DemaConsulting.DocDown.Core.Tests`, with method names beginning with
`DocDownBuilder_`.

The backend is the one seam a host supplies, so it is stubbed: tests register `StubExtractor` instances
and factories from `DemaConsulting.DocDown.TestSupport` and then inspect the engine the builder produces. The builder,
the engine it builds, and the registry inside that engine are all **real**, because the unit under test
is the builder's own behavior — carrying instances and factories through to the engine, materializing a
factory exactly once, snapshotting configuration at build time, and rejecting duplicate ids and null
arguments. The one scenario that observes a default option end to end runs a real extraction of a small
text input into a per-test `TempScratch` folder; every other scenario is a pure in-memory build with no
filesystem access.

#### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Backends**: `StubExtractor` instances and factories registered per scenario
- **Filesystem**: only the default-options scenario writes, into a per-test `TempScratch` folder
- **Mocking**: the backend seam is stubbed; the builder, engine, and registry are real
- **Isolation**: each test builds its own builder and engine; no shared state

#### Acceptance Criteria

Per IEC 62304 §5.5.2, a `DocDownBuilder` unit test run passes when a registered instance and a
factory-materialized backend both appear on the built engine; when a factory is materialized exactly
once at build time; when configured defaults reach the engine; when duplicate ids and a null instance,
factory, action, or factory result are rejected with the documented exceptions; and when an already-built
engine is unaffected by later builder mutation. Any leaked mutation, repeated materialization, or missing
guard is a failure.

#### Test Scenarios

##### A registered instance appears on the built engine

**Test**: `DocDownBuilder_AddExtractor_Instance_AppearsOnBuiltEngine`

Proves a supplied backend instance is carried through to the built engine. Evidence for
`DocDownCore-Extraction-DocDownBuilder-RegisterInstance`.

##### A factory is materialized once at build

**Test**: `DocDownBuilder_AddExtractor_Factory_IsMaterializedOnceAtBuild`

Proves a registered factory is invoked exactly once, at build time, yielding one stable instance per
engine. Evidence for `DocDownCore-Extraction-DocDownBuilder-RegisterFactory`.

##### Configured defaults reach the engine

**Test**: `DocDownBuilder_ConfigureDefaults_RenderPages_ProducesPageGapByDefault`

Proves default options configured on the builder are applied to the engine, observed here as a
render-pages default that produces a page gap. Evidence for
`DocDownCore-Extraction-DocDownBuilder-ConfigureDefaults`.

##### Duplicate identifiers are rejected

**Test**: `DocDownBuilder_Build_DuplicateIds_ThrowsArgumentException`

Proves a configuration registering two backends with the same identifier is rejected at build time,
surfacing the misconfiguration immediately. Evidence for
`DocDownCore-Extraction-DocDownBuilder-RejectDuplicateIds`.

##### The built snapshot is immutable

**Tests**: `DocDownBuilder_Build_MutatedAfterBuild_DoesNotAffectSnapshot`,
`DocDownBuilder_Build_CalledTwiceWithMoreRegistrations_ProducesIndependentEngines`

Proves an already-built engine keeps behaving identically no matter what the builder does next, and that
building twice yields independent engines. Evidence for
`DocDownCore-Extraction-DocDownBuilder-ImmutableSnapshot`.

##### Null arguments are rejected

**Tests**: `DocDownBuilder_AddExtractor_NullInstance_ThrowsArgumentNullException`,
`DocDownBuilder_AddExtractor_NullFactory_ThrowsArgumentNullException`,
`DocDownBuilder_ConfigureDefaults_NullAction_ThrowsArgumentNullException`,
`DocDownBuilder_Build_FactoryReturnsNull_ThrowsArgumentException`

Proves a null instance, factory, configuration action, or factory result is rejected at the point of the
mistake with the documented exception. Evidence for
`DocDownCore-Extraction-DocDownBuilder-RejectNullArguments`.
