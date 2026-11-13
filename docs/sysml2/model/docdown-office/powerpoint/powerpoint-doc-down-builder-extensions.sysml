package DocDownOffice {
    private import SoftwareItemMetadata::*;

    part def PowerPointDocDownBuilderExtensions {
        doc /* The reflection-free registration seam: AddPowerPoint registers the managed Open XML backend on a DocDownBuilder. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Office/PowerPoint/PowerPointDocDownBuilderExtensions.cs");
            testFiles = ("test/DemaConsulting.DocDown.Office.Tests/PowerPoint/PowerPointDocDownBuilderExtensionsTests.cs");
            designFiles = ("docs/design/docdown-office/powerpoint/powerpoint-doc-down-builder-extensions.md");
            verificationFiles = ("docs/verification/docdown-office/powerpoint/powerpoint-doc-down-builder-extensions.md");
            requirementFiles = ("docs/reqstream/docdown-office/powerpoint/powerpoint-doc-down-builder-extensions.yaml");
        }
    }
}
