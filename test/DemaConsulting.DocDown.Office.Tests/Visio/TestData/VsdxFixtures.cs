
namespace DemaConsulting.DocDown.Office.Tests.Visio.TestData;

/// <summary>
///     Builds every drawing the test suite needs, at test time, using <see cref="VisioPackageBuilder"/>, so the reader can be driven through
///     shapes, masters, multiple pages and malformed parts that one committed fixture cannot cover.
/// </summary>
/// <remarks>
///     No binary <c>.vsdx</c> is committed to this repository. Each fixture is synthesized here when
///     the test that needs it runs, which keeps the repository text-only. All members are static and
///     pure apart from their allocations.
/// </remarks>
public static class VsdxFixtures
{
    /// <summary>
    ///     Builds arbitrary bytes for a legacy <c>.vsd</c> fixture whose content is never read.
    /// </summary>
    /// <returns>Placeholder bytes.</returns>
    /// <remarks>Used where selection fails before any extractor opens the drawing.</remarks>
    public static byte[] LegacyVsdBytes() => "This is a placeholder for a legacy binary Visio drawing."u8.ToArray();

    /// <summary>
    ///     Builds a one-page drawing modeling a small wash system: three labeled shapes and two
    ///     directed connections forming the chain <c>Inlet Tank → Transfer Pump → Outlet Valve</c>.
    /// </summary>
    /// <returns>The drawing bytes.</returns>
    public static byte[] WashSystem() => VisioPackageBuilder.Build(
    [
        new VisioBuildPage(
            "Wash System",
            [
                new VisioBuildShape("1", "Inlet Tank"),
                new VisioBuildShape("2", "Transfer Pump"),
                new VisioBuildShape("3", "Outlet Valve")
            ],
            [("1", "2"), ("2", "3")])
    ]);

    /// <summary>
    ///     Builds a one-page drawing whose shape text mixes a Wingdings arrow run with ordinary text,
    ///     and a second shape carrying the same character as ordinary text in an unstyled run.
    /// </summary>
    /// <returns>The drawing bytes.</returns>
    /// <remarks>
    ///     Visio stores a Wingdings arrow as the byte <c>0xE0</c>, which decodes to the Latin letter
    ///     <c>à</c>; only the run's font distinguishes the two. The second shape stands in for the
    ///     French text a blind replacement would corrupt, so pairing both in one drawing pins that
    ///     the recovery is gated on font evidence.
    /// </remarks>
    public static byte[] SymbolFontArrows() => VisioPackageBuilder.Build(
    [
        new VisioBuildPage(
            "Valves",
            [
                new VisioBuildShape("1", null, null,
                [
                    new VisioBuildTextRun("IN "),
                    new VisioBuildTextRun("\u00e0", "Wingdings"),
                    new VisioBuildTextRun(" OUT")
                ]),
                new VisioBuildShape("2", null, null, [new VisioBuildTextRun("Valve \u00e0 5 bar")])
            ],
            [])
    ]);

    /// <summary>
    ///     Builds a two-page drawing, to prove page names are surfaced and pages are ordered.
    /// </summary>
    /// <returns>The drawing bytes.</returns>
    public static byte[] TwoPages() => VisioPackageBuilder.Build(
    [
        new VisioBuildPage(
            "Schematic",            [new VisioBuildShape("1", "Inlet"), new VisioBuildShape("2", "Outlet")],
            [("1", "2")]),
        new VisioBuildPage(
            "Legend",
            [new VisioBuildShape("1", "Notes")],
            [])
    ]);

    /// <summary>
    ///     Builds a one-page drawing with a page that carries no shapes and no connections.
    /// </summary>
    /// <returns>The drawing bytes.</returns>
    public static byte[] EmptyPage() => VisioPackageBuilder.Build(
    [
        new VisioBuildPage("Blank", [], [])
    ]);

    /// <summary>
    ///     Builds a one-page drawing whose connectors write their <c>EndX</c> <c>&lt;Connect&gt;</c>
    ///     record before their <c>BeginX</c> record, reproducing the End-before-Begin document
    ///     ordering a real drawing can carry.
    /// </summary>
    /// <returns>The drawing bytes.</returns>
    /// <remarks>
    ///     This is the root-cause regression for Defect 1: a connector whose End record precedes its
    ///     Begin record must still yield exactly one edge, never a duplicate. Two connectors →
    ///     exactly two edges.
    /// </remarks>
    public static byte[] EndRecordBeforeBegin() => VisioPackageBuilder.Build(
    [
        new VisioBuildPage(
            "End First",
            [
                new VisioBuildShape("1", "Inlet Tank"),
                new VisioBuildShape("2", "Transfer Pump"),
                new VisioBuildShape("3", "Outlet Valve")
            ],
            [("1", "2"), ("2", "3")],
            EndRecordFirst: true)
    ]);

