## VisioRenderingDocDownBuilderExtensions

![DemaConsulting.DocDown.Visio.Rendering Structure](DocDownVisioRenderingView.svg)

### Purpose

`VisioRenderingDocDownBuilderExtensions` is the registration seam: the single, explicit call that adds
the page-rendering backend to a `DocDownBuilder`. Its single responsibility is to be the one visible
edge from a host to this package and, transitively, to the rasterization stack it carries.

That edge matters more than its size suggests. DocDown registers backends explicitly rather than by
reflection or assembly scanning, so a host's set of active backends is a property of its own code
rather than of what happens to be on disk. A host that never calls `AddVisioRendering` never pulls the
rasterization stack into its reachable extraction paths.

### Data Model

`VisioRenderingDocDownBuilderExtensions` is a `public static class` with no state. It references no
CanvasNet.Vsdx or CanvasNet type at all, so a host can reference the surface it configures without
the rasterizer's types entering its own compilation.

### Key Methods

- **`static DocDownBuilder AddVisioRendering(this DocDownBuilder builder)`** — registers a factory that
  produces a `VisioPageRenderingExtractor`, and returns the same builder so registration can be chained
  as `AddOffice().AddVisioRendering()` (or `AddVisio().AddVisioRendering()`). Precondition: `builder`
  non-null. Postcondition: exactly one further extractor is registered, and the builder returned is
  the one passed in.

  A factory is registered rather than an instance so construction is deferred to `Build`: a host that
  configures a builder it never builds pays nothing, and each engine receives its own extractor
  instance.

### Error Handling

A null builder is rejected with `ArgumentNullException` at the point of the call, so the error names
the offending call site. No other error condition arises: registration performs no input or output
and constructs nothing.

### Dependencies

- **DemaConsulting.DocDown.Core** — `DocDownBuilder`.
- **VisioPageRenderingExtractor** — the backend this unit registers. See *VisioPageRenderingExtractor
  Design*.

### Callers

A host application, once, when configuring an engine. Nothing inside this package calls it.
