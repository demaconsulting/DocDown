### DocDownBuilder Verification Design

This document describes the unit-level verification strategy for `DocDownBuilder`.

#### Verification Approach

`DocDownBuilder` is verified through unit tests in `DocDownBuilderTests.cs`. Registered backends are
stubbed through `StubExtractor`, while the builder, registry, and built engine remain real. One test
runs a real extraction so a configured default option is observed through the engine rather than only
inspected in memory.

#### Test Environment

- **Framework**: xUnit v3 under the .NET SDK
- **Targets**: net8.0, net9.0, and net10.0
- **Backends**: `StubExtractor` instances and factories
- **Filesystem**: only the default-options scenario writes, using `TempScratch`

#### Acceptance Criteria

Per IEC 62304 §5.5.2, `DocDownBuilder` passes when registered instances and factories appear on the
built engine, factories are materialized once at build time, configured defaults reach the engine,
duplicate identifiers are rejected, later builder mutation does not affect an existing engine, and
null registration or configuration inputs fail with the documented exceptions.

#### Test Scenarios

##### A registered instance appears on the built engine

**Test**: `DocDownBuilder_AddExtractor_Instance_AppearsOnBuiltEngine`

##### A registered factory is materialized once

**Test**: `DocDownBuilder_AddExtractor_Factory_IsMaterializedOnceAtBuild`

##### Configured defaults reach the engine

**Test**: `DocDownBuilder_ConfigureDefaults_RenderPages_RecordsRenderNoteByDefault`

Proves a default render-pages option reaches the engine and causes the expected note-producing path.

##### Duplicate identifiers are rejected

**Test**: `DocDownBuilder_Build_DuplicateIds_ThrowsArgumentException`

##### The built snapshot is unaffected by later builder mutation

**Tests**: `DocDownBuilder_Build_MutatedAfterBuild_DoesNotAffectSnapshot`,
`DocDownBuilder_Build_CalledTwiceWithMoreRegistrations_ProducesIndependentEngines`

##### Null arguments are rejected

**Tests**: `DocDownBuilder_AddExtractor_NullInstance_ThrowsArgumentNullException`,
`DocDownBuilder_AddExtractor_NullFactory_ThrowsArgumentNullException`,
`DocDownBuilder_ConfigureDefaults_NullAction_ThrowsArgumentNullException`,
`DocDownBuilder_Build_FactoryReturnsNull_ThrowsArgumentException`
