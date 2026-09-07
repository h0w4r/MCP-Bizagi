# Native data objects, stores and activity I/O

The current source provides typed native data mutations. It uses the locally
installed Modeler 4.3.0.008 engine and keeps `.bpm` as the authoritative format.
These changes are experimental and are not included in the existing release
archive. Full desktop compatibility and full Modeler automation remain separate
acceptance gates.

## Data properties

`NativeMutation.DataProperties` is an optional sparse patch. Omission preserves
the existing property; an empty string explicitly clears a text property.
The [example mutation batch](../examples/native-data.json) uses illustrative
identities: replace them with inspected object/store/process IDs and one fresh
reference ID. Use the output artifact and revision for each subsequent operation.

| Native kind | Supported `NativeDataProperties` fields | Ownership |
| --- | --- | --- |
| `DataObject` | `State`, `IsCollection` | Process or embedded subprocess |
| `DataStore` | `State`, `Capacity`, `IsUnlimited` | Diagram catalog |
| `DataStoreReference` | `StoreId` | Process or embedded subprocess |

- `DataStore` is an explicit creation alias for the native catalog class. Its
  observed `NativeElement.ElementType` is `Other`; it is not a graphical node.
  Use a diagram ID as `NativeMutation.ParentId` and omit `Geometry`.
- `DataStoreReference` creation requires an explicit existing `StoreId` in the
  same diagram. Its visible label comes from the shared store catalog; use the
  catalog's identity when editing its name.
- A store's state is shared by its references. The native serializer stores the
  state on references, not on the catalog record. A state patch therefore needs
  at least one durable reference. Clear a nonempty shared state before removing
  or relinking its final reference.
- Delete or relink every store reference before deleting its catalog.
- Capacity accepts canonical nonnegative integer text, including `0`, or empty
  to clear. It is not an allocation limit enforced by this MCP.
- A data object's `IsCollection` cannot override an imported shared item
  definition implicitly. That case fails instead of modifying another owner.
- Data-object and store documentation uses `NativeMutation.Documentation`.

`NativeElement.Data` distinguishes the actual `StoreId` object reference from
the raw `StoreBpmnName` / `StoreBpmnNamespace` QName. These are observations, not
interchangeable identifiers invented by the server.

## Associations are more than visible lines

Create an `Association` with explicit `SourceId`, `TargetId` and `Points` in the
same native flow container. Reconnection uses the existing `reconnect` operation.
The adapter reconciles native activity data bindings through the installed
`BPMN20ModelUtil.AddDataInput`, `AddDataOutput`, `RemoveDataInput` and
`RemoveDataOutput` methods.

- Data object/store reference → activity creates an input binding.
- Activity → data object/store reference creates an output binding.
- A data association attached to a sequence flow creates the applicable output
  on its source activity and input on its target activity.
- Parallel graphical associations share a logical binding. Removing one does
  not delete or recreate the binding still used by another association.
- Reconnecting the sequence flow also reconciles the affected data bindings.
- Automatic event I/O binding and arbitrary authored I/O graph editing are not
  yet supported by this mutation contract. They are not inferred from native
  enum membership or the existence of an internal class.
- Automatic cleanup rejects nonempty names, states, collection semantics,
  documentation, extended attributes and unrecognized payload on owned I/O.
  Rendering metadata on a deleted auto-managed port is part of that deletion.

`NativeElement.DataFlow` exposes owned `Inputs`, `Outputs`, `InputAssociations`,
`OutputAssociations`, ordered `InputSets` / `OutputSets`, and `HasSpecification`.
Ports and associations have durable identities. Input/output set runtime GUIDs
are deliberately not exposed as durable IDs. Owned ports are not advertised as
standalone graphical shapes or freely mutable flow elements.

## Nested loading and native cloning

The installed subprocess loader does not forward the enclosing process's I/O
records to its nested activity loader. The adapter rehydrates missing nested
ports using the original durable XPDL records and the installed
`InputAndOutputSetsAdapter`, not synthesized data or a replacement serializer.
`EngineReply.IntegrationAdjustments` records each
`nested_native_io_rehydrated:<activity-id>` adjustment. Partially loaded or
unresolved state fails instead of being silently replaced.

The installed collaboration cloner also shallow-copies some collection items
and reconstructs I/O while remapping graphical associations. The integration
isolates the affected I/O properties, restores original references in `finally`,
then installs independent native copies with explicit identity mappings.
Catalog stores, visible references, owned ports, data associations and input/output
memberships must all retain their content and resolve inside the cloned diagram.
References still pointing at original data identities are rejected.

These are version-specific integration adjustments, not modifications to vendor
binaries. A successful native read is not proof that an independent desktop
Modeler session displays or retains every property identically.

## Verification and recovery

Every successful mutation must pass native persistence, a newly started native
reader, typed postconditions and whole-archive comparison. The comparator projects
only explicitly requested fields and verified derived I/O relationships. Unknown
content and unrelated model, attachment, simulation and documentation data remain
subject to comparison. The original input is never overwritten by `native_mutate`.

Private operation artifacts retain `mutation-request.json`, `mutation-readback.json`,
engine fingerprints and a fidelity report when comparison reaches that phase.
The readback artifact is written before postcondition checking so a rejected
operation still retains both real worker observations.

Run the independent SDK acceptance client from the repository root:

```powershell
dotnet run --project tests/McpBizagi.Acceptance -c Release -- . --native --data-only
```

The circuit covers multiple diagrams, Unicode text, shared and nested store
references, parallel associations, activity I/O, sequence-flow reconnection,
unrelated edits, guarded failures, native cloning, offscreen rendering, Word
publication, explicit clearing/deletion and a final no-op save/readback. Unit
fixtures separately test exact namespace/owner checks, readback mismatches and
unknown-content protection. Consult the capability ledger for the latest
accreditation status; test presence alone does not prove a successful run.

### Recorded source acceptance

On 2026-09-07, source run `20260907-203617-f9f3a5` completed the entire
`NATIVE_DATA_LIFECYCLE_PASS` circuit on installed Modeler `4.3.0.008`:
20 terminal operations (16 completed and four expected guarded failures),
35 independently observed worker lifecycles and 311 periodic desktop samples.
No visible worker window or worker foreground was observed. Periodic observation
is not continuous proof and is not independent Modeler GUI verification.
The private SDK transcript SHA-256 is
`3985e694b25b966216dbe4ddf9063f1e64f6d4be4b8b6acd7d2029d6d0d4b3f7`.

The same source passed the expanded native regression
`20260907-204101-efa9c6` (17 operations, including expected failure, cancellation
and host-interruption/recovery paths) and the event-payload regression
`20260907-204255-686c71` (19 operations). The Release build had no warnings or
errors; 691 unit/component tests passed separately. These are source acceptance
results, not a new distribution release or a full-automation completion claim.
