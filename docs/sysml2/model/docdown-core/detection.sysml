package DocDownCore {
    part def Detection {
        doc /* Identifies a document's format from its file name, falling back to leading content bytes, and reports how it reached its conclusion. */

        comment testRef /* Test: test/DemaConsulting.DocDown.Core.Tests/Detection/DetectionTests.cs */
        comment designRef /* Design: docs/design/docdown-core/detection.md */
        comment verificationRef /* Verification: docs/verification/docdown-core/detection.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/detection.yaml */

        part formatSniffer : FormatSniffer;
    }

    part def DocumentFormat {
        doc /* Identifies a format by a short stable Id and its IANA media type. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Detection/DocumentFormat.cs */
        comment designRef /* Design: docs/design/docdown-core/detection.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/detection.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/detection.yaml (documented as a supporting type) */
    }

    part def DetectionBasis {
        doc /* Enumerates the kind of evidence behind a detection so consumers can weigh its reliability. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Detection/FormatSniffer.cs */
        comment designRef /* Design: docs/design/docdown-core/detection.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/detection.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/detection.yaml (documented as a supporting type) */
    }

    part def FormatDetection {
        doc /* Pairs the identified format with its detection basis and a confidence score. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Core/Detection/FormatSniffer.cs */
        comment designRef /* Design: docs/design/docdown-core/detection.md (documented as a supporting type) */
        comment verificationRef /* Verification: docs/verification/docdown-core/detection.md (documented as a supporting type) */
        comment reqRef /* Requirements: docs/reqstream/docdown-core/detection.yaml (documented as a supporting type) */
    }
}
