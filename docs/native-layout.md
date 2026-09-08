# Native layout integration research

**Investigated, not implemented or operationally accredited.** Explicit
geometry edits and same-diagram reparenting do not establish automatic routing,
alignment, distribution or global diagram layout. This note records the next
integration route, not a completed capability or a reduction of the full goal.

## Installed 4.3.0.008 responsibility map

Read-only inspection of the installed assemblies and frontend assets identified:

| Component | Observed responsibility | Integration consequence |
| --- | --- | --- |
| `DiagramEditorHandleManager.OnAlignShapes` | Delegates to an editor presenter | Does not calculate or persist layout itself |
| `DiagramEditorPresenter.AlignSelectedShapes` | Generates a script command for the current editor | Requires the editor frontend, not just native persistence objects |
| `window.alignSelectedShapes` | Calls the frontend layout handler | Programmatic entry point; no simulated mouse input is needed to invoke it |
| Frontend alignment/distribution services | Calculate selected-shape deltas and invoke native frontend modeling commands | Reuse these services before implementing replacement geometry formulas |
| Frontend `elements.align` command | Moves shapes through its modeling pipeline | Connected paths and other dependent geometry must be observed, not guessed |
| `DiagramEditorElementsHandler.UpdateElementShape` | Receives serialized frontend changes and delegates to the presenter | A frontend result is not yet durable native state |
| `DiagramEditorPresenter.UpdateElementShape` | Deserializes changes, builds native commands and dispatches them through `ElementManager` | The actual command path, thread affinity and notifications need an isolated adapter |
| `ElementEditorHandler.GetUpdateCommands` | Produces collection commands using `BaseEditorElement` observations and `DiagramEditorPresentationModel` | A potential native translation boundary; construction and lifetime remain unverified |
| `RoutingFixerService.GetElementFixingArgs` | Maps existing frontend waypoints into native connector event arguments | It is not an automatic routing algorithm |
| `BrowserImage` | Hosts the installed image-generator page in CefSharp OffScreen | Existing rendering support is not an editable frontend integration |

The inspected legacy `DirectionalRouter` accepts a Crainiate line. Its existence
does not prove that using it independently reproduces the current editor's
modeling, docking and connection behavior. `FrmModeler.OnResetLayout` concerns
application bars, not automatic placement of BPMN elements.

The frontend's observed alignment modes are `Top`, `Bottom`, `Left`, `Right`,
`Horizontal`, `Vertical`, `HorizontalEvenly` and `VerticalEvenly`.
`Horizontal` aligns vertical centers; `Vertical` aligns horizontal centers.
These selected-element operations are not proof of a general whole-diagram
auto-layout service. The inspected layout handler filters connection, pool,
lane, milestone, outer-icon and text-element shapes from that selection.

## Real CEF bindings are a mandatory acceptance boundary

The installed editor bundle has a development/demo path that installs mock
external handlers when its CEF host is absent. Some mutation handlers on that
path do nothing; some queries return demonstration content. Therefore:

- Loading the bundle successfully is not native integration proof.
- Seeing a selected shape move is not native persistence proof.
- A standalone browser page without verified .NET bindings cannot accredit the
  MCP feature, even if it produces visually plausible results.
- Missing bindings, rejected callbacks and unimplemented bridge methods must
  be explicit failures, never silent no-op success or demo fallbacks.

The observed desktop binding names are `diagramEditorElementsHandler`,
`diagramEditorViewEventsHandler`, `diagramEditorSynchronousHandler` and
`diagramEditorKeyboardCommandHandler`. Their presence alone is insufficient:
the actual callback must reach the owned worker and its native model.
The normal presenter also references UI services, runtime state and an STA
dispatch path, so constructing it wholesale is not yet a verified headless
solution.

The existing image-generator integration registers its own native synchronous
handler. It must not be relabeled as the complete editor bridge. No modification
to the installed bundle, personal configuration or running operator instance
was performed during this research.

For the embedding mechanism, the official
[CefSharp usage documentation](https://github.com/cefsharp/CefSharp/wiki/General-Usage)
describes `JavascriptObjectRepository.Register`, `CefSharp.BindObjectAsync` and
load/context notifications. Those APIs establish a transport mechanism, not
Bizagi-specific command fidelity or automatic Modeler compatibility.

## Evidence required for the next programmatic gate

The next route must retain the original file and use real native data throughout:

1. Load an actual native model into an isolated worker and obtain its native
   editor DTO/configuration through the installed adapters.
2. Load the installed editor asset in a windowless CEF instance with verified
   bindings backed by that real model. Reject demo or missing-handler paths.
3. Select actual native IDs programmatically and invoke an explicit layout mode.
4. Correlate and record the actual native frontend callback, including moved
   nodes, changed connector waypoints, labels and any dependent changes.
5. Apply those observations through the native command/adapter boundary; reject
   semantic, containment or identity changes not authorized by the operation.
6. Persist through the installed engine, terminate the editor worker and load
   the resulting `.bpm` in a new native process.
7. Check intended geometry, unchanged semantic content and every original
   archive leaf, including attachments, attributes and simulation configuration.
8. Exercise actual MCP dispatch, cancellation, missing-binding failure, worker
   recovery, rich nested/pool cases and independent visual compatibility.

Subtree placement, docking, labels, boundaries, swimlanes and reusable calls
need explicit contracts and acceptance cases. Selected alignment, selected
distribution, connector routing and whole-diagram automatic layout remain
distinct requirements; one successful trajectory must not stand in for all.

## Private research provenance

The inspected local installation contained these unchanged assets:

| Asset | Bytes | SHA-256 |
| --- | ---: | --- |
| `modeler-bpmn-editor.min.js` | 2,535,766 | `eec7db9e1474632e0e712c5df29ddc5b93aecb765cd8bc93422a1007ad8de1a9` |
| `modeler-bpmn-viewer.min.js` | 1,710,978 | `d10aabeb9c38acf34df3f7fc7c2577dfc19a6ce0b538b32efee339009959ec94` |

These are inspection fingerprints, not an operational compatibility allowlist.
Proprietary source, bundles and decompilation dumps are not redistributed.
No layout operation, frontend/native write or live-document synchronization is
claimed as completed by this static investigation.
