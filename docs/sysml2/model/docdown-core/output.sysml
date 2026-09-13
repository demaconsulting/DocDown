package DocDownCore {
    part def Output {
        doc /* The sole write path for an extraction: prepares the scratch folder, contains every path, writes the artifacts, and verifies the result. */

        comment testRef /* Test: test/DemaConsulting.DocDown.Core.Tests/Output/OutputTests.cs */
        comment designRef /* Design: docs/design/docdown-core/output.md */
        comment verificationRef /* Verification: docs/verification/docdown-core/output.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/output.yaml */

        part scratchFolder : ScratchFolder;
        part extractionSink : ExtractionSink;
        part contentWriter : ContentWriter;
        part summaryWriter : SummaryWriter;
        part manifestWriter : ManifestWriter;
        part contractVerifier : ContractVerifier;
        part imageTextSelector : ImageTextSelector;
    }

    part def IExtractionSink {
        doc /* The write surface a backend uses to add images, pages, content, and report the honesty stream. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Output/IExtractionSink.cs */
        comment designRef /* Design: docs/design/docdown-core/output.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/output.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/output.yaml (documented as a supporting type) */
    }

    part def ImageHint {
        doc /* Extractor-supplied advisory image metadata; Core allocates the real name. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Output/ImageHint.cs */
        comment designRef /* Design: docs/design/docdown-core/output.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/output.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/output.yaml (documented as a supporting type) */
    }

    part def ImageTextSource {
        doc /* The origin of a candidate image text, in decreasing directness: Description, Title, Caption, PictureName, Heading, Uri. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Output/ImageTextSource.cs */
        comment designRef /* Design: docs/design/docdown-core/output/image-text-selector.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/output/image-text-selector.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/output/image-text-selector.yaml (documented as a supporting type) */
    }

    part def ImageTextConfidence {
        doc /* How far a selected image text may be trusted: Descriptive, Contextual, or Fallback. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Output/ImageTextConfidence.cs */
        comment designRef /* Design: docs/design/docdown-core/output/image-text-selector.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/output/image-text-selector.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/output/image-text-selector.yaml (documented as a supporting type) */
    }

    part def ImageTextCandidate {
        doc /* One candidate image text paired with the source it came from. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Output/ImageTextCandidate.cs */
        comment designRef /* Design: docs/design/docdown-core/output/image-text-selector.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/output/image-text-selector.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/output/image-text-selector.yaml (documented as a supporting type) */
    }

    part def SelectedImageText {
        doc /* The chosen image text with its source and confidence, produced by ImageTextSelector. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Output/SelectedImageText.cs */
        comment designRef /* Design: docs/design/docdown-core/output/image-text-selector.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/output/image-text-selector.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/output/image-text-selector.yaml (documented as a supporting type) */
    }

    part def ContentPart {
        doc /* A content part supplied by an extractor: kind, an advisory ordinal, and an optional title. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Output/ContentPart.cs */
        comment designRef /* Design: docs/design/docdown-core/output.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/output.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/output.yaml (documented as a supporting type) */
    }

    part def ContentPartKind {
        doc /* The kind of a content part: Page, Sheet, Slide, Section, or Attachment. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Output/ContentPartKind.cs */
        comment designRef /* Design: docs/design/docdown-core/output.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/output.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/output.yaml (documented as a supporting type) */
    }

    part def ExtractionGap {
        doc /* An enumerated, explained gap in the extraction with a mandatory reason. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Output/ExtractionGap.cs */
        comment designRef /* Design: docs/design/docdown-core/output.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/output.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/output.yaml (documented as a supporting type) */
    }

    part def GapKind {
        doc /* The kind of content a gap concerns: Text, Images, Pages, Structure, Metadata, or Parts. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Output/GapKind.cs */
        comment designRef /* Design: docs/design/docdown-core/output.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/output.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/output.yaml (documented as a supporting type) */
    }

    part def GapScope {
        doc /* Why content is missing: NotAttempted, Unavailable, PartiallyExtracted, or Failed. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Output/GapScope.cs */
        comment designRef /* Design: docs/design/docdown-core/output.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/output.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/output.yaml (documented as a supporting type) */
    }

    part def ArtifactLedger {
        doc /* The six-entry completeness ledger stating what the extraction produced. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Output/ArtifactLedger.cs */
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

    part def ArtifactEntry {
        doc /* One completeness-ledger entry: path, status, and optional obtained/found counts. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Output/ArtifactEntry.cs */
        comment designRef /* Design: docs/design/docdown-core/output.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/output.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/output.yaml (documented as a supporting type) */
    }

    part def ArtifactStatus {
        doc /* The status of a ledger artifact: Present, Partial, or Absent. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Output/ArtifactStatus.cs */
        comment designRef /* Design: docs/design/docdown-core/output.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/output.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/output.yaml (documented as a supporting type) */
    }

    part def ExtractionEnvironment {
        doc /* Provenance: operating system, architecture, runtime, identifier, and an ordered list of facts. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Output/ExtractionEnvironment.cs */
        comment designRef /* Design: docs/design/docdown-core/output.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/output.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/output.yaml (documented as a supporting type) */
    }

    part def EnvironmentFact {
        doc /* One environment fact: a contributing-component source, a key, a value, and an optional availability flag. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Output/EnvironmentFact.cs */
        comment designRef /* Design: docs/design/docdown-core/output.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/output.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/output.yaml (documented as a supporting type) */
    }

    part def ContractViolation {
        doc /* One contract-verification finding: a code and a detail. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Output/ContractViolation.cs */
        comment designRef /* Design: docs/design/docdown-core/output.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/output.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/output.yaml (documented as a supporting type) */
    }

    part def ArtifactInventory {
        doc /* The single shared definition of what a manifest accounts for, used by the contract verifier and by the scratch-folder reuse guard. */

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

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Output/DocDownJsonContext.cs */
        comment designRef /* Design: docs/design/docdown-core/output.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/output.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/output.yaml (documented as a supporting type) */
    }

    part def ScratchFolderException {
        doc /* A structured scratch-folder refusal carrying a machine reason; converted by the engine into a ScratchFolderRefused failure. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Output/ScratchFolderException.cs */
        comment designRef /* Design: docs/design/docdown-core/output.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/output.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/output.yaml (documented as a supporting type) */
    }
}
