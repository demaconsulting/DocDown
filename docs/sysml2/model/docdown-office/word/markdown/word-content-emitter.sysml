package DocDownOffice {
    private import SoftwareItemMetadata::*;

    part def WordContentEmitter {
        doc /* The model-to-sink emission path: writes the model's content (single-flow or per-part), images, the content inventory, the document's reviewer comments as review comments carrying their location hints and their text exactly as the document records it, and notes through the sink. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Office/Word/Markdown/WordContentEmitter.cs");
            testFiles = ("test/DemaConsulting.DocDown.Office.Tests/Word/Markdown/WordContentEmitterTests.cs");
            designFiles = ("docs/design/docdown-office/word/markdown/word-content-emitter.md");
            verificationFiles = ("docs/verification/docdown-office/word/markdown/word-content-emitter.md");
            requirementFiles = ("docs/reqstream/docdown-office/word/markdown/word-content-emitter.yaml");
        }
    }
}
