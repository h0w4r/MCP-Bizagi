# Native event definition payloads

Current source exposes typed payload patches through `native_mutate` and actual
payload observations through `native_inspect`. This is separate from event modes,
boundary attachments and [special subprocess contexts](native-subprocesses.md).
It uses the installed 4.3.0.008 engine, not a replacement BPMN serializer.

## Selection and patch semantics

`NativeMutation.EventPayloads` is an array of `NativeEventPayloadPatch` objects.
Each selects a **unique existing definition by `Kind`**. A request must contain
one to eight distinct kinds and at least one actual property per patch. Omitted
properties preserve their current values; empty strings explicitly clear text.
Use the resulting artifact path and revision for the next operation.

- Creation uses the definitions produced by the installed element factory.
- Update does not add, remove, reorder or change definition kinds.
- An absent or ambiguous kind fails instead of changing another definition.
- Payload properties on tasks, connections or other non-events fail.
- Unknown typed JSON members are rejected by the MCP SDK binding configuration.
- `capabilities_get.nativeEventPayloadKinds` describes request vocabulary, not
  evidence that every combination is valid or has been executed.

| Definition kind | Writable fields in `NativeEventPayloadPatch` |
| --- | --- |
| `Message` | `Name` |
| `Timer` | `Timer.Kind` and `Timer.Text` as one complete value |
| `Conditional` | `Name`, `Condition` |
| `Link` | `Name` |
| `Signal` | `Name` |
| `Error` | `ErrorCode` |
| `Escalation` | `EscalationCode` |
| `Compensation` | `Compensation.WaitForCompletion`, `Compensation.ActivityId` |

These names describe definition payloads, not the event shape's own
`NativeMutation.Name`. Message service operations, arbitrary expressions with
extensions, definition identities and arbitrary reference-object editing are not
exposed by this contract. Text has a one-MiB character bound per property.

## Native multiple-event defaults

Inspect `NativeElement.Event.Definitions` rather than guessing a collection
from the word "Multiple". The installed factory creates different sets:

| Native mode | Initial multiple definitions |
| --- | --- |
| Start, including parallel-multiple start | Message, Timer, Conditional, Signal |
| Boundary | Message, Timer, Compensation, Conditional |
| Intermediate catch/throw and end | Message, Error, Compensation, Signal |

The serializer uses separate start, intermediate and end wrappers. Payload
selection is kind-based rather than dependent on collection order. A successful
patch does not accredit a multiple-event execution strategy in the simulator.

## Timers and conditions

`NativeEventTimer` supports these explicit durable representations:

| `Kind` | `Text` | Effect |
| --- | --- | --- |
| `None` | Empty string | Clear the timer value |
| `Cycle` | Native `R.../...` form, such as `R3/PT5M` | Persist cycle text |
| `Date` | Exact `yyyy-MM-ddTHH:mm:ss`, such as `2026-09-07T10:00:00` | Persist a zone-less date/time |

Cycle validation checks the native durable form, **not complete ISO-8601 syntax,
expression evaluation or timer firing**. Date input deliberately excludes zones
and fractional seconds because the inspected native persistence path normalizes
them away. Locale-dependent numeric/unit forms are not accepted by this contract.
The native enum's existence does not establish `timeDuration` persistence, so
duration is not advertised as writable.

Conditional `Condition` stores native expression text; it does not execute that
expression. No external services or native expression engines are invoked to
interpret supplied condition or timer text.

## Compensation references

`NativeCompensationPatch.ActivityId` targets an existing native activity in the
same flow container and diagram. An empty ID clears the target; omission retains
it. `WaitForCompletion` is an independent nullable Boolean patch.

The adapter updates the native object, BPMN QName and catalog representations
together. The installed XPDL loader restores a QName while persistence reads an
activity object. On load, the adapter resolves only a unique exact same-container
identity with consistent aliases. Unresolved references are not guessed.

Deleting a referenced activity fails until its compensation events are explicitly
cleared, redirected or deleted. Native diagram cloning remaps targets through the
native clone identity map, including nested activity targets. It does not point a
cloned event back into the source diagram.

The installed collaboration cloner may share compensation definition objects.
Before remapping, the adapter detaches the target collection and invokes the
definition's native `ICloneable.Clone` method. Both the original diagram and
the clone remain covered by independent durable fidelity checks.

`NativeEventDefinitionInfo.Compensation` reports `ActivityId`, `BpmnName`,
`BpmnNamespace`, `CatalogActivityId` and `WaitForCompletion` separately. This makes
unresolved or inconsistent aliases observable instead of hiding them behind one
synthetic ID. Successful reference persistence does not prove compensation runs.

## Fidelity and readback

Every mutation runs on a staged native copy, persists through Bizagi, and reloads
in a separate worker before the result can be accepted. `NativeEventPayloadPolicy`
checks requested values against this readback and projects only the requested
native fields for whole-archive comparison.

- Message IDs and unrelated fields remain compared.
- Link names are checked against exact native XML NMTOKEN encoding using
  [`XmlConvert.EncodeNmToken`](https://learn.microsoft.com/en-us/dotnet/api/system.xml.xmlconvert.encodenmtoken).
  This does not decode or normalize unrelated fields during comparison.
- Timer attributes are distinct from same-named expression child elements.
- Unknown attributes, namespaces, siblings and definition content are not waived.
- Replacing conditional text refuses unknown expression attributes or child nodes.
- Compensation cloning projects only exact native event reference locations.
- An unexplained durable change fails; the original file is not replaced.

The structured observation is intentionally not a complete serialization of every
possible definition extension. Archive fidelity protects content outside it.

## Example and operator acceptance

[Example payload patches](../examples/native-event-payloads.json) contain
illustrative GUIDs. Replace event and activity IDs with actual `native_inspect`
identities; supply the current artifact path and SHA-256 revision to
`native_mutate`. Use [native commit](native-commit.md) for deliberate adoption.

```powershell
dotnet run --project tests/McpBizagi.Acceptance -c Release --no-build -- . --native --event-payloads-only
```

The real-client corpus covers Unicode payload creation, nonempty updates,
unrelated edits, timer changes, clearing, native multiple defaults, nested
compensation targets, guarded deletion, wrong-kind/cross-container failures,
clone remapping, offscreen rendering, Word publication and fresh-worker no-op
readback. Consult [recorded validation](validation.md) for actual completed runs.

Collection editing, event-type conversion, full message/reference semantics,
event execution, independent Modeler GUI compatibility and complete desktop
automation remain separate acceptance requirements. Current source additions
are not retroactively included in an older release archive.
