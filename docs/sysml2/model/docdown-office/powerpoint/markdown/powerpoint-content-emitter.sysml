package DocDownOffice {
    private import SoftwareItemMetadata::*;

    part def PowerPointContentEmitter {
        doc /* The single emission path: turns a read deck model into the output contract, writing each slide's title, body text, and speaker notes in presentation order, plus the inline images, the content inventory including the comment and comment-author counts, the slide comments reported to Core as Slide {n} review comments, a note naming any modern persona comments the deck carries that this extractor leaves unread, and the notes the model and options imply. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Office/PowerPoint/Markdown/PowerPointContentEmitter.cs");
            testFiles = ("test/DemaConsulting.DocDown.Office.Tests/PowerPoint/Markdown/PowerPointContentEmitterTests.cs");
            designFiles = ("docs/design/docdown-office/powerpoint/markdown/powerpoint-content-emitter.md");
            verificationFiles = ("docs/verification/docdown-office/powerpoint/markdown/powerpoint-content-emitter.md");
            requirementFiles = ("docs/reqstream/docdown-office/powerpoint/markdown/powerpoint-content-emitter.yaml");
        }
    }
}
