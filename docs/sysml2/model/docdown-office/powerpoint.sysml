package DocDownOffice {
    doc
    /*
     * DocDown.PowerPoint is the PowerPoint extraction package: a separately distributed backend that
     * reads a deck's slide text, titles, speaker notes, slide order, and embedded images and metadata
     * into the DocDown output contract. The text, titles, speaker notes, and slide order are always
     * extracted from the file itself; rendered slide images are a separate, opt-in concern provided by
     * the DocDown.PowerPoint.Rendering package. It has two subsystems (Markdown, OpenXml) and one direct
     * unit (PowerPointDocDownBuilderExtensions).
     */

    part def PowerPoint {
        doc /* The DocDown.PowerPoint system as a whole. */

        comment testRef /* Test: test/DemaConsulting.DocDown.Office.Tests/PowerPoint/DocDownPowerPointTests.cs */
        comment designRef /* Design: docs/design/docdown-office/powerpoint.md */
        comment verificationRef /* Verification: docs/verification/docdown-office/powerpoint.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-office/powerpoint.yaml */

        part powerPointDocDownBuilderExtensions : PowerPointDocDownBuilderExtensions;
        part markdown : PowerPointMarkdown;
        part openXml : PowerPointOpenXml;
    }
}
