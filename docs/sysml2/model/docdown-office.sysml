package DocDownOffice {
    doc
    /*
     * DocDown.Office is the Microsoft Office extraction package: a separately distributed backend that
     * reads Word documents, Excel workbooks, PowerPoint presentations, and Visio drawings into the
     * DocDown output contract. It has five subsystems — four format subsystems (Word, Excel,
     * PowerPoint, Visio) and Com, which holds the availability probe and composition helpers the COM
     * automation backends share — and one direct unit (OfficeDocDownBuilderExtensions).
     *
     * The four formats were four packages once. They share a dependency, a release cadence, and an
     * audience, so splitting them cost a consumer four references to keep in step and bought nothing.
     */

    part def DocDownOfficeSystem {
        doc /* The DocDown.Office system as a whole. */

        comment testRef /* Test: test/DemaConsulting.DocDown.Office.Tests/OfficeDocDownBuilderExtensionsTests.cs */
        comment designRef /* Design: docs/design/docdown-office.md */
        comment verificationRef /* Verification: docs/verification/docdown-office.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-office.yaml */

        part officeDocDownBuilderExtensions : OfficeDocDownBuilderExtensions;
        part com : OfficeCom;
        part word : Word;
        part excel : Excel;
        part powerPoint : PowerPoint;
        part visio : Visio;
    }

    part def OfficeDocDownBuilderExtensions {
        doc /* The reflection-free registration seam that adds every Office backend in one call. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Office/OfficeDocDownBuilderExtensions.cs */
        comment testRef /* Test: test/DemaConsulting.DocDown.Office.Tests/OfficeDocDownBuilderExtensionsTests.cs */
        comment designRef /* Design: docs/design/docdown-office.md */
        comment verificationRef /* Verification: docs/verification/docdown-office.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-office.yaml */
    }
}
