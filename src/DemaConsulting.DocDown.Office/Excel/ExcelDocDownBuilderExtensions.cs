using DocDown.Core;
using DocDown.Excel.OpenXml;

namespace DocDown.Excel;

/// <summary>
///     The registration seam that adds Excel extraction to a <see cref="DocDownBuilder"/>.
/// </summary>
/// <remarks>
///     <para>
///         DocDown registers backends explicitly rather than by reflection or assembly scanning, so a
///         host's dependency graph is exactly what its code says it is. This class is that explicit
///         seam for the Excel package: <see cref="AddExcel"/> registers the managed Open XML backend,
///         which is the only Excel backend this package ships — the workbook intent deliberately
///         offers no rendering and therefore no second, environment-dependent backend.
///     </para>
///     <para>
///         Deliberately free of any Open XML SDK type, so a host can reference the registration
///         surface without the SDK's types entering its compilation. All members are static and
///         thread-safe; the builder they mutate is not.
///     </para>
/// </remarks>
public static class ExcelDocDownBuilderExtensions
{
    /// <summary>
    ///     Registers the managed Open XML Excel backend with the builder.
    /// </summary>
    /// <param name="builder">The builder to register with. Must not be null.</param>
    /// <returns>The same <paramref name="builder"/>, so registration can be chained.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <remarks>
    ///     Registers a factory rather than an instance so construction is deferred to
    ///     <see cref="DocDownBuilder.Build"/>. The Open XML backend serves every <c>.xlsx</c>; the
    ///     legacy binary <c>.xls</c> format is not supported by DocDown at all, so one call yields one
    ///     backend. Side effect: mutates <paramref name="builder"/>'s registration list.
    /// </remarks>
    /// <example>
    ///     <code language="csharp">
    ///     using System;
    ///     using DocDown.Core;
    ///     using DocDown.Excel;
    ///
    ///     var engine = new DocDownBuilder()
    ///         .AddExcel() // .xlsx - cells, formulas, charts; workbooks are never rendered
    ///         .Build();
    ///
    ///     Console.WriteLine(engine.Extractors.Count); // 1 — the managed Open XML Excel backend
    ///     </code>
    /// </example>
    public static DocDownBuilder AddExcel(this DocDownBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddExtractor(static () => new ExcelOpenXmlExtractor());
    }
}
