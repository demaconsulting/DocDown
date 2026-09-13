package DocDownVisio {
    doc
    /*
     * DocDown.Visio is the Visio extraction package: a separately distributed backend that reads a
     * drawing's page names, shape text, and — above all — the directed connector topology into the
     * DocDown output contract, plus its embedded images and metadata, and — when Microsoft Visio is
     * available — a rendered image of each page. A Visio drawing is an engineering schematic whose
     * meaning is the topology, so the page names, shape text, and directed source-to-target edges are
     * always extracted from the file itself with no Visio installation present; the render is added when
     * an environment can supply it. It has three subsystems (Com, Markdown, OpenXml) and one direct unit
     * (VisioDocDownBuilderExtensions).
     */

    part def DocDownVisioSystem {
        doc /* The DocDown.Visio system as a whole. */

        comment testRef /* Test: test/DemaConsulting.DocDown.Visio.Tests/DocDownVisioTests.cs */
        comment designRef /* Design: docs/design/docdown-visio.md */
        comment verificationRef /* Verification: docs/verification/docdown-visio.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-visio.yaml */

        part visioDocDownBuilderExtensions : VisioDocDownBuilderExtensions;
        part com : Com;
        part markdown : Markdown;
        part openXml : OpenXml;
    }
}
