package DocDownOffice {
    private import SoftwareItemMetadata::*;

    doc
    /*
     * DemaConsulting.DocDown.Office is the Microsoft Office extraction package: a separately distributed backend that
     * reads Word documents, Excel workbooks, PowerPoint presentations, and Visio drawings into the
     * DocDown output contract. It has four format subsystems (Word, Excel, PowerPoint, Visio) and
     * one direct unit (OfficeDocDownBuilderExtensions).
     *
     * The four formats were four packages once. They share a dependency, a release cadence, and an
     * audience, so splitting them cost a consumer four references to keep in step and bought nothing.
     */

    part def DocDownOfficeSystem {
        doc /* The DemaConsulting.DocDown.Office system as a whole. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.DocDown.Office.Tests/OfficeDocDownBuilderExtensionsTests.cs");
            designFiles = ("docs/design/docdown-office.md");
            verificationFiles = ("docs/verification/docdown-office.md");
            requirementFiles = ("docs/reqstream/docdown-office.yaml");
        }

        part officeDocDownBuilderExtensions : OfficeDocDownBuilderExtensions;
        part word : Word;
        part excel : Excel;
        part powerPoint : PowerPoint;
        part visio : Visio;
    }

    part def OfficeDocDownBuilderExtensions {
        doc /* The reflection-free registration seam that adds every Office backend in one call. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Office/OfficeDocDownBuilderExtensions.cs");
            testFiles = ("test/DemaConsulting.DocDown.Office.Tests/OfficeDocDownBuilderExtensionsTests.cs");
            designFiles = ("docs/design/docdown-office.md");
            verificationFiles = ("docs/verification/docdown-office.md");
            requirementFiles = ("docs/reqstream/docdown-office.yaml");
        }
    }
}
