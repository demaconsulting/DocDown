package DocDownOffice {
    private import SoftwareItemMetadata::*;

    doc
    /*
     * DemaConsulting.DocDown.Excel is the Excel extraction subsystem of DemaConsulting.DocDown.Office: a backend that reads a
     * workbook's worksheet cell values verbatim, the formulas behind computed cells, the charts each
     * worksheet shows, the annotations drawn over it, and its embedded images and metadata into the
     * DocDown output contract. A workbook is a prose and data container, not a picture, so the subsystem
     * deliberately never renders. It has two child subsystems (Markdown, OpenXml) and one direct unit
     * (ExcelDocDownBuilderExtensions).
     */

    part def Excel {
        doc /* The DemaConsulting.DocDown.Excel subsystem as a whole. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.DocDown.Office.Tests/Excel/DocDownExcelTests.cs");
            designFiles = ("docs/design/docdown-office/excel.md");
            verificationFiles = ("docs/verification/docdown-office/excel.md");
            requirementFiles = ("docs/reqstream/docdown-office/excel.yaml");
        }

        part excelDocDownBuilderExtensions : ExcelDocDownBuilderExtensions;
        part markdown : ExcelMarkdown;
        part openXml : ExcelOpenXml;
    }
}
