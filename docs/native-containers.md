# Native containers and expanded subprocesses

Current source after `0.4.0-alpha.1` extends `native_mutate` with explicit native
pool/process ownership, stable lane containment, milestones and embedded
subprocess lifecycle. These changes are **not** in the existing 0.4 release ZIP.
They remain part of the experimental full-automation implementation, not a claim
of complete desktop equivalence.

## Stable identities, not runtime aliases

- A participant owns a native process. Creating `Participant` requires a new,
  explicit `NativeMutation.ProcessId`, distinct from every mutation identity.
  The caller can use that process ID as the parent of later members in the same batch.
- A lane's `NativeElement.ParentId` is its **process ID**, not a native `LaneSet`
  GUID. Bizagi reconstructs lane-set GUIDs on loading; those runtime groups are
  no longer exposed as durable operator identities.
- `NativeMutation.ParentId` for lane or milestone creation must identify the
  process of a visible participant. Embedded subprocesses do not accept lanes.
- Embedded subprocesses own their tasks, events, flows, artifacts and further
  embedded subprocesses. Their durable representation includes both an XPDL
  `Activity` and the explicitly linked `ActivitySet`.
- All IDs use canonical nonempty lowercase GUIDs. `ProcessId` is a creation-only
  field, not an arbitrary process reassignment operation.

The contract uses the installed native element factory and native persistence.
It does not synthesize or patch native ZIP content to make a test pass.

## Complete lane and milestone partitions

The native loader normalizes partition geometry. A transaction must describe
the intended final state explicitly rather than silently accepting that drift:

| Element | Required final geometry |
| --- | --- |
| Lane | `X = 50`; `Width = pool.Width - 50`; consecutive `Y` offsets starting at zero |
| Pool with lanes | `Height = sum(lane.Height)` |
| Milestone | Consecutive `X` offsets starting at 50, in native milestone order; full pool height |
| Pool with milestones | `Width = 50 + sum(milestone.Width)` |

Include updates to every affected lane, milestone and pool in the same batch.
For example, changing pool height with lanes and milestones requires the final
lane heights and milestone heights as explicit mutations. A partial or
inconsistent partition request fails; the original file remains untouched.

This is deterministic geometry validation, **not** automatic layout, automatic
containment reassignment or a comprehensive diagram-quality validator.

## Expanded and collapsed sizes

`NativeMutation.Geometry` continues to describe the collapsed node bounds and
the requested `Expanded` state. An embedded subprocess can now supply a separate
`NativeMutation.ExpandedSize` with positive `Width` and `Height`:

```json
{
  "Operation": "update",
  "ElementId": "40000000-0000-4000-8000-000000000004",
  "Geometry": {
    "X": 100, "Y": 35, "Width": 100, "Height": 70,
    "Expanded": true
  },
  "ExpandedSize": { "Width": 550, "Height": 250 }
}
```

Replace the example identity with an actual embedded-subprocess ID returned by
`native_inspect`, and supply its current file revision to `native_mutate`.
Expanding requires explicit expanded dimensions. Collapsing without an
`ExpandedSize` preserves the stored expanded dimensions for later use.

`NativeElement.ExpandedGeometry` reports the stored expanded bounds for
subprocesses even while they are collapsed; its `Expanded` field indicates the
current state. Top-level process nodes use diagram coordinates: the pool origin
is not added automatically. Children inside an embedded subprocess use that
subprocess's local coordinates.
Call-activity expanded editing requires a separate contract and is not enabled
by this embedded-subprocess size operation.

## Deletion and fidelity

- Delete or reconnect incident connections first.
- Delete child elements explicitly before their container. There is no implicit cascade.
- A participant's empty native process is removed together with the participant.
- Deleting a main or last participant is rejected instead of allowing the native
  loader to reconstruct an implicit model with new identities.
- The independent comparison checks each requested identity before projecting
  related structures. Parent-first creation cannot hide a later child request;
  parent deletion cannot conceal an unrequested child.
- Pool/process and subprocess/activity-set references must be unique, correctly
  owned and durable. Reusing an existing process or sharing a companion fails.
- Pool documentation has a native process-header and runtime-JSON representation.
  Only the explicit description leaf is projected; every other JSON leaf stays
  subject to comparison, including unknown fields and numeric encodings.
- An authorized name does not excuse a same-ID `Name` attribute inside unknown
  XML or extended-attribute payloads.
- Expanded graphics and XPDL block view are verified separately, including the
  native serializer's omitted default `COLLAPSED` value.

The output remains a new revision-identified artifact. Native attributes,
embedded bytes, simulation data and all other archive leaves outside the
requested change remain subject to the full fidelity gate.

## Repeat the real circuit

```powershell
dotnet build -c Release --no-restore
dotnet test tests/McpBizagi.Core.Tests -c Release --no-build
dotnet run --project tests/McpBizagi.Acceptance -c Release --no-build -- . --native --containers-only
```

The independent acceptance client imports the project's own BPMN example,
creates a pool/process with two lanes and two milestones, creates two nested
subprocess levels with tasks and a connection, then edits, expands, renders,
saves, clears documentation, collapses and deletes that new structure. Every
mutation uses MCP stdio, installed native services, persistence and a separate
reader worker. Invalid deletion and inconsistent lane geometry are real failure
paths followed by valid operations.

To repeat against an existing rich native model without changing it:

```powershell
dotnet run --project tests/McpBizagi.Acceptance -c Release --no-build -- . --native --containers-only --input C:/Processes/rich-model.bpm
```

The fixture is copied into the private acceptance workspace. Raw models,
transcripts and images remain private because native files can include operator
metadata. [Sanitized run evidence](validation.md) distinguishes current source
and package execution.

## Still open in this family

Container moves, general selection/refactoring and automatic layout still need
implementation and native acceptance. Later work has separately added
[diagram lifecycle](native-diagrams.md), [local reusable calls](native-calls.md),
[special subprocesses](native-subprocesses.md), [events](native-events.md) and
[typography](native-styles.md); their individual contracts and dated evidence,
not this earlier container corpus, determine their verified scope.
Independent visual compatibility **inside Modeler** remains separate from the
offscreen renderer and fresh native readback. None of these boundaries closes
or reduces the full-automation objective.
