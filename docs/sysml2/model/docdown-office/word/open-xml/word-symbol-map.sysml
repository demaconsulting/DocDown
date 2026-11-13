package DocDownOffice {
    private import SoftwareItemMetadata::*;

    part def WordSymbolMap {
        doc /* Translates a Word symbol character (font and font code point) to verified Unicode text, resolving anything it cannot confirm to the U+FFFD placeholder so the reader can count and report it. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Office/Word/OpenXml/WordSymbolMap.cs");
            testFiles = ("test/DemaConsulting.DocDown.Office.Tests/Word/OpenXml/WordSymbolMapTests.cs");
            designFiles = ("docs/design/docdown-office/word/open-xml/word-symbol-map.md");
            verificationFiles = ("docs/verification/docdown-office/word/open-xml/word-symbol-map.md");
            requirementFiles = ("docs/reqstream/docdown-office/word/open-xml/word-symbol-map.yaml");
        }
    }
}
