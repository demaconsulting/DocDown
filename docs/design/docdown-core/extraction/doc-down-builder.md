### DocDownBuilder

![Extraction Structure](ExtractionView.svg)

#### Purpose

`DocDownBuilder` is the fluent configuration stage that collects extractor registrations and default
options and produces a configured `DocDownEngine`. Its single responsibility is assembly-time
configuration: the builder is mutable, but the engine it produces is a snapshot that later builder
mutation cannot affect.

#### Data Model

`DocDownBuilder` is a `sealed class` with two private fields.

- **`_factories`** (`List<Func<IDocumentExtractor>>`) — The registered extractor factories in
  registration order. Instance registrations are wrapped as factories so both forms share one
  materialization path.
- **`_defaults`** (`ExtractionOptions`) — The mutable default options configured through
  `ConfigureDefaults`; cloned at `Build`.

Registration order is preserved because it governs self-test aggregation order and reporting order.
The builder is not thread-safe and is intended to be configured from a single thread; the engine it
builds is safe for concurrent use.

#### Key Methods

- **`DocDownBuilder AddExtractor(IDocumentExtractor extractor)`** — registers a ready instance.
  Precondition: `extractor` non-null. Wraps the instance in a factory so it flows through the same
  materialization and duplicate-detection path as a factory registration. Returns `this` for chaining.
- **`DocDownBuilder AddExtractor(Func<IDocumentExtractor> factory)`** — registers a factory whose
  instance is created at `Build`. Precondition: `factory` non-null. Defers any expensive backend setup
  until an engine is actually assembled. Returns `this`.
- **`DocDownBuilder ConfigureDefaults(Action<ExtractionOptions> configure)`** — applies a configuration
  action to the builder's own defaults. Precondition: `configure` non-null. Multiple calls compose in
  call order. Returns `this`.
- **`DocDownEngine Build()`** — snapshots the registration list into a fresh `List`, materializes it
  through a new `ExtractorRegistry` (where duplicate identifiers are caught), and clones the configured
  defaults into the engine.
  - *Guarantees*: the returned engine is bound to an immutable registration snapshot and a private
    copy of the defaults; the builder remains usable and can produce further, independent engines.

#### Error Handling

`AddExtractor` and `ConfigureDefaults` throw `ArgumentNullException` for null arguments at the point of
registration, so the error names the offending call. `Build` propagates the `ArgumentException` that
`ExtractorRegistry`'s constructor raises for a duplicate identifier, a factory that returns null, or an
extractor with a null or empty identifier — identifier uniqueness is a hard build-time invariant
because the identifier is both the caller-override key and the manifest key. No exception is swallowed;
configuration faults surface deterministically at `Build`.

#### Dependencies

- **ExtractorRegistry** (same subsystem) — constructed from the factory snapshot at `Build`. See
  *ExtractorRegistry Design*.
- **DocDownEngine** (same subsystem) — the product of `Build`. See *DocDownEngine Design*.
- **ExtractionOptions** (supporting type; see *Extraction Subsystem Design*) — the configured and
  cloned defaults.

#### Callers

`DocDownBuilder` is the public entry point a consuming application uses to assemble an engine. It is
not called by any other unit within the subsystem.
