package DocDownCore {
    private import SoftwareItemMetadata::*;

    doc
    /*
     * DemaConsulting.DocDown.Core is a .NET library providing the core functionality for the DocDown
     * family of document-to-markdown extraction libraries.
     */

    part def DocDownCoreSystem {
        doc /* The DemaConsulting.DocDown.Core system as a whole. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.DocDown.Core.Tests/DocDownCoreTests.cs");
            designFiles = ("docs/design/docdown-core.md");
            verificationFiles = ("docs/verification/docdown-core.md");
            requirementFiles = ("docs/reqstream/docdown-core.yaml");
        }

        part detection : Detection;
        part extraction : Extraction;
        part output : Output;
    }
}
