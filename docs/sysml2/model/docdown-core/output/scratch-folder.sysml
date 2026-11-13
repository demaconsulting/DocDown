package DocDownCore {
    private import SoftwareItemMetadata::*;

    part def ScratchFolder {
        doc /* Owns the output directory and is the single path-safety gate for the extraction. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Core/Output/ScratchFolder.cs");
            testFiles = ("test/DemaConsulting.DocDown.Core.Tests/Output/ScratchFolderTests.cs");
            designFiles = ("docs/design/docdown-core/output/scratch-folder.md");
            verificationFiles = ("docs/verification/docdown-core/output/scratch-folder.md");
            requirementFiles = ("docs/reqstream/docdown-core/output/scratch-folder.yaml");
        }
    }
}
