## PowerPointDocDownBuilderExtensions

![DocDown.PowerPoint Structure](DocDownPowerPointView.svg)

### Purpose

`PowerPointDocDownBuilderExtensions` is the one visible edge from a host to this package: the explicit,
reflection-free call that adds the PowerPoint backends to a `DocDownBuilder`. Its single responsibility is
registration — it constructs no extractor eagerly and opens no file. DocDown registers backends
explicitly rather than by reflection or assembly scanning, so a host's dependency graph is exactly what
its code says it is, and this seam is where a host decides to include PowerPoint support.

### Data Model

`PowerPointDocDownBuilderExtensions` is a `public static class`. It holds no state; every member is static
and thread-safe, though the `DocDownBuilder` it mutates is not.

### Key Methods

- **`DocDownBuilder AddPowerPoint(this DocDownBuilder builder)`** — registers two deferred factories on
  the builder: one for `PowerPointOpenXmlExtractor` (the managed content backend, priority 10) and one for
  `PowerPointComExtractor` (the COM rendering backend, priority 0). Registering factories rather than
  instances defers construction to `DocDownBuilder.Build`, so nothing is constructed and no environment is
  probed at registration. Returns the same builder so registration can be chained. Precondition: `builder`
  is non-null. Side effect: mutates the builder's registration list.

### Error Handling

A null builder is rejected with `ArgumentNullException` at the point of the call, through
`ArgumentNullException.ThrowIfNull`, so the failure names the offending call site rather than surfacing
later at build time against configuration the host wrote correctly.

### Dependencies

- **DocDown.Core** — `DocDownBuilder` and its `AddExtractor` registration method.
- **PowerPointOpenXmlExtractor** — the managed backend the first factory produces.
- **PowerPointComExtractor** — the COM backend the second factory produces.

The method body names both extractor types, but only inside the two static factory lambdas, so the SDK and
COM types they pull in are not part of this unit's public surface.

### Callers

A host calls `AddPowerPoint` when configuring a DocDown engine, alongside `AddCore` and any other format
packages it wants. Nothing else in the package calls it.
