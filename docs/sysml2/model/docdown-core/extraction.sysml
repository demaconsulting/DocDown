package DocDownCore {
    part def Extraction {
        doc /* Owns the backend model and pipeline: registers extractors, selects the best available backend, and orchestrates one extraction. */

        comment testRef /* Test: test/DemaConsulting.DocDown.Core.Tests/Extraction/ExtractionTests.cs */
        comment designRef /* Design: docs/design/docdown-core/extraction.md */
        comment verificationRef /* Verification: docs/verification/docdown-core/extraction.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/extraction.yaml */

        part docDownBuilder : DocDownBuilder;
        part extractorRegistry : ExtractorRegistry;
        part extractorSelector : ExtractorSelector;
        part docDownEngine : DocDownEngine;
    }

    part def IDocumentExtractor {
        doc /* The backend contract implemented by external extractor packages. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Extraction/IDocumentExtractor.cs */
        comment designRef /* Design: docs/design/docdown-core/extraction.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/extraction.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/extraction.yaml (documented as a supporting type) */
    }

    part def IExtractionContext {
        doc /* What a backend sees during extraction: options, sink, detected format, selected extractor, and environment - but no output path. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Extraction/ExtractionContext.cs */
        comment designRef /* Design: docs/design/docdown-core/extraction.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/extraction.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/extraction.yaml (documented as a supporting type) */
    }

    part def ExtractionContext {
        doc /* The concrete IExtractionContext the engine constructs per run. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Extraction/ExtractionContext.cs */
        comment designRef /* Design: docs/design/docdown-core/extraction.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/extraction.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/extraction.yaml (documented as a supporting type) */
    }

    part def ExtractorAvailability {
        doc /* Whether a backend is usable now, an optional reason, and whether it can render pages here. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Extraction/IDocumentExtractor.cs */
        comment designRef /* Design: docs/design/docdown-core/extraction.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/extraction.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/extraction.yaml (documented as a supporting type) */
    }

    part def ExtractorDescriptor {
        doc /* An immutable snapshot of a backend's identity and supported formats, detached from the live instance. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Extraction/IDocumentExtractor.cs */
        comment designRef /* Design: docs/design/docdown-core/extraction.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/extraction.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/extraction.yaml (documented as a supporting type) */
    }

    part def ExtractorCandidate {
        doc /* A descriptor paired with its probed availability; the unit of input to selection. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Extraction/ExtractorSelector.cs */
        comment designRef /* Design: docs/design/docdown-core/extraction.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/extraction.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/extraction.yaml (documented as a supporting type) */
    }

    part def ExtractionOutcome {
        doc /* Whether an extraction produced the invariant output layout or the document was unreadable. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Extraction/ExtractionResult.cs */
        comment designRef /* Design: docs/design/docdown-core/extraction.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/extraction.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/extraction.yaml (documented as a supporting type) */
    }

    part def ExtractionFailure {
        doc /* A plain-language failure: a one-line summary and a multi-line displayable explanation. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Extraction/ExtractionResult.cs */
        comment designRef /* Design: docs/design/docdown-core/extraction.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/extraction.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/extraction.yaml (documented as a supporting type) */
    }

    part def ExtractionOptions {
        doc /* The request configuration for one extraction: page rendering, image inclusion, scratch mode, and preferences. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Extraction/ExtractionOptions.cs */
        comment designRef /* Design: docs/design/docdown-core/extraction.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/extraction.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/extraction.yaml (documented as a supporting type) */
    }

    part def ScratchFolderMode {
        doc /* How the scratch folder is prepared: CleanIfDocDownFolder or Overwrite. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Extraction/ExtractionOptions.cs */
        comment designRef /* Design: docs/design/docdown-core/extraction.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/extraction.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/extraction.yaml (documented as a supporting type) */
    }

    part def PageRange {
        doc /* An inclusive first-last page range with containment and count helpers. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Extraction/ExtractionOptions.cs */
        comment designRef /* Design: docs/design/docdown-core/extraction.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/extraction.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/extraction.yaml (documented as a supporting type) */
    }

    part def DocumentSource {
        doc /* A file- or stream-backed document input with its metadata and an OpenRead accessor. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Extraction/DocumentSource.cs */
        comment designRef /* Design: docs/design/docdown-core/extraction.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/extraction.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/extraction.yaml (documented as a supporting type) */
    }

    part def DocumentInfo {
        doc /* Extractor-reported metadata: optional title, author, page count, and part count. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Extraction/DocumentSource.cs */
        comment designRef /* Design: docs/design/docdown-core/extraction.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/extraction.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/extraction.yaml (documented as a supporting type) */
    }

    part def DocumentMetadata {
        doc /* What a document asserts about itself: populated self-reported fields with per-field provenance, plus the interesting fields left blank. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Extraction/DocumentSource.cs */
        comment designRef /* Design: docs/design/docdown-core/output.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/output.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/output.yaml (documented as a supporting type) */
    }

    part def MetadataProvenance {
        doc /* The origin of a self-reported metadata value: OpcCoreProperties, PdfDocumentInformation, or BackendHeuristic. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Extraction/DocumentSource.cs */
        comment designRef /* Design: docs/design/docdown-core/output.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/output.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/output.yaml (documented as a supporting type) */
    }

    part def OpcMetadataMapper {
        doc /* Maps a shared OPC core-property snapshot to DocumentMetadata once for every OPC backend, applying omit-empty, interesting-absence, provenance, and date normalization. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Extraction/OpcMetadataMapper.cs */
        comment testRef /* Test: test/DemaConsulting.DocDown.Core.Tests/Extraction/OpcMetadataMapperTests.cs */
        comment designRef /* Design: docs/design/docdown-core/output.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/output.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/output.yaml (documented as a supporting type) */
    }

    part def ExtractionResult {
        doc /* The full outcome returned to the caller: outcome, output paths, detected format, selected extractor, environment, and notes. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Extraction/ExtractionResult.cs */
        comment designRef /* Design: docs/design/docdown-core/extraction.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/extraction.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/extraction.yaml (documented as a supporting type) */
    }

    part def ISelfValidating {
        doc /* Opt-in interface by which a backend contributes self-test cases. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Extraction/IDocumentExtractor.cs */
        comment designRef /* Design: docs/design/docdown-core/extraction.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/extraction.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/extraction.yaml (documented as a supporting type) */
    }

    part def SelfTestCase {
        doc /* A named, categorized, runnable self-test case. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Extraction/SelfTestResult.cs */
        comment designRef /* Design: docs/design/docdown-core/extraction.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/extraction.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/extraction.yaml (documented as a supporting type) */
    }

    part def SelfTestContext {
        doc /* The working folder and cancellation token handed to a self-test. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Extraction/SelfTestResult.cs */
        comment designRef /* Design: docs/design/docdown-core/extraction.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/extraction.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/extraction.yaml (documented as a supporting type) */
    }

    part def SelfTestResult {
        doc /* The status, optional message, and duration of a self-test. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Extraction/SelfTestResult.cs */
        comment designRef /* Design: docs/design/docdown-core/extraction.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/extraction.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/extraction.yaml (documented as a supporting type) */
    }

    part def SelfTestStatus {
        doc /* The status of a self-test: Passed, Failed, or Skipped. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Extraction/SelfTestResult.cs */
        comment designRef /* Design: docs/design/docdown-core/extraction.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/extraction.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/extraction.yaml (documented as a supporting type) */
    }

    part def SelfTestProbe {
        doc /* Reads a backend's embedded self-test probe document from its assembly manifest. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Extraction/SelfTestProbe.cs */
        comment designRef /* Design: docs/design/docdown-core/extraction.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/extraction.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/extraction.yaml (documented as a supporting type) */
    }
}
