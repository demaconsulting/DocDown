package DocDownCore {
    private import SoftwareItemMetadata::*;

    part def Extraction {
        doc /* Owns the backend model and pipeline: registers extractors, selects the best available backend, and orchestrates one extraction. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.DocDown.Core.Tests/Extraction/ExtractionTests.cs");
            designFiles = ("docs/design/docdown-core/extraction.md");
            verificationFiles = ("docs/verification/docdown-core/extraction.md");
            requirementFiles = ("docs/reqstream/docdown-core/extraction.yaml");
        }

        part docDownBuilder : DocDownBuilder;
        part extractorRegistry : ExtractorRegistry;
        part extractorSelector : ExtractorSelector;
        part docDownEngine : DocDownEngine;
    }

    part def IDocumentExtractor {
        doc /* The backend contract implemented by external extractor packages (documented as a supporting type). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Core/Extraction/IDocumentExtractor.cs");
            designFiles = ("docs/design/docdown-core/extraction.md");
            verificationFiles = ("docs/verification/docdown-core/extraction.md");
            requirementFiles = ("docs/reqstream/docdown-core/extraction.yaml");
        }
    }

    part def IExtractionContext {
        doc /* What a backend sees during extraction: options, sink, detected format, selected extractor, and environment - but no output path (documented as a supporting type). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Core/Extraction/ExtractionContext.cs");
            designFiles = ("docs/design/docdown-core/extraction.md");
            verificationFiles = ("docs/verification/docdown-core/extraction.md");
            requirementFiles = ("docs/reqstream/docdown-core/extraction.yaml");
        }
    }

    part def ExtractionContext {
        doc /* The concrete IExtractionContext the engine constructs per run (documented as a supporting type). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Core/Extraction/ExtractionContext.cs");
            designFiles = ("docs/design/docdown-core/extraction.md");
            verificationFiles = ("docs/verification/docdown-core/extraction.md");
            requirementFiles = ("docs/reqstream/docdown-core/extraction.yaml");
        }
    }

    part def ExtractorAvailability {
        doc /* Whether a backend is usable now, an optional reason, and whether it can render pages here (documented as a supporting type). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Core/Extraction/IDocumentExtractor.cs");
            designFiles = ("docs/design/docdown-core/extraction.md");
            verificationFiles = ("docs/verification/docdown-core/extraction.md");
            requirementFiles = ("docs/reqstream/docdown-core/extraction.yaml");
        }
    }

    part def ExtractorDescriptor {
        doc /* An immutable snapshot of a backend's identity and supported formats, detached from the live instance (documented as a supporting type). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Core/Extraction/IDocumentExtractor.cs");
            designFiles = ("docs/design/docdown-core/extraction.md");
            verificationFiles = ("docs/verification/docdown-core/extraction.md");
            requirementFiles = ("docs/reqstream/docdown-core/extraction.yaml");
        }
    }

    part def ExtractorCandidate {
        doc /* A descriptor paired with its probed availability; the unit of input to selection (documented as a supporting type). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Core/Extraction/ExtractorSelector.cs");
            designFiles = ("docs/design/docdown-core/extraction.md");
            verificationFiles = ("docs/verification/docdown-core/extraction.md");
            requirementFiles = ("docs/reqstream/docdown-core/extraction.yaml");
        }
    }

    part def ExtractionOutcome {
        doc /* Whether an extraction produced the invariant output layout or the document was unreadable (documented as a supporting type). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Core/Extraction/ExtractionResult.cs");
            designFiles = ("docs/design/docdown-core/extraction.md");
            verificationFiles = ("docs/verification/docdown-core/extraction.md");
            requirementFiles = ("docs/reqstream/docdown-core/extraction.yaml");
        }
    }

    part def ExtractionFailure {
        doc /* A plain-language failure: a one-line summary and a multi-line displayable explanation (documented as a supporting type). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Core/Extraction/ExtractionResult.cs");
            designFiles = ("docs/design/docdown-core/extraction.md");
            verificationFiles = ("docs/verification/docdown-core/extraction.md");
            requirementFiles = ("docs/reqstream/docdown-core/extraction.yaml");
        }
    }

    part def ExtractionOptions {
        doc /* The request configuration for one extraction: page rendering, image inclusion, scratch mode, and preferences (documented as a supporting type). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Core/Extraction/ExtractionOptions.cs");
            designFiles = ("docs/design/docdown-core/extraction.md");
            verificationFiles = ("docs/verification/docdown-core/extraction.md");
            requirementFiles = ("docs/reqstream/docdown-core/extraction.yaml");
        }
    }

    part def ScratchFolderMode {
        doc /* How the scratch folder is prepared: CleanIfDocDownFolder or Overwrite (documented as a supporting type). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Core/Extraction/ExtractionOptions.cs");
            designFiles = ("docs/design/docdown-core/extraction.md");
            verificationFiles = ("docs/verification/docdown-core/extraction.md");
            requirementFiles = ("docs/reqstream/docdown-core/extraction.yaml");
        }
    }

    part def PageRange {
        doc /* An inclusive first-last page range with containment and count helpers (documented as a supporting type). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Core/Extraction/ExtractionOptions.cs");
            designFiles = ("docs/design/docdown-core/extraction.md");
            verificationFiles = ("docs/verification/docdown-core/extraction.md");
            requirementFiles = ("docs/reqstream/docdown-core/extraction.yaml");
        }
    }

    part def DocumentSource {
        doc /* A file- or stream-backed document input with its metadata and an OpenRead accessor (documented as a supporting type). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Core/Extraction/DocumentSource.cs");
            designFiles = ("docs/design/docdown-core/extraction.md");
            verificationFiles = ("docs/verification/docdown-core/extraction.md");
            requirementFiles = ("docs/reqstream/docdown-core/extraction.yaml");
        }
    }

    part def DocumentInfo {
        doc /* Extractor-reported metadata: optional title, author, page count, and part count (documented as a supporting type). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Core/Extraction/DocumentSource.cs");
            designFiles = ("docs/design/docdown-core/extraction.md");
            verificationFiles = ("docs/verification/docdown-core/extraction.md");
            requirementFiles = ("docs/reqstream/docdown-core/extraction.yaml");
        }
    }

    part def DocumentMetadata {
        doc /* What a document asserts about itself: populated self-reported fields with per-field provenance, plus the interesting fields left blank (documented as a supporting type). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Core/Extraction/DocumentSource.cs");
            designFiles = ("docs/design/docdown-core/output.md");
            verificationFiles = ("docs/verification/docdown-core/output.md");
            requirementFiles = ("docs/reqstream/docdown-core/output.yaml");
        }
    }

    part def MetadataProvenance {
        doc /* The origin of a self-reported metadata value: OpcCoreProperties, PdfDocumentInformation, or BackendHeuristic (documented as a supporting type). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Core/Extraction/DocumentSource.cs");
            designFiles = ("docs/design/docdown-core/output.md");
            verificationFiles = ("docs/verification/docdown-core/output.md");
            requirementFiles = ("docs/reqstream/docdown-core/output.yaml");
        }
    }

    part def OpcMetadataMapper {
        doc /* Maps a shared OPC core-property snapshot to DocumentMetadata once for every OPC backend, applying omit-empty, interesting-absence, provenance, and date normalization (documented as a supporting type). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Core/Extraction/OpcMetadataMapper.cs");
            testFiles = ("test/DemaConsulting.DocDown.Core.Tests/Extraction/OpcMetadataMapperTests.cs");
            designFiles = ("docs/design/docdown-core/output.md");
            verificationFiles = ("docs/verification/docdown-core/output.md");
            requirementFiles = ("docs/reqstream/docdown-core/output.yaml");
        }
    }

    part def ExtractionResult {
        doc /* The full outcome returned to the caller: outcome, output paths, detected format, selected extractor, environment, and notes (documented as a supporting type). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Core/Extraction/ExtractionResult.cs");
            designFiles = ("docs/design/docdown-core/extraction.md");
            verificationFiles = ("docs/verification/docdown-core/extraction.md");
            requirementFiles = ("docs/reqstream/docdown-core/extraction.yaml");
        }
    }

    part def ISelfValidating {
        doc /* Opt-in interface by which a backend contributes self-test cases (documented as a supporting type). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Core/Extraction/IDocumentExtractor.cs");
            designFiles = ("docs/design/docdown-core/extraction.md");
            verificationFiles = ("docs/verification/docdown-core/extraction.md");
            requirementFiles = ("docs/reqstream/docdown-core/extraction.yaml");
        }
    }

    part def SelfTestCase {
        doc /* A named, categorized, runnable self-test case (documented as a supporting type). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Core/Extraction/SelfTestResult.cs");
            designFiles = ("docs/design/docdown-core/extraction.md");
            verificationFiles = ("docs/verification/docdown-core/extraction.md");
            requirementFiles = ("docs/reqstream/docdown-core/extraction.yaml");
        }
    }

    part def SelfTestContext {
        doc /* The working folder and cancellation token handed to a self-test (documented as a supporting type). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Core/Extraction/SelfTestResult.cs");
            designFiles = ("docs/design/docdown-core/extraction.md");
            verificationFiles = ("docs/verification/docdown-core/extraction.md");
            requirementFiles = ("docs/reqstream/docdown-core/extraction.yaml");
        }
    }

    part def SelfTestResult {
        doc /* The status, optional message, and duration of a self-test (documented as a supporting type). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Core/Extraction/SelfTestResult.cs");
            designFiles = ("docs/design/docdown-core/extraction.md");
            verificationFiles = ("docs/verification/docdown-core/extraction.md");
            requirementFiles = ("docs/reqstream/docdown-core/extraction.yaml");
        }
    }

    part def SelfTestStatus {
        doc /* The status of a self-test: Passed, Failed, or Skipped (documented as a supporting type). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Core/Extraction/SelfTestResult.cs");
            designFiles = ("docs/design/docdown-core/extraction.md");
            verificationFiles = ("docs/verification/docdown-core/extraction.md");
            requirementFiles = ("docs/reqstream/docdown-core/extraction.yaml");
        }
    }

    part def SelfTestProbe {
        doc /* Reads a backend's embedded self-test probe document from its assembly manifest (documented as a supporting type). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Core/Extraction/SelfTestProbe.cs");
            designFiles = ("docs/design/docdown-core/extraction.md");
            verificationFiles = ("docs/verification/docdown-core/extraction.md");
            requirementFiles = ("docs/reqstream/docdown-core/extraction.yaml");
        }
    }
}
