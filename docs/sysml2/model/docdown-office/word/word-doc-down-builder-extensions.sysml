package DocDownOffice {
    private import SoftwareItemMetadata::*;

    part def WordDocDownBuilderExtensions {
        doc /* The registration seam: explicit, reflection-free calls that add the Word backends to a DocDown builder. A direct unit of the DemaConsulting.DocDown.Word subsystem. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Office/Word/WordDocDownBuilderExtensions.cs");
            testFiles = ("test/DemaConsulting.DocDown.Office.Tests/Word/WordDocDownBuilderExtensionsTests.cs");
            designFiles = ("docs/design/docdown-office/word/word-doc-down-builder-extensions.md");
            verificationFiles = ("docs/verification/docdown-office/word/word-doc-down-builder-extensions.md");
            requirementFiles = ("docs/reqstream/docdown-office/word/word-doc-down-builder-extensions.yaml");
        }
    }
}
