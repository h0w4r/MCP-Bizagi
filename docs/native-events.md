# Native events and boundary lifecycle

This is an experimental **native `.bpm`** contract for the installed Modeler
adapter. It uses `native_mutate`, not an XML-to-native conversion or mouse input.
Implementation, installed-engine acceptance and visual desktop compatibility are
different states. See [validation](validation.md) for completed circuits.

## Creation modes

`NativeMutation.EventMode` is `Catch`, `Throw` or `Boundary`, and applies **only
when creating an intermediate event**. It does not implicitly convert an existing
event class or discard its definition payloads. The existing defaults remain
`NoneIntermediate` → `Throw`, and `MessageIntermediate` / `TimerIntermediate` →
`Catch`. Additional intermediate kinds require an explicit mode.

| Mode | Intermediate kinds |
| --- | --- |
| Catch | Message, Timer, Conditional, Link, Signal, Multiple, ParallelMultiple |
| Throw | None, Message, Escalation, Link, Compensation, Signal, Multiple |
| Boundary | Message, Timer, Escalation, Conditional, Error, Cancel, Compensation, Signal, Multiple, ParallelMultiple |

Append `Intermediate` to the table names for `NativeMutation.ElementType`.
Other creation additions are `ConditionalStart`, `SignalStart`, `MultipleStart`,
`ParallelMultipleStart`, `EscalationEnd`, `ErrorEnd`, `CompensationEnd`,
`SignalEnd`, `MultipleEnd`, `EventBasedGatewayExclusive` and
`EventBasedGatewayParallel`. Existing start/end/gateway types remain available.
The native factory owns default event definitions; creating a Multiple marker
does **not** configure custom message/error/timer/expression payloads.
`ErrorStart`, `EscalationStart`, `CompensationStart`, `CancelEnd` and
`CancelIntermediate` have explicit context requirements in the
[special-subprocess contract](native-subprocesses.md).

The desktop distinctions are documented by Bizagi in its [event palette](https://help.bizagi.com/platform/en/events.htm)
and [gateway palette](https://help.bizagi.com/platform/en/gateways.htm).
`capabilities_get.nativeMutationTypes` publishes the actual host creation
allowlist; `intermediateCreationModes` publishes the mode spellings. Presence in
that schema inventory is not an installed-engine acceptance result.

## Boundary references and interruption

`NativeMutation.EventProperties` is a patch:

| Field | Meaning |
| --- | --- |
| `IsInterrupting` | Nullable flag on a start or boundary event; omission preserves it |
| `AttachedToActivityId` | Native activity GUID in the same flow container and diagram |

Boundary creation requires a target. Reattachment changes the native activity
object reference and its BPMN/catalog aliases together. A target is **not** a
diagram, process, event, arbitrary string or external model reference. Empty
does not detach: detaching would require a separate explicit event conversion
contract. Error, compensation and cancel boundaries reject a noninterrupting flag.
Noninterrupting start events require an event-triggered subprocess; setting a
flag on an ordinary process is not an equivalent operation.

Delete or reattach boundary events before deleting their activity. Nested
boundaries target activities in the same embedded subprocess, not its outer
process. Sequence flows cannot enter a boundary event, start event or
instantiating event gateway, and cannot leave an end event. These guards do not
claim complete BPMN validation; use the native validation tool separately.

## Actual readback

`NativeElement.Event` reports:

- `Mode`: Start, End, Catch, Throw or Boundary, from the actual native class.
- `IsInterrupting` and `IsParallelMultiple`: native nullable flags.
- `AttachedToActivityId`: resolved native activity object ID.
- `AttachedToBpmnName`, `AttachedToBpmnNamespace` and
  `AttachedToCatalogActivityId`: separate native reference representations.
- `DefinitionKinds`: actual native definition kinds, **not** an exhaustive
  payload serialization or an assertion that referenced data exists.

`NativeElement.EventGateway` reports native `Instantiate` and `Kind` independently
of `NativeElement.GatewayDirection`.

Every successful mutation retains its requested identities, types, containment,
mode and supplied properties after another worker reloads the resulting `.bpm`.
The whole native archive remains subject to the existing fidelity policy.
Updates project only the explicitly requested native event attributes from the
comparison; unknown trigger metadata, namespaces, comments and other content are
not normalized away. Native diagram cloning verifies boundary target remapping
against the native identity map.

## Example and verification

[The example request](../examples/native-events.json) uses illustrative GUIDs.
Replace the parent and target with IDs from your actual `native_inspect` result,
and supply unused GUIDs for new elements. Invoke `native_mutate` with the current
path, expected SHA-256 revision and this mutation array. Adopt the result only
through the separate [native commit contract](native-commit.md).

The operator-controlled native acceptance entry point is:

```powershell
dotnet run --project tests/McpBizagi.Acceptance -c Release --no-build -- . --native --events-only
```

The corpus covers the palette, explicit catch/throw modes, nested boundary
references, interruption/reattachment, wrong-kind and cross-container failures,
guarded deletion, native cloning, no-op save, offscreen rendering and Word
publication. A completed palette circuit does not accredit event execution by
the simulator, arbitrary event-definition payload editing, GUI rendering
equivalence or full Modeler automation. Special subprocesses have their own
separate acceptance entry point and evidence.
