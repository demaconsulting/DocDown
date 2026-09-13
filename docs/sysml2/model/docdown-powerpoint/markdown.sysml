package DocDownPowerPoint {
    part def Markdown {
        doc /* The Markdown subsystem: the reader-neutral projection of the deck model onto the output contract — the content emitter that writes each slide's title, text, and speaker notes, the inline images, the content inventory, and notes. */

        comment testRef /* Test: test/DemaConsulting.DocDown.PowerPoint.Tests/Markdown/PowerPointContentEmitterTests.cs */
        comment designRef /* Design: docs/design/docdown-powerpoint/markdown.md */
        comment verificationRef /* Verification: docs/verification/docdown-powerpoint/markdown.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-powerpoint/markdown.yaml */

        part powerPointContentEmitter : PowerPointContentEmitter;
    }
}
