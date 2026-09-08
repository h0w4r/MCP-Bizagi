# Native element reparenting

`native_elements_reparent` changes explicit containment in a private native
`.bpm` copy. It uses actual installed domain objects and collections, not the
clipboard, generated replacement XML, simulated input or a foreground window.
This remains experimental, with a real reparenting circuit in the
[0.6.0-alpha.1 package acceptance](validation-package-0.6.md).
The immutable 0.5 ZIP predates the tool. Full Modeler automation remains open.

## Request

Supply a confined `path`, its actual `expectedRevision` SHA-256 and `moves`.
Each `NativeReparenting` entry contains:

| Field | Meaning |
| --- | --- |
| `ElementId` | Actual native GUID of the selected subtree root |
| `ExpectedParentId` | Required current container GUID, checked before editing |
| `TargetParentId` | Existing process or embedded subprocess in the same diagram |
| `Position` | Optional `NativePoint` with `X` and `Y`; no implicit resizing |

Use `native_inspect` to obtain actual IDs and revision. Select 1–1000 distinct
roots; do not select both a subtree and one of its descendants. Include the
complete affected sequence-flow, association and attached-boundary closure.
The server does not guess which connections to remove or reconnect.

See [the request template](../examples/native-reparenting.json). It contains
placeholders, not hardcoded native identities that exist in every model.
The independent acceptance command below authors its own actual native corpus.

## Native execution and preservation

1. Capture the revision-checked original into private operation storage.
2. A source-reader worker loads the actual model and checks final containment,
   expected owners, cycles, reference closure and relevant event contexts.
3. An editor worker removes and adds the **same objects** in the installed
   `FlowElements` / `Artifacts` collections, then saves through the native engine.
4. A new reader worker loads the result. Compare its graph and metadata with
   the editor, check images and all requested/unchanged observed graph fields.
5. Compare every native archive leaf, including unknown XML and binary content.
   Unexplained differences fail the operation rather than silently publish it.

The fidelity policy verifies the resulting native XML owner before restoring
only requested relocations **in comparison copies**. It retains each actual
record's payload, including separately serialized native I/O associations,
process-level I/O ports and flattened subprocess activity sets. Original collection order is restored for
comparison; unrequested membership changes remain failures. Unknown attributes,
comments, preserved text and unrelated records are not discarded.

Same-diagram attributes, embedded attachments, image files, roles/RACI,
scenario definitions and persisted user preferences remain subject to the
whole-container gate. Retained IDs keep these references stable. This does not
promise equivalent simulated behavior after changing execution containment.

## Coordinates are explicit, not automatic layout

Omitting `Position` retains the selected root's native coordinates. Supplying
it changes only that root's X/Y; native width, height, style and collapsed or
expanded view are retained. Descendant coordinates and connector paths are
not translated or routed automatically. Connector positions cannot be patched
as though they were nodes.

Root artifacts are serialized in a diagram-wide native collection; the native
loader resolves their participant using pool geometry. A requested root
artifact must fit its intended visible pool, or the operation rejects it.
Fresh-reader ownership, not the editor's in-memory assignment, decides success.

## Results, cancellation and publication

Poll `operation_get` using the returned `OperationId`. A completed `Result`
contains `before`, `edited`, `reopened`, `fidelity`, `requestedMovesVerified`,
`nativeSourceUnmodified`, `outputArtifact` and `outputRevision`.
`interpretationWarning` states the layout and compatibility boundaries.

The original is never overwritten by this tool. Failed, cancelled and
interrupted artifacts are quarantined. Use `operation_cancel` for owned work;
do not blindly retry a write after disconnecting. Adopting a verified artifact
into an existing workspace file remains the separate
[`native_commit`](native-commit.md) operation, with revision checks and backup.

Private evidence includes `reparenting-request.json`,
`reparenting-readback.json`, `reparenting-fidelity.json`, worker receipts and
the MCP transcript. These may contain operator model data and are not uploaded.

## Boundaries that remain explicit

- Cross-diagram moves require metadata, file, scenario and preference migration;
  this tool rejects them rather than silently convert through BPMN.
- Diagram catalogs, participants/processes, groups, lanes and milestones have
  separate lifecycle and ordering contracts, not this contained-element move.
- This is not copy/paste, extraction, reverse inlining, automatic layout,
  behavioral equivalence, live unsaved document editing or GUI accreditation.
- Shared Enterprise models are outside this local desktop contract.
- Special context changes that invalidate boundary/start/end semantics reject.

## Independent acceptance

```powershell
dotnet build -c Release
dotnet test tests/McpBizagi.Core.Tests -c Release --no-build
dotnet tests/McpBizagi.Acceptance/bin/Release/net10.0-windows/McpBizagi.Acceptance.dll . --native --reparenting-only
```

The client creates two diagrams, native pools/subprocesses, nested tasks,
sequence and data associations, a boundary event, Unicode attributes, an
embedded file, a transparent image, RACI, a configured scenario and persisted
tabs. It tests same-diagram subtree moves and their inverses, cross-process
containment, explicit coordinates, root/nested image ownership, native no-op
save and rendering. Stale revision, incomplete closure, cyclic selection,
subsequent recovery and unchanged-original checks are independent assertions.
Pure policy tests remain separate from actual installed-engine acceptance.

The [verification ledger](validation.md#native-same-diagram-reparenting--2026-09-08-utc)
records the successful rich run, exact transcript hash, failure assertions and
worker-exit/desktop observations. It does not accredit arbitrary untested models.
