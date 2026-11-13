package DocDownCore {
    private import SoftwareItemMetadata::*;

    part def Detection {
        doc /* Identifies a document's format from its file name, falling back to leading content bytes, and reports how it reached its conclusion. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.DocDown.Core.Tests/Detection/DetectionTests.cs");
            designFiles = ("docs/design/docdown-core/detection.md");
            verificationFiles = ("docs/verification/docdown-core/detection.md");
            requirementFiles = ("docs/reqstream/docdown-core/detection.yaml");
        }

        part formatSniffer : FormatSniffer;
    }

    part def DocumentFormat {
        doc /* Identifies a format by a short stable Id and its IANA media type (documented as a supporting type). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Core/Detection/DocumentFormat.cs");
            designFiles = ("docs/design/docdown-core/detection.md");
            verificationFiles = ("docs/verification/docdown-core/detection.md");
            requirementFiles = ("docs/reqstream/docdown-core/detection.yaml");
        }
    }

    part def DetectionBasis {
        doc /* Enumerates the kind of evidence behind a detection so consumers can weigh its reliability (documented as a supporting type). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Core/Detection/FormatSniffer.cs");
            designFiles = ("docs/design/docdown-core/detection.md");
            verificationFiles = ("docs/verification/docdown-core/detection.md");
            requirementFiles = ("docs/reqstream/docdown-core/detection.yaml");
        }
    }

    part def FormatDetection {
        doc /* Pairs the identified format with the detection basis behind it (documented as a supporting type). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Core/Detection/FormatSniffer.cs");
            designFiles = ("docs/design/docdown-core/detection.md");
            verificationFiles = ("docs/verification/docdown-core/detection.md");
            requirementFiles = ("docs/reqstream/docdown-core/detection.yaml");
        }
    }
}
