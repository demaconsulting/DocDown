## PdfRenderingDocDownBuilderExtensions

![DocDown.Pdf.Rendering Structure](DocDownPdfRenderingView.svg)

### Purpose

`PdfRenderingDocDownBuilderExtensions` is the registration seam: the single, explicit call that adds
the page-rendering backend to a `DocDownBuilder`. Its single responsibility is to be the one visible
edge from a host to this package and, transitively, to the native rasterization stack it carries.

That edge matters more than its size suggests. DocDown registers backends explicitly rather than by
reflection or assembly scanning, so a host's set of active backends — and whether a native stack is
loaded at all — is a property of its own code rather than of what happens to be on disk. A host that
never calls `AddPdfRendering` never loads a native binary.

### Data Model

`PdfRenderingDocDownBuilderExtensions` is a `public static class` with no state. It references no
PDFtoImage, PDFium, or SkiaSharp type at all, so a host can reference the surface it configures
without the native rasterizer's types entering its own compilation.

### Key Methods

- **`static DocDownBuilder AddPdfRendering(this DocDownBuilder builder)`** — registers a factory that
  produces a `PdfPageRenderingExtractor`, and returns the same builder so registration can be chained
  as `AddPdf().AddPdfRendering()`. Precondition: `builder` non-null. Postcondition: exactly one
  further extractor is registered, and the builder returned is the one passed in.

  A factory is registered rather than an instance so construction is deferred to `Build`: a host that
  configures a builder it never builds pays nothing, and each engine receives its own extractor
  instance.

### Error Handling

A null builder is rejected with `ArgumentNullException` at the point of the call, so the error names
the offending call site. No other error condition arises: registration performs no input or output
and constructs nothing.

### Dependencies

- **DocDown.Core** — `DocDownBuilder`.
- **PdfPageRenderingExtractor** — the backend this unit registers. See *PdfPageRenderingExtractor
  Design*.

### Callers

A host application, once, when configuring an engine; the `docdown` tool registers it as
`AddPdf().AddPdfRendering()`. Nothing inside this package calls it.
