using System.Text.Json.Serialization;

namespace DocDown.Core;

/// <summary>
///     The source-generated <see cref="JsonSerializerContext"/> for the manifest DTO graph.
/// </summary>
/// <remarks>
///     <para>
///         Source-generated serialization metadata is used instead of runtime reflection so
///         <c>manifest.json</c> can be produced in a trimmed, AOT-compiled, or single-file
///         published application without losing type metadata. Registering only
///         <see cref="ExtractionManifest"/> is sufficient because the generator walks the entire
///         reachable graph of nested records from that root.
///     </para>
///     <para>
///         The options mirror the manifest contract: camelCase property names, indented output
///         for human readability, and no ignore condition so absent optional values serialize as
///         explicit <c>null</c> rather than being dropped — which keeps the shape stable for
///         consumers.
///     </para>
///     <para>
///         Declared <see langword="internal"/> because only Core's <c>ManifestWriter</c> and
///         <c>ScratchFolder</c> (same assembly) serialize and deserialize the manifest; the DTOs
///         themselves are public for inspection, but the serialization context is an internal
///         implementation detail. Generated contexts are thread-safe for concurrent use.
///     </para>
/// </remarks>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never)]
[JsonSerializable(typeof(ExtractionManifest))]
internal sealed partial class DocDownJsonContext : JsonSerializerContext;
