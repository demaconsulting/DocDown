package DocDownOffice {
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

    part def Visio {
        doc /* The DocDown.Visio system as a whole. */

        comment testRef /* Test: test/DemaConsulting.DocDown.Office.Tests/Visio/DocDownVisioTests.cs */
        comment designRef /* Design: docs/design/docdown-office/visio.md */
        comment verificationRef /* Verification: docs/verification/docdown-office/visio.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-office/visio.yaml */

        part visioDocDownBuilderExtensions : VisioDocDownBuilderExtensions;
        part com : VisioCom;
        part markdown : VisioMarkdown;
        part openXml : VisioOpenXml;
    }
}
