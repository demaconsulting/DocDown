package DocDownCore {
    part def Output {
        doc /* The sole write path for an extraction: prepares the scratch folder, contains every path, and writes the artifacts. */

        comment testRef /* Test: test/DemaConsulting.DocDown.Core.Tests/Output/OutputTests.cs */
        comment designRef /* Design: docs/design/docdown-core/output.md */
        comment verificationRef /* Verification: docs/verification/docdown-core/output.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/output.yaml */

        part scratchFolder : ScratchFolder;
        part extractionSink : ExtractionSink;
        part contentWriter : ContentWriter;
        part summaryWriter : SummaryWriter;
        part manifestWriter : ManifestWriter;
        part imageTextSelector : ImageTextSelector;
    }

    part def IExtractionSink {
        doc /* The write surface a backend uses to add images, pages, and content, and to report notes and the content inventory. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Output/ExtractionSink.cs */
        comment designRef /* Design: docs/design/docdown-core/output.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/output.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/output.yaml (documented as a supporting type) */
    }

    part def ImageHint {
        doc /* Extractor-supplied advisory image metadata; Core allocates the real name. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Output/EmbeddedImageWriter.cs */
        comment designRef /* Design: docs/design/docdown-core/output.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/output.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/output.yaml (documented as a supporting type) */
    }

    part def ImageTextSource {
        doc /* The origin of a candidate image text, in decreasing directness: Description, Title, Caption, PictureName, Heading, Uri. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Output/ImageTextSelector.cs */
        comment designRef /* Design: docs/design/docdown-core/output/image-text-selector.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/output/image-text-selector.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/output/image-text-selector.yaml (documented as a supporting type) */
    }

    part def ImageTextConfidence {
        doc /* How far a selected image text may be trusted: Descriptive, Contextual, or Fallback. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Output/ImageTextSelector.cs */
        comment designRef /* Design: docs/design/docdown-core/output/image-text-selector.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/output/image-text-selector.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/output/image-text-selector.yaml (documented as a supporting type) */
    }

    part def ImageTextCandidate {
        doc /* One candidate image text paired with the source it came from. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Output/ImageTextSelector.cs */
        comment designRef /* Design: docs/design/docdown-core/output/image-text-selector.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/output/image-text-selector.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/output/image-text-selector.yaml (documented as a supporting type) */
    }

    part def SelectedImageText {
        doc /* The chosen image text with its source and confidence, produced by ImageTextSelector. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Output/ImageTextSelector.cs */
        comment designRef /* Design: docs/design/docdown-core/output/image-text-selector.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/output/image-text-selector.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/output/image-text-selector.yaml (documented as a supporting type) */
    }

    part def ContentPart {
        doc /* A content part supplied by an extractor: kind, an advisory ordinal, and an optional title. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Output/ContentWriter.cs */
        comment designRef /* Design: docs/design/docdown-core/output.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/output.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/output.yaml (documented as a supporting type) */
    }

    part def ContentPartKind {
        doc /* The kind of a content part: Page, Sheet, Slide, Section, or Attachment. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Output/ContentWriter.cs */
        comment designRef /* Design: docs/design/docdown-core/output.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/output.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/output.yaml (documented as a supporting type) */
    }

    part def ExtractionNote {
        doc /* A short plain-language note recorded when DocDown attempted a step and could not complete it; carries a message only, never a code, severity, remedy, or impact. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Output/ExtractionSink.cs */
        comment designRef /* Design: docs/design/docdown-core/output.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/output.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/output.yaml (documented as a supporting type) */
    }

    part def MetadataWriter {
        doc /* Serializes metadata.json, the record of what the document asserts about itself, with per-field provenance, omit-empty, and the never-bare-{} rule. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Output/MetadataWriter.cs */
        comment testRef /* Test: test/DemaConsulting.DocDown.Core.Tests/Output/MetadataWriterTests.cs */
        comment designRef /* Design: docs/design/docdown-core/output.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/output.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/output.yaml (documented as a supporting type) */
    }

    part def ExtractionEnvironment {
        doc /* Provenance: operating system, architecture, runtime, identifier, and an ordered list of facts. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Output/ManifestWriter.cs */
        comment designRef /* Design: docs/design/docdown-core/output.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/output.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/output.yaml (documented as a supporting type) */
    }

    part def EnvironmentFact {
        doc /* One environment fact: a contributing-component source, a key, a value, and an optional availability flag. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Output/ManifestWriter.cs */
        comment designRef /* Design: docs/design/docdown-core/output.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/output.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/output.yaml (documented as a supporting type) */
    }

    part def ArtifactInventory {
        doc /* The single shared definition of what a manifest accounts for, used by the scratch-folder reuse guard to delete only what this library wrote. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Output/ArtifactInventory.cs */
        comment designRef /* Design: docs/design/docdown-core/output.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/output.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/output.yaml (documented as a supporting type) */
    }

    part def ExtractionManifest {
        doc /* The manifest.json serialization model and its nested DTO records. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Output/ExtractionManifest.cs */
        comment designRef /* Design: docs/design/docdown-core/output.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/output.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/output.yaml (documented as a supporting type) */
    }

    part def DocDownJsonContext {
        doc /* The source-generated serializer context for the manifest DTO graph, keeping serialization trim- and AOT-safe. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Output/ManifestWriter.cs */
        comment designRef /* Design: docs/design/docdown-core/output.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/output.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/output.yaml (documented as a supporting type) */
    }

    part def ScratchFolderException {
        doc /* A structured scratch-folder refusal carrying a machine reason; converted by the engine into a prose extraction failure. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Output/ScratchFolder.cs */
        comment designRef /* Design: docs/design/docdown-core/output.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/output.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/output.yaml (documented as a supporting type) */
    }

    part def ImageDimensions {
        doc /* Reads pixel dimensions from PNG, JPEG, GIF and BMP headers without decoding the image. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Output/ImageDimensions.cs */
        comment designRef /* Design: docs/design/docdown-core/output.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/output.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/output.yaml (documented as a supporting type) */
    }
    part def ImageLinkText {
        doc /* Builds the markdown link text for an extracted image from the selected image text. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Output/ImageLinkText.cs */
        comment designRef /* Design: docs/design/docdown-core/output/image-text-selector.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/output/image-text-selector.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/output/image-text-selector.yaml (documented as a supporting type) */
    }
    part def PartResourceLinkRewriter {
        doc /* Rewrites resource links in a content part so they resolve from the part file's own folder. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Output/PartResourceLinkRewriter.cs */
        comment designRef /* Design: docs/design/docdown-core/output/content-writer.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/output/content-writer.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/output/content-writer.yaml (documented as a supporting type) */
    }
}
