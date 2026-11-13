package DocDownOffice {
    private import SoftwareItemMetadata::*;

    part def VisioMarkdown {
        doc /* The Markdown subsystem: the reader-neutral projection of the drawing model onto markdown — the content emitter that writes one section per page carrying the page name, shape text, and the directed source-to-target topology, plus the inline images, the content inventory, and notes, and the shape labeler that decides how each topology endpoint is named from what the drawing says about it. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.DocDown.Office.Tests/Visio/Markdown/VisioContentEmitterTests.cs");
            designFiles = ("docs/design/docdown-office/visio/markdown.md");
            verificationFiles = ("docs/verification/docdown-office/visio/markdown.md");
            requirementFiles = ("docs/reqstream/docdown-office/visio/markdown.yaml");
        }

        part visioContentEmitter : VisioContentEmitter;
        part visioShapeLabeler : VisioShapeLabeler;
    }
}
