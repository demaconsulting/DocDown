## WordDocDownBuilderExtensions

![DocDown.Word Structure](DocDownWordView.svg)

### Purpose

`WordDocDownBuilderExtensions` is the registration seam: the one explicit call that adds the Word
backend to a `DocDownBuilder`. Its single responsibility is to be the one visible edge from a host
to this package.

That edge matters more than its size suggests. DocDown registers backends explicitly rather than by
reflection or assembly scanning, so a host's set of active backends is a property of its own code
rather than of what happens to be on disk at deployment time. This unit is where that property is
either upheld or lost.

### Data Model

`WordDocDownBuilderExtensions` is a `public static class` with no state. It carries no Open XML SDK
type on its public surface — the `using DocDown.Word.OpenXml;` directive references only this
package's own extractor type — so a host can reference the surface it configures without the SDK's
types entering its own compilation. Its member is thread-safe; the builder it mutates is not.

### Key Methods

- **`static DocDownBuilder AddWord(this DocDownBuilder builder)`** — registers a factory that
  produces a `WordOpenXmlExtractor`, and returns the same builder so registration can be chained.
  Precondition: `builder` non-null. Postcondition: exactly one further extractor is registered, and
  the builder returned is the one passed in.

  There is deliberately no second registration method. The package ships one backend, and a call
  that registered a subset of one backend would be an affordance with nothing behind it.

The method registers a factory rather than an instance, so construction is deferred to
`DocDownBuilder.Build`: a host that configures a builder it never builds pays nothing, and each
engine built from the builder receives its own extractor instance.

### Error Handling

A null builder is rejected with `ArgumentNullException` at the point of the call, so the error
names the offending call site. Deferring the failure to build time would report it against
configuration the host wrote correctly. No other error condition arises: registration performs no
input or output and constructs nothing.

### Dependencies

- **DocDown.Core** — `DocDownBuilder`.
- **WordOpenXmlExtractor** — the backend registered. See *WordOpenXmlExtractor Design*.

### Callers

A host application, once, when configuring an engine. `DocDown.Tool` calls `AddWord` alongside its
other extractor registrations. Nothing inside this package calls it.
