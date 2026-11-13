package DocDownOffice {
    private import SoftwareItemMetadata::*;

    part def ExcelContentEmitter {
        doc /* The single emission path: writes one Sheet content part per worksheet and one Chart part per chart, the verbatim listing and the additive grid table with its elision and merged-range notes, the shape annotations, the inline image links, the content inventory including the comment and comment-author counts, the cell comments reported to Core as SheetName!CellRef review comments, and the notes the model implies. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Office/Excel/Markdown/ExcelContentEmitter.cs");
            testFiles = ("test/DemaConsulting.DocDown.Office.Tests/Excel/Markdown/ExcelContentEmitterTests.cs");
            designFiles = ("docs/design/docdown-office/excel/markdown/excel-content-emitter.md");
            verificationFiles = ("docs/verification/docdown-office/excel/markdown/excel-content-emitter.md");
            requirementFiles = ("docs/reqstream/docdown-office/excel/markdown/excel-content-emitter.yaml");
        }
    }
}