    /// <summary>
    ///     Builds a one-page drawing with two distinct connector sheets joining the same ordered pair
    ///     of shapes, to pin that genuine parallel connectors each contribute their own edge.
    /// </summary>
    /// <returns>The drawing bytes.</returns>
    /// <remarks>
    ///     De-duplication is per connector sheet, not per (from, to) pair: two real connectors
    ///     between the same shapes are two real edges. This distinguishes the correct fix from an
    ///     over-eager pair-level de-duplication.
    /// </remarks>
    public static byte[] ParallelConnectors() => VisioPackageBuilder.Build(
    [
        new VisioBuildPage(
            "Parallel",
            [new VisioBuildShape("1", "Inlet"), new VisioBuildShape("2", "Outlet")],
            [("1", "2"), ("1", "2")])
    ]);

    /// <summary>
    ///     Builds a two-page drawing whose pages reuse the same shape ids but wire them with
    ///     different connectors, to pin that each page reports only its own edges.
    /// </summary>
    /// <returns>The drawing bytes.</returns>
    /// <remarks>
    ///     Because each page's connectors are resolved against its own part, reusing shape ids across
    ///     pages must not leak a sibling page's edges into another page's topology.
    /// </remarks>
    public static byte[] PagesReusingShapeIds() => VisioPackageBuilder.Build(
    [
        new VisioBuildPage(
            "First",
            [
                new VisioBuildShape("1", "A1"),
                new VisioBuildShape("2", "A2"),
                new VisioBuildShape("3", "A3")
            ],
            [("1", "2")]),
        new VisioBuildPage(
            "Second",
            [
                new VisioBuildShape("1", "B1"),
                new VisioBuildShape("2", "B2"),
                new VisioBuildShape("3", "B3")
            ],
            [("2", "3"), ("3", "1")])
    ]);
    /// <summary>
    ///     Builds a one-page drawing whose text-less shapes are instantiated from named masters,
    ///     reproducing the mix a real engineering schematic carries: a labeled shape, shapes named
    ///     only by their master type, a shape whose master describes the connector itself, and a
    ///     shape that names no master at all.
    /// </summary>
    /// <returns>The drawing bytes.</returns>
    /// <remarks>
    ///     Master <c>7</c> declares only <c>NameU</c> so the reader's fallback from <c>Name</c> is
    ///     exercised against a real package rather than a hand-built model. The edges deliberately
    ///     produce every endpoint class: text, type, connective-master (suppressed), and no master.
    /// </remarks>
    public static byte[] MasterTypedSchematic() => VisioPackageBuilder.Build(
        [
            new VisioBuildPage(
                "Schematic",
                [
                    new VisioBuildShape("1", "Inlet Tank", "2"),
                    new VisioBuildShape("2", null, "6"),
                    new VisioBuildShape("3", null, "5"),
                    new VisioBuildShape("4", null),
                    new VisioBuildShape("5", null, "7")
                ],
                [("1", "2"), ("2", "3"), ("3", "4"), ("4", "5")])
        ],
        [
            new VisioBuildMaster("2", Name: "Tank", NameU: "Tank"),
            new VisioBuildMaster("5", Name: "Dynamic connector", NameU: "Dynamic connector"),
            new VisioBuildMaster("6", Name: "Positive displacement", NameU: "Positive displacement"),
            new VisioBuildMaster("7", NameU: "Cyclone 1")
        ]);

    /// <summary>
    ///     Builds a one-page drawing that embeds one EMF vector metafile image, so the
    ///     vector-passthrough caveat can be exercised end to end.
    /// </summary>
    /// <returns>The drawing bytes.</returns>
    /// <remarks>
    ///     The page carries a labeled shape so it is not also flagged as empty; its single embedded
    ///     image is a Windows metafile, whose bytes are written unchanged and counted as extracted
    ///     with an informational <c>VISIO0003</c> caveat that must not degrade the run.
    /// </remarks>
    public static byte[] PageWithVectorImage() => VisioPackageBuilder.Build(
    [
        new VisioBuildPage(
            "Schematic",
            [new VisioBuildShape("1", "Inlet")],
            [],
            EmbedVectorImage: true)
    ]);

    /// <summary>
    ///     Builds a one-page drawing whose shape names a master the drawing does not declare.
    /// </summary>
    /// <returns>The drawing bytes.</returns>
    /// <remarks>
    ///     A dangling master reference must leave the shape unnamed rather than invent a type for it;
    ///     this fixture is the regression that pins that refusal.
    /// </remarks>
    public static byte[] DanglingMasterReference() => VisioPackageBuilder.Build(
        [
            new VisioBuildPage(
                "Dangling",
                [new VisioBuildShape("1", "Inlet"), new VisioBuildShape("2", null, "99")],
                [("1", "2")])
        ],
        [new VisioBuildMaster("2", Name: "Tank")]);
}
