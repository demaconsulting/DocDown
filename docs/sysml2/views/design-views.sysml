// Views rendered into docs/design/generated/ for the Design document.
// Reopens each system's package so every 'expose' statement can reference
// its target part def directly. Per SysML2Tools syntax, 'expose' is only valid
// inside a named 'view' usage (not a 'view def' definition), and it - not
// 'render' - is what scopes the rendered diagram content. Every view is typed by a
// standard view definition (sysml2tools 0.3.0-beta.3+): an untyped view has its notation
// inferred from its name, so a name ending in 'ActionView' (such as 'ExtractionView') was
// drawn as an action-flow diagram and rendered empty for a structural part def.
package DocDownCore {
    private import StandardViewDefinitions::*;

    // Several systems, so a general view draws one package frame per system. Documentation and the
    // artifact-location comments are filtered out so the overview stays readable.
    view SoftwareStructureView : GeneralView {
        expose DocDownCore::**;
        expose DocDownOffice::**;
        expose DocDownPdf::**;
        expose DocDownPdfRendering::**;
        expose DocDownPowerPointRendering::**;
        expose DocDownVisioRendering::**;
        expose DocDownTool::**;
        filter not @SysML::Documentation;
        filter not @SysML::Comment;
    }

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
}

// The repository now holds more than one system, so the full-detail structure view
// above is joined by a per-system view here. DocDownPdf is flat - it has no
// subsystems - so it needs only the one system view, which already shows every unit.
package DocDownPdf {
    private import StandardViewDefinitions::*;

    view DocDownPdfView : InterconnectionView {
        expose DocDownPdfSystem::**;
    }
}

// DocDownPdfRendering is the optional page-rendering system. It is flat - it has no
// subsystems - so it needs only the one system view, which already shows every unit.
package DocDownPdfRendering {
    private import StandardViewDefinitions::*;

    view DocDownPdfRenderingView : InterconnectionView {
        expose DocDownPdfRenderingSystem::**;
    }
}

// DocDownPowerPointRendering is the optional slide-rendering system. It is flat - it has no
// subsystems - so it needs only the one system view, which already shows every unit.
package DocDownPowerPointRendering {
    private import StandardViewDefinitions::*;

    view DocDownPowerPointRenderingView : InterconnectionView {
        expose DocDownPowerPointRenderingSystem::**;
    }
}

// DocDownVisioRendering is the optional page-rendering system. It is flat - it has no
// subsystems - so it needs only the one system view, which already shows every unit.
package DocDownVisioRendering {
    private import StandardViewDefinitions::*;

    view DocDownVisioRenderingView : InterconnectionView {
        expose DocDownVisioRenderingSystem::**;
    }
}

// DocDownTool is the command-line tool system. It has two subsystems
// (Cli, SelfTest) and one direct unit (Program), so it carries a system view plus a view
// per subsystem.
package DocDownTool {
    private import StandardViewDefinitions::*;

    view DocDownToolView : InterconnectionView {
        expose DocDownToolSystem::**;
    }

    view CliView : InterconnectionView {
        expose Cli::**;
    }

    view SelfTestView : InterconnectionView {
        expose SelfTest::**;
    }
}

// DocDownOffice is the Microsoft Office extraction system. It has four format subsystems
// (Word, Excel, PowerPoint, Visio) and one direct unit (OfficeDocDownBuilderExtensions), so it
// carries a system view plus a view per subsystem. The design documents embed the system view
// (DocDownOfficeView.svg).
package DocDownOffice {
    private import StandardViewDefinitions::*;

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
}
