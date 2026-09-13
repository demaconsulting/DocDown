package DocDownVisio {
    part def Markdown {
        doc /* The Markdown subsystem: the reader-neutral projection of the drawing model onto markdown — the content emitter that writes one section per page carrying the page name, shape text, and the directed source-to-target topology, plus the inline images, diagnostics, and honest gaps, and the shape labeler that decides how each topology endpoint is named from what the drawing says about it. */

        comment testRef /* Test: test/DemaConsulting.DocDown.Visio.Tests/Markdown/VisioContentEmitterTests.cs */
        comment designRef /* Design: docs/design/docdown-visio/markdown.md */
        comment verificationRef /* Verification: docs/verification/docdown-visio/markdown.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-visio/markdown.yaml */

        part visioContentEmitter : VisioContentEmitter;
        part visioShapeLabeler : VisioShapeLabeler;
    }
}
