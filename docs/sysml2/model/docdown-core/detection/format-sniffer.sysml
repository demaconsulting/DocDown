package DocDownCore {
    private import SoftwareItemMetadata::*;

    part def FormatSniffer {
        doc /* The Detection entry point; identifies a document's format from its file-name extension and, only as a fallback, from its leading content bytes. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Core/Detection/FormatSniffer.cs");
            testFiles = ("test/DemaConsulting.DocDown.Core.Tests/Detection/FormatSnifferTests.cs");
            designFiles = ("docs/design/docdown-core/detection/format-sniffer.md");
            verificationFiles = ("docs/verification/docdown-core/detection/format-sniffer.md");
            requirementFiles = ("docs/reqstream/docdown-core/detection/format-sniffer.yaml");
        }
    }
}
