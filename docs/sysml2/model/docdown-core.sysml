package DocDownCore {
    doc
    /*
     * DocDown.Core is a .NET library providing the core functionality for the DocDown
     * family of document-to-markdown extraction libraries.
     */

    part def DocDownCoreSystem {
        doc /* The DocDown.Core system as a whole. */

        comment testRef /* Test: test/DemaConsulting.DocDown.Core.Tests/DocDownCoreTests.cs */
        comment designRef /* Design: docs/design/docdown-core.md */
        comment verificationRef /* Verification: docs/verification/docdown-core.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-core.yaml */

        part detection : Detection;
        part extraction : Extraction;
        part output : Output;
    }
}
