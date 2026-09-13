## PdfDocDownBuilderExtensions

![DocDown.Pdf Structure](DocDownPdfView.svg)

### Purpose

`PdfDocDownBuilderExtensions` is the registration seam: the single explicit call that adds the
managed PDF backend to a `DocDownBuilder`. Its single responsibility is to make a host's decision to
use `DocDown.Pdf` visible in the host's own code.

### Data Model

`PdfDocDownBuilderExtensions` is a `public static class` with no state. It exposes no PdfPig type,
so a host can reference the registration surface without taking a PDF-parser type into its own
public surface.

### Key Methods

- **`static DocDownBuilder AddPdf(this DocDownBuilder builder)`** - registers a factory that
  constructs `PdfDocumentExtractor` and returns the same builder instance so registration can be
  chained with other backends. Precondition: `builder` non-null. Postcondition: one additional PDF
  extractor factory is registered.

The method registers a factory rather than an instance so construction is deferred to `Build`. A
builder that is configured but never built pays no extractor-construction cost, and each built
document engine receives its own extractor instance.

### Error Handling

A null builder is rejected with `ArgumentNullException` at the call site. No other error condition
arises: registration performs no I/O, opens no document, and resolves no reflection-based type.

### Dependencies

- **DocDown.Core** - `DocDownBuilder`.
- **PdfDocumentExtractor** - the backend this unit registers. See *PdfDocumentExtractor Design*.

### Callers

A host application calls `AddPdf` while configuring a DocDown engine. Nothing inside this package
calls it.
