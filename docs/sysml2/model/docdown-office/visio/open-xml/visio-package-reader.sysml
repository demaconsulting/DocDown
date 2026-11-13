package DocDownOffice {
    private import SoftwareItemMetadata::*;

    part def VisioPackageReader {
        doc /* Reads a Visio drawing's Open Packaging container directly with System.IO.Packaging — because DocumentFormat.OpenXml has zero Visio types — into the backend-neutral model: each page's name, the text of every shape that carries text, the master each shape was instantiated from (classifying a text-less shape by its type), a symbol-font glyph recovered only within a run whose font declares it, and the directed connections resolved from the connector begin and end records, scoped to the page that owns them. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Office/Visio/OpenXml/VisioPackageReader.cs");
            testFiles = ("test/DemaConsulting.DocDown.Office.Tests/Visio/OpenXml/VisioPackageReaderTests.cs");
            designFiles = ("docs/design/docdown-office/visio/open-xml/visio-package-reader.md");
            verificationFiles = ("docs/verification/docdown-office/visio/open-xml/visio-package-reader.md");
            requirementFiles = ("docs/reqstream/docdown-office/visio/open-xml/visio-package-reader.yaml");
        }
    }
}
