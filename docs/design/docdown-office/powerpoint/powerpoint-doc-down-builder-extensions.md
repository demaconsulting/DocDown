## PowerPointDocDownBuilderExtensions

![DocDown.PowerPoint Structure](PowerPointView.svg)

### Purpose

`PowerPointDocDownBuilderExtensions` is the one visible edge from a host to this package: the explicit,
reflection-free call that adds the PowerPoint backend to a `DocDownBuilder`. Its single responsibility is
registration — it constructs no extractor eagerly and opens no file. DocDown registers backends
explicitly rather than by reflection or assembly scanning, so a host's dependency graph is exactly what
its code says it is, and this seam is where a host decides to include PowerPoint support.

### Data Model

`PowerPointDocDownBuilderExtensions` is a `public static class`. It holds no state; every member is static
and thread-safe, though the `DocDownBuilder` it mutates is not.

### Key Methods

- **`DocDownBuilder AddPowerPoint(this DocDownBuilder builder)`** — registers one deferred factory on
  the builder, for `PowerPointOpenXmlExtractor` (the managed content backend, priority 10). Registering a
  factory rather than an instance defers construction to `DocDownBuilder.Build`, so nothing is constructed
  and no environment is probed at registration. Returns the same builder so registration can be chained.
  Precondition: `builder` is non-null. Side effect: mutates the builder's registration list.

### Error Handling

A null builder is rejected with `ArgumentNullException` at the point of the call, through
`ArgumentNullException.ThrowIfNull`, so the failure names the offending call site rather than surfacing
later at build time against configuration the host wrote correctly.

### Dependencies

- **DocDown.Core** — `DocDownBuilder` and its `AddExtractor` registration method.
- **PowerPointOpenXmlExtractor** — the managed backend the factory produces.

The method body names the extractor type, but only inside the static factory lambda, so the SDK types it
pulls in are not part of this unit's public surface.

### Callers

A host calls `AddPowerPoint` when configuring a DocDown engine, alongside `AddCore` and any other format
packages it wants. Nothing else in the package calls it.
