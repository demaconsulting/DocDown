using DocDown.Core;
using DocDown.Word.OpenXml;

namespace DocDown.Word;

/// <summary>
///     The registration seam that adds Word extraction to a <see cref="DocDownBuilder"/>.
/// </summary>
/// <remarks>
///     <para>
///         DocDown registers backends explicitly rather than by reflection or assembly scanning, so a
///         host's dependency graph is exactly what its code says it is. This class is that explicit
///         seam for the Word package: <see cref="AddWord"/> registers the managed Open XML backend,
///         which is the only Word backend this package ships.
///     </para>
///     <para>
///         Deliberately free of any Open XML SDK type, so a host can reference the registration
///         surface without the SDK's types entering its compilation. All members are static and
///         thread-safe; the builder they mutate is not.
///     </para>
/// </remarks>
public static class WordDocDownBuilderExtensions
{
    /// <summary>
    ///     Registers the managed Open XML Word backend with the builder.
    /// </summary>
    /// <param name="builder">The builder to register with. Must not be null.</param>
    /// <returns>The same <paramref name="builder"/>, so registration can be chained.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <remarks>
    ///     Registers a factory rather than an instance so construction is deferred to
    ///     <see cref="DocDownBuilder.Build"/>. The Open XML backend serves every <c>.docx</c>; the
    ///     legacy binary <c>.doc</c> format is not supported by DocDown at all, so one call yields
    ///     one backend and no environment-dependent second answer. Side effect: mutates
    ///     <paramref name="builder"/>'s registration list.
    /// </remarks>
    /// <example>
    ///     <code language="csharp">
    ///     using System;
    ///     using DocDown.Core;
    ///     using DocDown.Word;
    ///
    ///     var engine = new DocDownBuilder()
    ///         .AddWord() // .docx - text, tables, images; no page images
    ///         .Build();
    ///
    ///     Console.WriteLine(engine.Extractors.Count); // 1 — the managed Open XML Word backend
    ///     </code>
    /// </example>
    public static DocDownBuilder AddWord(this DocDownBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddExtractor(static () => new WordOpenXmlExtractor());
    }
}
