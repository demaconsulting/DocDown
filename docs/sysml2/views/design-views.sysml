// Views rendered into docs/design/generated/ for the Design document.
// Per SysML2Tools syntax, 'expose' is only valid inside a named 'view' usage
// (not a 'view def' definition), and it - not 'render' - is what scopes the
// rendered diagram content. Every view is typed by a standard view definition
// (sysml2tools 0.3.0-beta.3+): an untyped view has its notation inferred from its name,
// so a name ending in 'ActionView' (such as 'ExtractionView') was drawn as an
// action-flow diagram and rendered empty for a structural part def.
package DocDownDesignViews {
    private import StandardViewDefinitions::*;
    private import DocDownCore::*;
    private import DocDownOffice::*;
    private import DocDownPdf::*;
    private import DocDownPdfRendering::*;
    private import DocDownPowerPointRendering::*;
    private import DocDownVisioRendering::*;
    private import DocDownTool::*;
    private import OtsDependencies::*;

    // Several systems, so the overview exposes each system's part def: every system draws as a
    // frame with its direct subsystem usages nested inside. Documentation and comments are
    // filtered out so the overview stays readable.
    view SoftwareStructureView : InterconnectionView {
        expose DocDownCore::DocDownCoreSystem;
        expose DocDownOffice::DocDownOfficeSystem;
        expose DocDownPdf::DocDownPdfSystem;
        expose DocDownPdfRendering::DocDownPdfRenderingSystem;
        expose DocDownPowerPointRendering::DocDownPowerPointRenderingSystem;
        expose DocDownVisioRendering::DocDownVisioRenderingSystem;
        expose DocDownTool::DocDownToolSystem;
        filter not (@SysML::Documentation);
        filter not (@SysML::Comment);
    }

    // DocDownCore: system view plus a view per subsystem.
    view DocDownCoreView : InterconnectionView {
        expose DocDownCoreSystem::**;
    }

    view DetectionView : InterconnectionView {
        expose Detection::**;
    }

    view ExtractionView : InterconnectionView {
        expose Extraction::**;
    }

    view OutputView : InterconnectionView {
        expose Output::**;
    }

    // DocDownPdf is flat - it has no subsystems - so it needs only the one system view,
    // which already shows every unit.
    view DocDownPdfView : InterconnectionView {
        expose DocDownPdfSystem::**;
    }

    // DocDownPdfRendering is the optional page-rendering system. It is flat - it has no
    // subsystems - so it needs only the one system view, which already shows every unit.
    view DocDownPdfRenderingView : InterconnectionView {
        expose DocDownPdfRenderingSystem::**;
    }

    // DocDownPowerPointRendering is the optional slide-rendering system. It is flat - it has no
    // subsystems - so it needs only the one system view, which already shows every unit.
    view DocDownPowerPointRenderingView : InterconnectionView {
        expose DocDownPowerPointRenderingSystem::**;
    }

    // DocDownVisioRendering is the optional page-rendering system. It is flat - it has no
    // subsystems - so it needs only the one system view, which already shows every unit.
    view DocDownVisioRenderingView : InterconnectionView {
        expose DocDownVisioRenderingSystem::**;
    }

    // DocDownTool is the command-line tool system. It has two subsystems (Cli, SelfTest)
    // and one direct unit (Program), so it carries a system view plus a view per subsystem.
    view DocDownToolView : InterconnectionView {
        expose DocDownToolSystem::**;
    }

    view CliView : InterconnectionView {
        expose Cli::**;
    }

    view SelfTestView : InterconnectionView {
        expose SelfTest::**;
    }

    // DocDownOffice is the Microsoft Office extraction system. It has four format subsystems
    // (Word, Excel, PowerPoint, Visio) and one direct unit (OfficeDocDownBuilderExtensions), so it
    // carries a system view plus a view per subsystem. The design documents embed the system view
    // (DocDownOfficeView.svg).
    view DocDownOfficeView : InterconnectionView {
        expose DocDownOfficeSystem::**;
    }

    view WordView : InterconnectionView {
        expose Word::**;
    }

    view WordMarkdownView : InterconnectionView {
        expose WordMarkdown::**;
    }

    view WordOpenXmlView : InterconnectionView {
        expose WordOpenXml::**;
    }

    view ExcelView : InterconnectionView {
        expose Excel::**;
    }

    view ExcelMarkdownView : InterconnectionView {
        expose ExcelMarkdown::**;
    }

    view ExcelOpenXmlView : InterconnectionView {
        expose ExcelOpenXml::**;
    }

    view PowerPointView : InterconnectionView {
        expose PowerPoint::**;
    }

    view PowerPointMarkdownView : InterconnectionView {
        expose PowerPointMarkdown::**;
    }

    view PowerPointOpenXmlView : InterconnectionView {
        expose PowerPointOpenXml::**;
    }

    view VisioView : InterconnectionView {
        expose Visio::**;
    }

    view VisioMarkdownView : InterconnectionView {
        expose VisioMarkdown::**;
    }

    view VisioOpenXmlView : InterconnectionView {
        expose VisioOpenXml::**;
    }

    // The modeled OTS runtime dependencies (only HelpCards so far). Rendered with the other views
    // but not embedded in a design document, because one item would not represent the OTS set.
    view OtsView : GeneralView {
        expose OtsDependencies::*;
        filter not (@SysML::Documentation);
    }
}
