package DocDownOffice {
    private import SoftwareItemMetadata::*;

    part def ExcelDocDownBuilderExtensions {
        doc /* The registration seam: the one explicit, reflection-free call that adds the managed Open XML Excel backend to a DocDown builder. Free of any Open XML SDK type on its public surface. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Office/Excel/ExcelDocDownBuilderExtensions.cs");
            testFiles = ("test/DemaConsulting.DocDown.Office.Tests/Excel/ExcelDocDownBuilderExtensionsTests.cs");
            designFiles = ("docs/design/docdown-office/excel/excel-doc-down-builder-extensions.md");
            verificationFiles = ("docs/verification/docdown-office/excel/excel-doc-down-builder-extensions.md");
            requirementFiles = ("docs/reqstream/docdown-office/excel/excel-doc-down-builder-extensions.yaml");
        }
    }
}
