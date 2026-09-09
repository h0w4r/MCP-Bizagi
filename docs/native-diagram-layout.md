# Automatic native diagram layout

`native_diagram_layout` calculates a complete represented diagram on the MCP
server. Unlike `native_surface_layout`, it coordinates visible pools, lane and
milestone partitions, nested expanded subprocess bodies, graphical groups and
cross-pool messages.
It is an experimental source addition after the immutable 0.6.0-alpha.1 package,
not a claim of complete Modeler or live-document automation.

## Operator contract

Inspect the native file first. Use its actual diagram UUID and exact SHA-256
revision; identifiers from a different model are not interchangeable.

| Argument | Meaning |
| --- | --- |
| `path` | Confined `.bpm` path or completed native artifact handle |
| `expectedRevision` | Exact inspected input SHA-256 |
| `layout.DiagramId` | One actual Collaboration UUID |
| `layout.Direction` | `Right` (default) or `Down` |

The client supplies neither calculated coordinates nor a partial selection.
Poll `operation_get` with the returned operation ID. Only a completed operation
provides an adoptable `outputArtifact` and `outputRevision`; the original file is
retained. `operation_cancel` uses the existing cancellation/worker ownership
contract. An interrupted write is not blindly replayed.

`Result.plan` contains the source graph hash, direction, solver version and DLL
SHA-256, typed pre-write mutations, pool memberships and surface/port intent.
`Result.before`, `edited` and `reopened` distinguish source reading, native
writing and a fresh worker's durable-file reading. `Result.fidelity` is the
whole-archive comparison, not just the shapes requested by the client.

## How it works

1. `NativeWorkflows.LayoutDiagram` captures and revision-checks the source,
   writes a staging copy, and opens it with the installed native reader.
2. `NativeDiagramLayoutPlanner.Calculate` validates identifiers, complete record
   coverage, coordinates, ports and the 1,000-record selected-diagram bound.
   Native connector snapshots have zero-size graphical placeholders: their
   actual geometry is the point sequence, not a movable node rectangle.
3. `NativeDiagramPartitionPlanner` records source pool/lane/milestone membership
   before changing any size. It plans embedded bodies bottom-up and sizes
   expanded bounds from child rectangles, route points and manual labels.
4. `NativeDiagramSurfacePlanner` uses pinned **Msagl 1.2.1** `LayeredLayout`
   with seed 17, layer separation 100 and node separation 70. Host packing
   envelopes include attached events and manually positioned labels.
   Two-dimensional partition constraints retain the original assignments.
5. MSAGL `RectilinearEdgeRouter` routes against actual visible rectangles,
   external manual labels and, for cross-pool messages, pool header bands.
   Existing cardinal midpoint ports are fixed using the library's public
   `FloatingPort(null, point)` API and real obstacle membership. Route endpoints
   must agree before dispatch; the result is not patched to disguise drift.
6. `NativeDiagramLayoutGeometry.Verify` checks predicted geometry independently
   of MSAGL's success result. The complete typed plan is persisted before the
   installed native `mutate_save` writer executes it.
7. A separate worker reopens the output. Native graph/containment, image restart,
   entire archive fidelity and independent geometry gates must all pass.

### Graphical group enclosures

`NativeDiagramGroupLayout` treats native `Group` records as diagram-owned
graphical enclosures, not filled routing obstacles or process ownership.
It observes which root visible shapes and complete pools the original rectangle
contains. Embedded local-coordinate children are represented by their visible
subprocess bounds, not mistaken for global positions.

After planning the enclosed elements, each nonempty group retains its original
left, top, right and bottom margins around those elements' union. Empty groups
retain their bounds. Nested/overlapping group relationships and enclosed identity
sets must remain unchanged; the same checks run on the fresh native reader.
Unrequested group text, color and metadata remain under full-archive fidelity.
The installed adapter persists the native intrinsic expanded group and its
derived dimensions, rather than introducing a new grouping format.

