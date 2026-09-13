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

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Extraction/IExtractionContext.cs */
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
        doc /* Whether a backend is usable now, an optional reason, and its effective capabilities. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Extraction/ExtractorAvailability.cs */
        comment designRef /* Design: docs/design/docdown-core/extraction.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/extraction.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/extraction.yaml (documented as a supporting type) */
    }

    part def ExtractorCapabilities {
        doc /* The capabilities a backend can provide, as a flags enumeration. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Extraction/ExtractorCapabilities.cs */
        comment designRef /* Design: docs/design/docdown-core/extraction.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/extraction.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/extraction.yaml (documented as a supporting type) */
    }

    part def ExtractorDescriptor {
        doc /* An immutable snapshot of a backend's identity and declared abilities, detached from the live instance. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Extraction/ExtractorDescriptor.cs */
        comment designRef /* Design: docs/design/docdown-core/extraction.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/extraction.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/extraction.yaml (documented as a supporting type) */
    }

    part def ExtractorCandidate {
        doc /* A descriptor paired with its probed availability; the unit of input to selection. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Extraction/ExtractorCandidate.cs */
        comment designRef /* Design: docs/design/docdown-core/extraction.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/extraction.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/extraction.yaml (documented as a supporting type) */
    }

    part def BackendStatus {
        doc /* A flattened backend status for reporting via GetBackendStatus. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Extraction/BackendStatus.cs */
        comment designRef /* Design: docs/design/docdown-core/extraction.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/extraction.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/extraction.yaml (documented as a supporting type) */
    }

    part def SelectionMode {
        doc /* Whether selection ran automatically or under a caller override. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Extraction/SelectionMode.cs */
        comment designRef /* Design: docs/design/docdown-core/extraction.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/extraction.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/extraction.yaml (documented as a supporting type) */
    }

    part def SelectionResult {
        doc /* The selection outcome: the selected descriptor, mode, capabilities, trace, and any failure. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Extraction/SelectionResult.cs */
        comment designRef /* Design: docs/design/docdown-core/extraction.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/extraction.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/extraction.yaml (documented as a supporting type) */
    }

    part def CandidateOutcome {
        doc /* The per-candidate classification produced by selection. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Extraction/CandidateOutcome.cs */
        comment designRef /* Design: docs/design/docdown-core/extraction.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/extraction.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/extraction.yaml (documented as a supporting type) */
    }

    part def CandidateVerdict {
        doc /* One candidate's identifier, priority, outcome, and a human-readable detail. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Extraction/CandidateVerdict.cs */
        comment designRef /* Design: docs/design/docdown-core/extraction.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/extraction.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/extraction.yaml (documented as a supporting type) */
    }

    part def ExtractionOutcome {
        doc /* Whether an extraction succeeded, degraded, or failed. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Extraction/ExtractionOutcome.cs */
        comment designRef /* Design: docs/design/docdown-core/extraction.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/extraction.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/extraction.yaml (documented as a supporting type) */
    }

    part def ExtractionFailureKind {
        doc /* Enumerates the failure kinds an extraction can report. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Extraction/ExtractionFailureKind.cs */
        comment designRef /* Design: docs/design/docdown-core/extraction.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/extraction.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/extraction.yaml (documented as a supporting type) */
    }

    part def ExtractionFailure {
        doc /* A structured failure: kind, code, summary, displayable explanation, candidates, and optional remedy. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Extraction/ExtractionFailure.cs */
        comment designRef /* Design: docs/design/docdown-core/extraction.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/extraction.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/extraction.yaml (documented as a supporting type) */
    }

    part def ExtractionDiagnostic {
        doc /* A coded, severity-tagged, optionally-located diagnostic message. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Extraction/ExtractionDiagnostic.cs */
        comment designRef /* Design: docs/design/docdown-core/extraction.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/extraction.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/extraction.yaml (documented as a supporting type) */
    }

    part def DiagnosticSeverity {
        doc /* The severity of a diagnostic: Info, Warning, or Error. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Extraction/DiagnosticSeverity.cs */
        comment designRef /* Design: docs/design/docdown-core/extraction.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/extraction.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/extraction.yaml (documented as a supporting type) */
    }

    part def DiagnosticCodes {
        doc /* The Core diagnostic-code constants used across the pipeline. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Extraction/DiagnosticCodes.cs */
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
        doc /* How the scratch folder is prepared: RequireEmpty, CleanIfDocDownFolder, Overwrite, or CreateUnique. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Extraction/ScratchFolderMode.cs */
        comment designRef /* Design: docs/design/docdown-core/extraction.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/extraction.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/extraction.yaml (documented as a supporting type) */
    }

    part def ContentSplitMode {
        doc /* How extracted content is split across output files: Auto, Single, or PerPart. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Extraction/ContentSplitMode.cs */
        comment designRef /* Design: docs/design/docdown-core/extraction.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/extraction.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/extraction.yaml (documented as a supporting type) */
    }

    part def ImageOutputMode {
        doc /* How extracted images are encoded on output: Preserve or ForcePng. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Extraction/ImageOutputMode.cs */
        comment designRef /* Design: docs/design/docdown-core/extraction.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/extraction.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/extraction.yaml (documented as a supporting type) */
    }

    part def PageRange {
        doc /* An inclusive first-last page range with containment and count helpers. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Extraction/PageRange.cs */
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

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Extraction/DocumentInfo.cs */
        comment designRef /* Design: docs/design/docdown-core/extraction.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/extraction.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/extraction.yaml (documented as a supporting type) */
    }

    part def DocumentMetadata {
        doc /* What a document asserts about itself: populated self-reported fields with per-field provenance, plus the interesting fields left blank. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Extraction/DocumentMetadata.cs */
        comment designRef /* Design: docs/design/docdown-core/output.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/output.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/output.yaml (documented as a supporting type) */
    }

    part def MetadataProvenance {
        doc /* The origin of a self-reported metadata value: OpcCoreProperties, PdfDocumentInformation, or BackendHeuristic. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Extraction/DocumentMetadata.cs */
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
        doc /* The full outcome returned to the caller: outcome, output paths, selection, diagnostics, gaps, and ledger. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Extraction/ExtractionResult.cs */
        comment designRef /* Design: docs/design/docdown-core/extraction.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/extraction.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/extraction.yaml (documented as a supporting type) */
    }

    part def ISelfValidating {
        doc /* Opt-in interface by which a backend contributes self-test cases. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Extraction/ISelfValidating.cs */
        comment designRef /* Design: docs/design/docdown-core/extraction.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/extraction.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/extraction.yaml (documented as a supporting type) */
    }

    part def SelfTestCase {
        doc /* A named, categorized, runnable self-test case. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Extraction/SelfTestCase.cs */
        comment designRef /* Design: docs/design/docdown-core/extraction.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/extraction.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/extraction.yaml (documented as a supporting type) */
    }

    part def SelfTestContext {
        doc /* The working folder and cancellation token handed to a self-test. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Extraction/SelfTestContext.cs */
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

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Extraction/SelfTestStatus.cs */
        comment designRef /* Design: docs/design/docdown-core/extraction.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/extraction.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/extraction.yaml (documented as a supporting type) */
    }
}
