package DocDownPowerPoint {
    doc
    /*
     * DocDown.PowerPoint is the PowerPoint extraction package: a separately distributed backend that
     * reads a deck's slide text, titles, speaker notes, slide order, and embedded images and metadata
     * into the DocDown output contract, and — when Microsoft PowerPoint is available — a rendered image
     * of each slide. A slide is a visual composition, so the render is captured when available, but the
     * text, titles, speaker notes, and slide order are always extracted from the file itself. It has
     * three subsystems (Com, Markdown, OpenXml) and one direct unit (PowerPointDocDownBuilderExtensions).
     */

    part def DocDownPowerPointSystem {
        doc /* The DocDown.PowerPoint system as a whole. */

        comment testRef /* Test: test/DemaConsulting.DocDown.PowerPoint.Tests/DocDownPowerPointTests.cs */
        comment designRef /* Design: docs/design/docdown-powerpoint.md */
        comment verificationRef /* Verification: docs/verification/docdown-powerpoint.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-powerpoint.yaml */

        part powerPointDocDownBuilderExtensions : PowerPointDocDownBuilderExtensions;
        part com : Com;
        part markdown : Markdown;
        part openXml : OpenXml;
    }
}
