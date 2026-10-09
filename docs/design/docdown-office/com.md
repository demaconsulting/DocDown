## Com Subsystem

![DemaConsulting.DocDown.Office Com Structure](OfficeComView.svg)

### Overview

The Com subsystem holds what Visio's COM automation backend uses. It was shared with PowerPoint's
own COM backend until that backend was removed in favor of a fully-managed rendering package; these
types remain because Visio's COM backend still probes whether its application can be reached,
delegates content extraction to its managed counterpart, and reconciles that delegate's output with
the rendering it then performs.

### Interfaces

Every type here is internal. The subsystem's consumer is the Visio COM extractor in its own format
subsystem; nothing outside `DemaConsulting.DocDown.Office` can reach these types.

### Design

Three types, none of which touches a COM interface itself.

- **OfficeComAvailability** — answers whether a named Office application's automation can run here.
  It reads the operating system and, on Windows, whether a ProgID resolves. It launches nothing.
- **ComposingDelegatedSink** — forwards every write a delegated managed run makes, suppressing one
  specific environment fact.
- **DelegatedExtractionContext** — the context a COM backend hands its managed counterpart: page
  rendering switched off, output routed through the composing sink.

The COM calls themselves live in Visio's own `Com` subsystem, in its automation adapter and dispatch
helper. Those are not shared, because the COM boundary is driven through `Visio.InvisibleApp` with
its own process name and value converter, and collapsing a single-backend concern into this shared
subsystem would add complexity these types do not need.

#### Why the delegate's honesty needs composing

A managed backend, asked to extract on its own terms, states plainly that it does not render pages.
That statement is true of the managed backend and false of the run the reader is looking at, because
the COM backend rendered the pages sitting beside it in the same output folder.

`ComposingDelegatedSink` suppresses exactly that one fact, and only when it is reported unavailable.
Every other fact, note, image, and content part passes through untouched. The fact's key is supplied
by the caller rather than hard-coded, because the Visio backend reports `visio.pageRendering`.

#### Risk control measures

- **The probe never launches an application.** The engine probes every registered backend before
  selecting one, so a probe that started Microsoft Office would do so on every extraction, including
  on machines that cannot run it.
- **The probe never throws.** Any fault while reading the registry is reported as unavailable, which
  is what Core's availability contract requires.
- **Reasons state facts, not instructions.** An unavailable reason says the application is not
  present; it does not tell the reader to install it. The reader of a summary is usually an agent
  that cannot install anything, and a reason it cannot act on should not be phrased as an action.
- **Suppression is narrow.** One key, and only when unavailable. A broader filter could hide a fact
  the reader needs.

#### Design constraints

- The subsystem adds no native asset and no interop assembly. COM is reached through late-bound
  `IDispatch`, so the package stays runtime-identifier agnostic even though these backends only
  function on Windows.
- The availability probe is cheap enough to run on every extraction, because it does.
