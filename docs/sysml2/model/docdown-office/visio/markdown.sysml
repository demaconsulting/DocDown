package DocDownOffice {
    part def VisioMarkdown {
        doc /* The Markdown subsystem: the reader-neutral projection of the drawing model onto markdown — the content emitter that writes one section per page carrying the page name, shape text, and the directed source-to-target topology, plus the inline images, the content inventory, and notes, and the shape labeler that decides how each topology endpoint is named from what the drawing says about it. */

        comment testRef /* Test: test/DemaConsulting.DocDown.Office.Tests/Visio/Markdown/VisioContentEmitterTests.cs */
        comment designRef /* Design: docs/design/docdown-office/visio/markdown.md */
        comment verificationRef /* Verification: docs/verification/docdown-office/visio/markdown.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-office/visio/markdown.yaml */

        part visioContentEmitter : VisioContentEmitter;
        part visioShapeLabeler : VisioShapeLabeler;
    }
}
