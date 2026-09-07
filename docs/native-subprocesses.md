# Native special subprocesses

This is an experimental native `.bpm` contract, not an XML replacement model or
desktop click driver. The installed factory and persistence engine own the
objects and their serialization. See [validation](validation.md) for actual
completed circuits; an exposed option is not an accreditation by itself.

## Creation and explicit updates

Use `native_mutate` with `NativeMutation.ElementType = "SubProcess"`.
`NativeMutation.SubProcessKind` is a **creation-only** choice:

| Value | Native object |
| --- | --- |
| `SubProcess` or omitted | Ordinary embedded subprocess |
| `Transaction` | Native transaction subprocess |
| `AdHoc` | Native ad hoc subprocess |

This is not reusable `CallActivity` editing and does not implicitly convert
existing classes. Set `NativeMutation.SubProcessProperties.TriggeredByEvent`
to `true` on an ordinary embedded subprocess for an event-triggered subprocess.
It cannot also be a transaction or an ad hoc subprocess.

`NativeMutation.SubProcessProperties` is a nonempty create/update patch:

| Field | Meaning |
| --- | --- |
| `TriggeredByEvent` | Nullable flag; omission preserves existing state |
| `AdHocOrdering` | Exact `Parallel` or `Sequential`; ad hoc objects only |
| `AdHocCompletionCondition` | Native expression text; null preserves, empty clears |

Condition text is not evaluated by this server. Updating it is not evidence
that the native simulator executes the expression. The per-operation text bound
is 1 MiB of .NET characters. Unknown expression data remains part of the native
archive fidelity comparison, not a silently discarded extension.

## Event context and references

- `ErrorStart`, `EscalationStart` and `CompensationStart` require an event-triggered
  subprocess. Noninterrupting start events also require that context.
- `CancelEnd` requires a transaction parent.
- `CancelIntermediate` is a boundary creation mode and must attach to a
  transaction in the same flow container and diagram.
- Error, compensation and cancel exception events cannot be made
  noninterrupting by this contract.
- Event-triggered subprocesses cannot participate in the parent's sequence flow.
- Existing children are checked when their parent's trigger flag changes. Remove
  incompatible children explicitly before clearing the flag; unrelated shapes
  are not silently deleted or converted.
- Remove or reattach boundaries before deleting the activity they reference.

These are mutation preconditions, not a complete BPMN semantic validator.
Bizagi describes the desktop distinctions in its [activity palette](https://help.bizagi.com/platform/en/activities.htm)
and [event palette](https://help.bizagi.com/platform/en/events.htm).

## Readback, preservation and rendering

`NativeElement.SubProcess` comes from the actual native object after loading:
`Kind`, `TriggeredByEvent`, `AdHocOrdering`, `AdHocCompletionCondition`,
`CancelRemainingInstances` and `TransactionMethod`. The last two fields are
**read-only observations**. No writable contract is advertised for them: a native
property's presence is not proof of durable editing support.

Successful mutations require a different worker to load the artifact and verify
the requested class and properties. Native archive comparison projects only the
requested attributes on the exact linked XPDL `ActivitySet`; it does not remove
unknown children, attributes, namespaces or sibling data from comparison.
Native cloning verifies subprocess properties and remapped boundary references.

The offscreen renderer and documentation image selection include the native
subprocess subclasses rather than only exact ordinary-subprocess class names.
`native_render_svg.subProcessId` selects a particular nested surface. Offscreen
output remains distinct from independent compatibility verification in the
Modeler desktop application.

## Simulation boundary

Bizagi documents transactional and ad hoc processes as unsupported simulation
diagrams. Do not infer transactional rollback, ad hoc scheduling or event
execution semantics from a successful native save, renderer output or simulator
completion. [Official simulation considerations](https://help.bizagi.com/platform/en/simulation_in_bizagi.htm).
Successful native simulation responses identify those actual input elements
using `NativeSimulationLimitation.Code = "special_subprocess_simulation_unsupported"`
and `NativeSimulationLimitation.SubProcess`. This warning does not reinterpret,
replace or manufacture the native simulator's output.

## Example and acceptance

[The example mutations](../examples/native-subprocesses.json) use illustrative
GUIDs. Replace the parent ID with a real process ID from `native_inspect` and
allocate unused IDs for new objects. Pass the array to `native_mutate` together
with the actual source path and its current SHA-256 revision. Output remains
artifact-first; [workspace adoption](native-commit.md) is a separate operation.

```powershell
dotnet run --project tests/McpBizagi.Acceptance -c Release --no-build -- . --native --subprocesses-only
```

This operator-controlled entry point uses the independent official MCP SDK
client, actual stdio host, restricted local pipe, installed engine, durable
artifact and fresh-worker readback. Unit projection fixtures do not replace it.
Event payload editing, class conversion, broader simulation behavior, live
unsaved synchronization and full Modeler automation remain separate goals.
