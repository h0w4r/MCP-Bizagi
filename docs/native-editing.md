# Native editing batches

`native_mutate(path, expectedRevision, mutations)` operates on native objects in
an isolated worker. It does not reconstruct a `.bpm` ZIP or convert it to BPMN.
The original file is never overwritten.

## Transaction and evidence

1. Read the archive and verify its SHA-256 revision.
2. Snapshot it into the private operation directory.
3. Apply the ordered request using the installed element factory and native collections.
4. Persist a new `.bpm` using Bizagi's persistence manager.
5. Start another worker and inspect the durable native graph.
6. Verify every requested identity, type, parent, value and connection endpoint.
7. Project only explicitly requested changes out of the comparison, then compare
   every remaining native archive leaf under the existing fidelity policy.

The request, native replies and `native-fidelity.json` remain available locally.
An unexplained change fails the operation and prevents its artifact from being
accepted as an input through an opaque `artifact:` reference.

## Request contract

`NativeMutation` accepts only these operations:

| Operation | Required fields | Optional fields |
| --- | --- | --- |
| `create` | `elementId`, `parentId`, `elementType` | `name`, `documentation`, `geometry`, `callTarget` for calls; connectors require endpoints and points |
| `update` | Existing `elementId`; at least one changed value | `name`, `documentation`, `geometry`, `callTarget` for calls |
| `delete` | Existing `elementId` | None; incident connections and nonempty children must be handled first |
| `reconnect` | Existing connector `elementId`, `sourceId`, `targetId`, `points` | None |

IDs are canonical lowercase GUIDs obtained from `native_inspect`, except new IDs
which the client generates. A batch has 1–1,000 mutations and cannot address the
same ID twice. Use the returned revision and opaque artifact for another batch.

Current source also accepts typed `NativeMutation.ActivityProperties`,
`GatewayDirection` and complete `FlowCondition` replacements on compatible
native element kinds. See [semantic properties and their separate simulation
boundary](native-semantics.md). Delete/reconnect cannot carry hidden property
updates. Unknown members in typed requests are rejected by the SDK marshaller.

`NativeMutation.Style` adds sparse typography, fill/outline colors and native
label bounds, with installed-font discovery through `native_fonts_get`.
Persistence and rendered label placement have separate, type-specific limits;
see [native styling](native-styles.md). Styling is not accepted on delete or
reconnect operations and cannot duplicate colors supplied in `Geometry`.

`ActivityLoop` explicitly replaces the native loop configuration; see
[loop semantics and limits](native-loops.md). `EventMode` selects an intermediate
event class at creation, and `EventProperties` patches interruption and boundary
attachment; see [native events](native-events.md). Boundary references must be
handled before deleting their target activity.

`NativeGeometry` contains `x`, `y`, `width`, `height`, `expanded`,
`backgroundArgb` and `borderArgb`. Bounds must be finite and dimensions positive.
Colors are signed 32-bit ARGB values. Expanding an embedded subprocess requires
an explicit `NativeMutation.ExpandedSize`; collapsed bounds and expanded size
are separate fields. Other element classes cannot silently borrow that contract.
Readback exposes that separate surface as `NativeElement.ExpandedGeometry`.
On newly imported expanded BPMN DI shapes, the adapter preserves the supplied
expanded bounds rather than the native importer's threefold size default, and
records each adjustment. Reading/rendering an existing `.bpm` never resizes it.

`NativePoint` contains `x` and `y`. Connectors require 2–10,000 finite points.
Sequence-flow endpoints must be flow nodes in the same native process/container.
Existing incoming/outgoing native references are maintained during reconnection.

## Real tested palette

The acceptance client has created, persisted, reopened and deleted these types:

- Tasks: `AbstractTask`, `UserTask`, `ManualTask`, `ServiceTask`, `ScriptTask`,
  `SendTask`, `ReceiveTask`, `BusinessRuleTask`.
- Start events: `NoneStart`, `MessageStart`, `TimerStart`.
- End events: `NoneEnd`, `MessageEnd`, `TerminateEnd`.
- Intermediate events: `NoneIntermediate` uses throw mode;
  `MessageIntermediate` and `TimerIntermediate` use catch mode.
- Gateways: `ExclusiveGateway`, `InclusiveGateway`, `ParallelGateway`,
  `EventBasedGateway`, `ComplexGateway`.

A separate real batch inserted a `UserTask` and `SequenceFlow`, reconnected an
existing flow, changed descriptions, bounds and colors, then removed the inserted
structure. Every successful batch passed fresh-worker and native-container gates.

The disconnected palette is a structural editing corpus, not a claim that it is
a valid executable business process. Use `native_validate` separately.

## Remaining boundaries

The investigated allowlist is wider than the tested palette. Current source adds
a separate [container lifecycle contract](native-containers.md) for participants,
stable process-owned lanes, milestones and nested embedded subprocesses. These
unreleased additions are not covered by the old palette run or old release ZIP.
The [local reusable-call contract](native-calls.md) separately covers
`CallActivity` creation, target updates, guarded deletion and clone references.
Data/artifact categories, reparenting, boundary-event modes,
conditional flow details, rich styles and automatic layout are not accredited by
the palette test. Unknown native content is not excused merely because an edit
was requested. Expanded coverage requires its own native persistence proof.

Repeat the actual MCP circuits with an existing native test model containing a
task with an incoming flow:

```powershell
dotnet run --project tests/McpBizagi.Acceptance -c Release --no-build -- . --native --mutations-only --input C:/Processes/example.bpm
dotnet run --project tests/McpBizagi.Acceptance -c Release --no-build -- . --native --palette-only --input C:/Processes/example.bpm
```
