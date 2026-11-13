package DocDownOffice {
    private import SoftwareItemMetadata::*;

    part def PowerPointMarkdown {
        doc /* The Markdown subsystem: the reader-neutral projection of the deck model onto the output contract — the content emitter that writes each slide's title, text, and speaker notes, the inline images, the content inventory, and notes. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.DocDown.Office.Tests/PowerPoint/Markdown/PowerPointContentEmitterTests.cs");
            designFiles = ("docs/design/docdown-office/powerpoint/markdown.md");
            verificationFiles = ("docs/verification/docdown-office/powerpoint/markdown.md");
            requirementFiles = ("docs/reqstream/docdown-office/powerpoint/markdown.yaml");
        }

        part powerPointContentEmitter : PowerPointContentEmitter;
    }
}