A group whose border cuts through a root shape is ambiguous and explicitly
rejected. A pool band crossing is not itself an error: graphical groups may
enclose only a subset of a pool's activities. If recalculated rectangular bounds
would acquire another shape or alter another group's enclosure relationship,
the operation fails rather than silently changing the grouping. These constraints
do not turn BPMN graphical grouping into execution semantics or prove that all
overlapping arrangements can be laid out.

No vendor binaries are redistributed, vendor library is patched, generic
reflection tool is exposed, saved XML is rewritten, or desktop input is sent.
This tool's routing is calculated by MSAGL and persisted by Bizagi; it must not
be confused with the installed editor's grouped routing used by
`native_surface_layout`.

Real solver progress events publish algorithm-local ratios in operation phases.
Both placement and routing link their cancellation token to the operation token.
Native work retains the existing activity-based monitoring, not a total runtime
deadline. Polling can miss brief solver phases; a recorded terminal success is
not evidence that an operator observed or cancelled a particular live phase.

## Verified corpus and explicit limits

The independent official-SDK client covers blank and single-node diagrams,
Right and Down layouts, two pools, four lanes, four milestones, two nested
expanded bodies, cycles, self-loops and two bidirectional cross-pool messages.
It also covers three noninterrupting timer boundary events, 17 manual labels,
an untouched second diagram and exact opaque presentation-file bytes.
Separate checks cover native save-copy, root/nested SVG endpoints, unknown-owner
failure, a real inconsistent lane-resize rejection, live cancellation and recovery.
See [execution evidence](validation.md) for the actual source/package runs.

These boundaries deliberately reject unsupported work instead of returning
partial success:

- More than 1,000 selected diagram records or subprocess nesting over 100.
- No visible native pool, ambiguous/out-of-bounds source partition membership,
  missing geometry, duplicate identities, unresolved or cross-diagram routes.
- Diagram-owned artifacts other than the represented graphical groups, or other
  surfaces without complete movement rules.
- Unknown/offset ports, or original endpoints inconsistent with their ports.
  Unspecified/zero port metadata requires a unique observed cardinal midpoint;
  the original metadata is retained rather than replaced by an invented ID.
- Attached events on an expanded host whose dimensions must change, until an
  explicit anchor-resize contract is available.
- Non-line solver curves, unrepresentable geometry or a result rejected by the
  installed engine, fresh reader or unknown-content fidelity policy.

Rectangular clearance and native SVG endpoint equality do **not** establish
rounded-curve/arrowhead clearance, text rendering quality, every BPMN glyph,
every port/shape combination or independent desktop GUI compatibility.
Automatic label placement is not provided: manually positioned labels retain
their dimensions and host-relative offsets. Other diagrams are not laid out.

## Reproduce

From the repository root, after a Release build, run in PowerShell 7:

```powershell
# The official SDK client authors its own native corpus through MCP.
dotnet run --project tests/McpBizagi.Acceptance -c Release --no-build -- . --native --diagram-layout-only

# Use the exact evidence directory printed by that successful client.
./tests/diagram-layout-audit.ps1 -Run '<printed evidence directory>'
```

For an extracted distribution, append `--package '<extracted directory>'
--discover-worker` to the client command. Verify its complete manifest before
and after execution using the distribution's `scripts/verify-package.ps1`.
Unreleased dirty-checkout candidates require the explicitly named private-test
override; they do not become clean releases by passing native tests.

The independent PowerShell audit imports neither MSAGL nor production
comparators. It reads complete native receipts, checks retained port identities,
boundary semantics/offsets, manual-label dimensions/offsets, node and route
clearance, SVG path endpoints and exact embedded bytes. Its corpus-size checks
prevent empty or incomplete evidence from passing. These are acceptance checks,
not runtime restrictions on operator models. Public CI does not run these
native tests on an operator's machine or execute untrusted PR code there.

The extended group corpus uses `--native --diagram-layout-groups-only` and
`./tests/diagram-layout-audit.ps1 -Run '<printed evidence directory>' -ExpectedGroups 6`.
It includes nested whole-pool groups, single-task and multi-task/boundary subsets,
an empty group, and a real saved partial-group rejection. The audit checks all
six native SVG identities, source membership, all four margins and empty bounds
without importing the production planner.
