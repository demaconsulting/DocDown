package DocDownOffice {
    private import SoftwareItemMetadata::*;

    part def VisioDocDownBuilderExtensions {
        doc /* The one visible edge from a host to this subsystem: the explicit, reflection-free AddVisio call that adds the managed Visio backend to a DocDownBuilder, registering a factory so nothing is constructed and no environment is probed at registration. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Office/Visio/VisioDocDownBuilderExtensions.cs");
            testFiles = ("test/DemaConsulting.DocDown.Office.Tests/Visio/VisioDocDownBuilderExtensionsTests.cs");
            designFiles = ("docs/design/docdown-office/visio/visio-doc-down-builder-extensions.md");
            verificationFiles = ("docs/verification/docdown-office/visio/visio-doc-down-builder-extensions.md");
            requirementFiles = ("docs/reqstream/docdown-office/visio/visio-doc-down-builder-extensions.yaml");
        }
    }
}
