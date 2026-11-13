package DocDownOffice {
    private import SoftwareItemMetadata::*;

    doc
    /*
     * DemaConsulting.DocDown.Visio is the Visio extraction subsystem of DemaConsulting.DocDown.Office: a backend that reads a
     * drawing's page names, shape text, and — above all — the directed connector topology into the
     * DocDown output contract, plus its embedded images and metadata. Rendered page images are a
     * separate, opt-in concern provided by the fully-managed DemaConsulting.DocDown.Visio.Rendering
     * package. A Visio drawing is an engineering schematic whose meaning is the topology, so the page
     * names, shape text, and directed source-to-target edges are always extracted from the file
     * itself. It has two child subsystems (Markdown, OpenXml) and one direct unit
     * (VisioDocDownBuilderExtensions).
     */

    part def Visio {
        doc /* The DemaConsulting.DocDown.Visio subsystem as a whole. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.DocDown.Office.Tests/Visio/DocDownVisioTests.cs");
            designFiles = ("docs/design/docdown-office/visio.md");
            verificationFiles = ("docs/verification/docdown-office/visio.md");
            requirementFiles = ("docs/reqstream/docdown-office/visio.yaml");
        }

        part visioDocDownBuilderExtensions : VisioDocDownBuilderExtensions;
        part markdown : VisioMarkdown;
        part openXml : VisioOpenXml;
    }
}
