## PdfDocDownBuilderExtensions

![DocDown.Pdf Structure](DocDownPdfView.svg)

### Purpose

`PdfDocDownBuilderExtensions` is the registration seam: the single, explicit call that adds the PDF
backend to a `DocDownBuilder`. Its single responsibility is to be the one visible edge from a host to
this package.

That edge matters more than its size suggests. DocDown registers backends explicitly rather than by
reflection or assembly scanning, so a host's set of active backends is a property of its own code
rather than of what happens to be on disk at deployment time. This unit is where that property is
either upheld or lost.

### Data Model

`PdfDocDownBuilderExtensions` is a `public static class` with no state. It references no PDF-parser
type at all, so a host can reference the surface it configures without the parser's types entering
its own compilation.

### Key Methods

- **`static DocDownBuilder AddPdf(this DocDownBuilder builder)`** — registers a factory that produces
  a `PdfDocumentExtractor`, and returns the same builder so registration can be chained with other
  backends. Precondition: `builder` non-null. Postcondition: exactly one further extractor is
  registered, and the builder returned is the one passed in.

  A factory is registered rather than an instance so construction is deferred to `Build`: a host that
  configures a builder it never builds pays nothing, and each engine built from the builder receives
  its own extractor instance.

### Error Handling

A null builder is rejected with `ArgumentNullException` at the point of the call, so the error names
the offending call site. Deferring the failure to build time would report it against configuration
the host wrote correctly. No other error condition arises: registration performs no input or output
and constructs nothing.

### Dependencies

- **DocDown.Core** — `DocDownBuilder`.
- **PdfDocumentExtractor** — the backend this unit registers. See *PdfDocumentExtractor Design*.

### Callers

A host application, once, when configuring an engine. Nothing inside this package calls it.
