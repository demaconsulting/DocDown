package DocDownOffice {
    part def PowerPointContentEmitter {
        doc /* The single emission path: turns a read deck model into the output contract, writing each slide's title, body text, and speaker notes in presentation order, plus the inline images, the content inventory, and the notes the model and options imply. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Office/PowerPoint/Markdown/PowerPointContentEmitter.cs */
        comment testRef /* Test: test/DemaConsulting.DocDown.Office.Tests/PowerPoint/Markdown/PowerPointContentEmitterTests.cs */
        comment designRef /* Design: docs/design/docdown-office/powerpoint/markdown/powerpoint-content-emitter.md */
        comment verificationRef /* Verification: docs/verification/docdown-office/powerpoint/markdown/powerpoint-content-emitter.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-office/powerpoint/markdown/powerpoint-content-emitter.yaml */
    }
}
