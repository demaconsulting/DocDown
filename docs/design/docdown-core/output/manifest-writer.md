### ManifestWriter

![Output Structure](OutputView.svg)

#### Purpose

`ManifestWriter` serializes `manifest.json`, the machine-readable record of one extraction. Its
single responsibility is to project the in-memory extraction report and sink state into the stable
schema that downstream tools consume.

#### Data Model

`ManifestWriter` is a `static class` with no mutable state. It builds the immutable
`ExtractionManifest` DTO graph and serializes it through the source-generated `DocDownJsonContext`.
Every domain enum is projected to a fixed camelCase string before it reaches the DTO graph, so the
serialized object uses only strings, numbers, booleans, nulls, and lists.

#### On-disk format

`manifest.json` is emitted with schema version `2.0`, `
` line endings, and UTF-8 without a byte
order mark. The top-level shape contains:

- `schemaVersion`
- `tool`
- `scratchFolder`
- `extractedAtUtc`
- `status` (`produced` or `unreadable`)
- `source`
- `extractor`
- `environment`
- `document`
- `contentFeatures`
- `images`
- `pages`
- `parts`
- `notes`
- `requestedOptions`
- `failure`

The file contains only the schema 2.0 top-level fields listed above. The selected extractor shape is
limited to identifier, display name, owning package, and priority. Notes are emitted as plain
strings in emission order.

#### Key Methods

- **`WriteAsync(ScratchFolder folder, ExtractionSink sink, ExtractionReport report,
  ContentWriteResult? content, CancellationToken cancellationToken)`** — builds the DTO graph and
  writes the serialized JSON.
- **`BuildManifest`** — maps the report and sink state to the immutable DTO graph.
- **`BuildSource`**, **`BuildExtractor`**, **`BuildEnvironment`**, **`BuildDocument`**,
  **`BuildContentFeatures`**, **`BuildImages`**, **`BuildPages`**, **`BuildParts`**,
  **`BuildNotes`**, **`BuildOptions`**, and **`BuildFailure`** — focused mapping helpers.
- **`OutcomeString`**, **`BasisString`**, **`ImageOutputString`**, **`TransformString`**,
  **`SplitString`**, and **`ScratchModeString`** — projection helpers for stable schema strings.

#### Error Handling

`WriteAsync` throws `ArgumentNullException` for null required collaborators. Serialization performs no
fallback inference: unknown enum values fail fast through the projection helpers rather than reaching
a consumer as undocumented output.

#### Dependencies

- **`ScratchFolder`** — deterministic contained text write.
- **`ExtractionManifest`** and **`DocDownJsonContext`** — manifest DTO graph and source-generated
  serializer context.
- **`ExtractionSink`** and **`ExtractionReport`** — recorded extraction state.
- `System.Text.Json` — in-box serializer.

#### Callers

`DocDownEngine` calls `ManifestWriter` after `MetadataWriter` and before `SummaryWriter`.
