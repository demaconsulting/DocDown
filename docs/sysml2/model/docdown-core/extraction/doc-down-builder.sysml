package DocDownCore {
    private import SoftwareItemMetadata::*;

    part def DocDownBuilder {
        doc /* The fluent configuration stage that collects extractor registrations and default options and produces an engine. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Core/Extraction/DocDownBuilder.cs");
            testFiles = ("test/DemaConsulting.DocDown.Core.Tests/Extraction/DocDownBuilderTests.cs");
            designFiles = ("docs/design/docdown-core/extraction/doc-down-builder.md");
            verificationFiles = ("docs/verification/docdown-core/extraction/doc-down-builder.md");
            requirementFiles = ("docs/reqstream/docdown-core/extraction/doc-down-builder.yaml");
        }
    }
}
