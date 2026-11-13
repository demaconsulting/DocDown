package DocDownCore {
    private import SoftwareItemMetadata::*;

    part def Output {
        doc /* The sole write path for an extraction: prepares the scratch folder, contains every path, and writes the artifacts. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.DocDown.Core.Tests/Output/OutputTests.cs");
            designFiles = ("docs/design/docdown-core/output.md");
            verificationFiles = ("docs/verification/docdown-core/output.md");
            requirementFiles = ("docs/reqstream/docdown-core/output.yaml");
        }

        part scratchFolder : ScratchFolder;
        part extractionSink : ExtractionSink;
        part contentWriter : ContentWriter;
        part reviewCommentsWriter : ReviewCommentsWriter;
        part summaryWriter : SummaryWriter;
        part manifestWriter : ManifestWriter;
        part imageTextSelector : ImageTextSelector;
        part embeddedImageWriter : EmbeddedImageWriter;
        part imageDimensions : ImageDimensions;
        part imageLinkText : ImageLinkText;
    }

    part def IExtractionSink {
        doc /* The write surface a backend uses to add images, pages, and content, and to report notes and the content inventory (documented as a supporting type). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Core/Output/ExtractionSink.cs");
            designFiles = ("docs/design/docdown-core/output.md");
            verificationFiles = ("docs/verification/docdown-core/output.md");
            requirementFiles = ("docs/reqstream/docdown-core/output.yaml");
        }
    }

    part def ImageHint {
        doc /* Extractor-supplied advisory image metadata; Core allocates the real name (documented as a supporting type). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Core/Output/EmbeddedImageWriter.cs");
            designFiles = ("docs/design/docdown-core/output.md");
            verificationFiles = ("docs/verification/docdown-core/output.md");
            requirementFiles = ("docs/reqstream/docdown-core/output.yaml");
        }
    }

    part def ImageTextSource {
        doc /* The origin of a candidate image text, in decreasing directness: Description, Title, Caption, PictureName, Heading, Uri (documented as a supporting type). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Core/Output/ImageTextSelector.cs");
            designFiles = ("docs/design/docdown-core/output/image-text-selector.md");
            verificationFiles = ("docs/verification/docdown-core/output/image-text-selector.md");
            requirementFiles = ("docs/reqstream/docdown-core/output/image-text-selector.yaml");
        }
    }

    part def ImageTextConfidence {
        doc /* How far a selected image text may be trusted: Descriptive, Contextual, or Fallback (documented as a supporting type). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Core/Output/ImageTextSelector.cs");
            designFiles = ("docs/design/docdown-core/output/image-text-selector.md");
            verificationFiles = ("docs/verification/docdown-core/output/image-text-selector.md");
            requirementFiles = ("docs/reqstream/docdown-core/output/image-text-selector.yaml");
        }
    }

    part def ImageTextCandidate {
        doc /* One candidate image text paired with the source it came from (documented as a supporting type). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Core/Output/ImageTextSelector.cs");
            designFiles = ("docs/design/docdown-core/output/image-text-selector.md");
            verificationFiles = ("docs/verification/docdown-core/output/image-text-selector.md");
            requirementFiles = ("docs/reqstream/docdown-core/output/image-text-selector.yaml");
        }
    }

    part def SelectedImageText {
        doc /* The chosen image text with its source and confidence, produced by ImageTextSelector (documented as a supporting type). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Core/Output/ImageTextSelector.cs");
            designFiles = ("docs/design/docdown-core/output/image-text-selector.md");
            verificationFiles = ("docs/verification/docdown-core/output/image-text-selector.md");
            requirementFiles = ("docs/reqstream/docdown-core/output/image-text-selector.yaml");
        }
    }

    part def ContentPart {
        doc /* A content part supplied by an extractor: kind, an advisory ordinal, and an optional title (documented as a supporting type). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Core/Output/ContentWriter.cs");
            designFiles = ("docs/design/docdown-core/output.md");
            verificationFiles = ("docs/verification/docdown-core/output.md");
            requirementFiles = ("docs/reqstream/docdown-core/output.yaml");
        }
    }

    part def ContentPartKind {
        doc /* The kind of a content part: Page, Sheet, Slide, Section, or Attachment (documented as a supporting type). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Core/Output/ContentWriter.cs");
            designFiles = ("docs/design/docdown-core/output.md");
            verificationFiles = ("docs/verification/docdown-core/output.md");
            requirementFiles = ("docs/reqstream/docdown-core/output.yaml");
        }
    }

    part def ExtractionNote {
        doc /* A short plain-language note recorded when DocDown attempted a step and could not complete it; carries a message only, never a code, severity, remedy, or impact (documented as a supporting type). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Core/Output/ExtractionSink.cs");
            designFiles = ("docs/design/docdown-core/output.md");
            verificationFiles = ("docs/verification/docdown-core/output.md");
            requirementFiles = ("docs/reqstream/docdown-core/output.yaml");
        }
    }

    part def DocumentComment {
        doc /* One reviewer comment a document carries: an optional author, the comment body, and a single precomputed human-readable location phrase composed by the backend (documented as a supporting type). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Core/Output/ReviewCommentsWriter.cs");
            designFiles = ("docs/design/docdown-core/output/review-comments-writer.md");
            verificationFiles = ("docs/verification/docdown-core/output/review-comments-writer.md");
            requirementFiles = ("docs/reqstream/docdown-core/output/review-comments-writer.yaml");
        }
    }

    part def ReviewCommentsWriteResult {
        doc /* The outcome of finalizing review-comments.md: the relative path when a file was written, or null and a zero count when the document carried no comments (documented as a supporting type). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Core/Output/ReviewCommentsWriter.cs");
            designFiles = ("docs/design/docdown-core/output/review-comments-writer.md");
            verificationFiles = ("docs/verification/docdown-core/output/review-comments-writer.md");
            requirementFiles = ("docs/reqstream/docdown-core/output/review-comments-writer.yaml");
        }
    }

    part def MetadataWriter {
        doc /* Serializes metadata.json, the record of what the document asserts about itself, with per-field provenance, omit-empty, and the never-bare-{} rule (documented as a supporting type). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Core/Output/MetadataWriter.cs");
            testFiles = ("test/DemaConsulting.DocDown.Core.Tests/Output/MetadataWriterTests.cs");
            designFiles = ("docs/design/docdown-core/output.md");
            verificationFiles = ("docs/verification/docdown-core/output.md");
            requirementFiles = ("docs/reqstream/docdown-core/output.yaml");
        }
    }

    part def ExtractionEnvironment {
        doc /* Provenance: operating system, architecture, runtime, identifier, and an ordered list of facts (documented as a supporting type). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Core/Output/ManifestWriter.cs");
            designFiles = ("docs/design/docdown-core/output.md");
            verificationFiles = ("docs/verification/docdown-core/output.md");
            requirementFiles = ("docs/reqstream/docdown-core/output.yaml");
        }
    }

    part def EnvironmentFact {
        doc /* One environment fact: a contributing-component source, a key, a value, and an optional availability flag (documented as a supporting type). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Core/Output/ManifestWriter.cs");
            designFiles = ("docs/design/docdown-core/output.md");
            verificationFiles = ("docs/verification/docdown-core/output.md");
            requirementFiles = ("docs/reqstream/docdown-core/output.yaml");
        }
    }

    part def ArtifactInventory {
        doc /* The single shared definition of what a manifest accounts for, used by the scratch-folder reuse guard to delete only what this library wrote (documented as a supporting type). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Core/Output/ArtifactInventory.cs");
            designFiles = ("docs/design/docdown-core/output.md");
            verificationFiles = ("docs/verification/docdown-core/output.md");
            requirementFiles = ("docs/reqstream/docdown-core/output.yaml");
        }
    }

    part def ExtractionManifest {
        doc /* The manifest.json serialization model and its nested DTO records (documented as a supporting type). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Core/Output/ExtractionManifest.cs");
            designFiles = ("docs/design/docdown-core/output.md");
            verificationFiles = ("docs/verification/docdown-core/output.md");
            requirementFiles = ("docs/reqstream/docdown-core/output.yaml");
        }
    }

    part def DocDownJsonContext {
        doc /* The source-generated serializer context for the manifest DTO graph, keeping serialization trim- and AOT-safe (documented as a supporting type). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Core/Output/ManifestWriter.cs");
            designFiles = ("docs/design/docdown-core/output.md");
            verificationFiles = ("docs/verification/docdown-core/output.md");
            requirementFiles = ("docs/reqstream/docdown-core/output.yaml");
        }
    }

    part def ScratchFolderException {
        doc /* A structured scratch-folder refusal carrying a machine reason; converted by the engine into a prose extraction failure (documented as a supporting type). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Core/Output/ScratchFolder.cs");
            designFiles = ("docs/design/docdown-core/output.md");
            verificationFiles = ("docs/verification/docdown-core/output.md");
            requirementFiles = ("docs/reqstream/docdown-core/output.yaml");
        }
    }

    part def PartResourceLinkRewriter {
        doc /* Rewrites resource links in a content part so they resolve from the part file's own folder (documented as a supporting type). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Core/Output/PartResourceLinkRewriter.cs");
            designFiles = ("docs/design/docdown-core/output/content-writer.md");
            verificationFiles = ("docs/verification/docdown-core/output/content-writer.md");
            requirementFiles = ("docs/reqstream/docdown-core/output/content-writer.yaml");
        }
    }
}
