using DemaConsulting.DocDown.Core;
using DemaConsulting.DocDown.Excel;
using DemaConsulting.DocDown.PowerPoint;
using DemaConsulting.DocDown.Visio;
using DemaConsulting.DocDown.Word;

namespace DemaConsulting.DocDown.Office;

/// <summary>
///     The registration seam that adds every Microsoft Office backend to a <see cref="DocDownBuilder"/>.
/// </summary>
/// <remarks>
///     <para>
///         DocDown registers backends explicitly rather than by reflection or assembly scanning, so a
///         host's dependency graph is exactly what its code says it is. This class is the one call
///         that registers everything this package ships: Word, Excel, PowerPoint, and Visio.
///     </para>
///     <para>
///         The per-format methods remain available for a host that wants only some of them — a
///         service that only ever sees spreadsheets can call <c>AddExcel</c> alone and register one
///         backend rather than four.
///     </para>
///     <para>
///         Deliberately free of any Open XML SDK type, so a host can reference the registration
///         surface without the SDK's types entering its compilation. All members are static and
///         thread-safe; the builder they mutate is not.
///     </para>
/// </remarks>
public static class OfficeDocDownBuilderExtensions
{
    /// <summary>
    ///     Registers every Office backend with the builder.
    /// </summary>
    /// <param name="builder">The builder to register with. Must not be null.</param>
    /// <returns>The same <paramref name="builder"/>, so registration can be chained.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <remarks>
    ///     Equivalent to calling <c>AddWord</c>, <c>AddExcel</c>, <c>AddPowerPoint</c>, and
    ///     <c>AddVisio</c> in turn. Word, Excel, PowerPoint, and Visio each register one managed
    ///     Open XML backend. Side effect: mutates <paramref name="builder"/>'s registration list.
    /// </remarks>
    /// <example>
    ///     <code language="csharp">
    ///     using System;
    ///     using DemaConsulting.DocDown.Core;
    ///     using DemaConsulting.DocDown.Office;
    ///
    ///     var engine = new DocDownBuilder()
    ///         .AddOffice() // .docx, .xlsx, .pptx, .vsdx, .vsdm
    ///         .Build();
    ///
    ///     Console.WriteLine(engine.Extractors.Count); // 4 — one managed reader per format
    ///     </code>
    /// </example>
    public static DocDownBuilder AddOffice(this DocDownBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder
            .AddWord()
            .AddExcel()
            .AddPowerPoint()
            .AddVisio();
    }
}
