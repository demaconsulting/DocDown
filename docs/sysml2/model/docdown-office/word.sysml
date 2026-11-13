package DocDownOffice {
    private import SoftwareItemMetadata::*;

    doc
    /*
     * DemaConsulting.DocDown.Word is the Word extraction subsystem of DemaConsulting.DocDown.Office: a backend that reads a
     * Word document's text, real tables, embedded images, document control, and metadata into the
     * DocDown output contract. It has two child subsystems (Markdown, OpenXml) and one direct
     * unit (WordDocDownBuilderExtensions).
     */

    part def Word {
        doc /* The DemaConsulting.DocDown.Word subsystem as a whole. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.DocDown.Office.Tests/Word/DocDownWordTests.cs");
            designFiles = ("docs/design/docdown-office/word.md");
            verificationFiles = ("docs/verification/docdown-office/word.md");
            requirementFiles = ("docs/reqstream/docdown-office/word.yaml");
        }

        part wordDocDownBuilderExtensions : WordDocDownBuilderExtensions;
        part markdown : WordMarkdown;
        part openXml : WordOpenXml;
    }
}
