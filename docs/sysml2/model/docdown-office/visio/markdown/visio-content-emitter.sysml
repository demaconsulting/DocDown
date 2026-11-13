package DocDownOffice {
    private import SoftwareItemMetadata::*;

    part def VisioContentEmitter {
        doc /* The single emission path: writes a read drawing model through the sink as per-page content in document order — the page name, the informative shape text, and the directed connector topology rendered as a readable source-to-target edge list — plus the inline images, the content inventory, the topology-labeling convention, and the notes the model and options imply. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Office/Visio/Markdown/VisioContentEmitter.cs");
            testFiles = ("test/DemaConsulting.DocDown.Office.Tests/Visio/Markdown/VisioContentEmitterTests.cs");
            designFiles = ("docs/design/docdown-office/visio/markdown/visio-content-emitter.md");
            verificationFiles = ("docs/verification/docdown-office/visio/markdown/visio-content-emitter.md");
            requirementFiles = ("docs/reqstream/docdown-office/visio/markdown/visio-content-emitter.yaml");
        }
    }
}
