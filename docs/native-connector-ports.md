# Native connector port metadata

This is an experimental **source capability after the immutable 0.6 package**.
It extends `native_inspect` readback and `native_mutate`; it is not a separate
layout engine or a claim of full Modeler automation.

## Explicit contract

`NativeElement.SourcePort` and `NativeElement.TargetPort` expose the exact native
string values. Unknown imported identifiers remain visible, not normalized.
Null represents an absent value or an element without connector ports.

For connector creation and `reconnect`, `NativeMutation` accepts:

| Field | Omitted or null | Empty string | Nonempty string |
| --- | --- | --- | --- |
| `SourcePort` | Preserve source port | Clear source port | Replace source port |
| `TargetPort` | Preserve target port | Clear target port | Replace target port |

The operation still requires `SourceId`, `TargetId` and the complete `Points`
sequence. Node updates and deletions reject port fields. Supported connector
classes are `SequenceFlow`, `Association` and `MessageFlow`.

The installed 4.3 editor's port identifiers use canonical decimal strings from
`"0"` through `"74"`. Noncanonical strings, signed numbers and unknown identifiers
are rejected for new writes. Existing unknown values are preserved when omitted.
This validation is a vocabulary bound, **not proof that every identifier is
meaningful for every endpoint shape**.

Midpoint identifiers are `"1"` (top), `"2"` (bottom), `"3"` (left) and `"4"`
(right). Offset identifiers are shape-dependent. `"0"` is the native unmapped
value; it is not the same thing as an absent port. Future versions must repeat
native acceptance rather than assuming the same mapping.

## Coordinates are separate

Port metadata does **not** calculate or move route points. Supply a route whose
endpoints agree with the intended shape ports, in the native container's
coordinate system. A geometrically valid path does not automatically establish
correct port metadata, and a metadata edit does not establish correct routing.

For example, when an existing task-to-task flow already exits the right midpoint
and enters the left midpoint, its reconnect request can include:

```json
{
  "Operation": "reconnect",
  "ElementId": "11111111-2222-4333-8444-555555555555",
  "SourceId": "22222222-3333-4444-8555-666666666666",
  "TargetId": "33333333-4444-4555-8666-777777777777",
  "SourcePort": "4",
  "TargetPort": "3",
  "Points": [{ "X": 320, "Y": 150 }, { "X": 500, "Y": 150 }]
}
```

Replace the illustrative identities and geometry with the actual inspected model.
Use `native_mutate(path, expectedRevision, mutations)` with the current SHA-256
revision. The request writes a new artifact, never the original input.

## Fidelity and acceptance

The adapter updates the installed native connector properties. A second worker
loads the durable `.bpm` and verifies requested port values. The archive comparator
also verifies the exact native `ConnectorGraphicsInfo.FromPort`/`ToPort`
attributes before projecting only those requested changes out of comparison.
Opposite-end ports, unknown metadata, coordinates and unrelated archive content
remain protected. Clearing uses the native absent value, not a fabricated default.

The independent protocol acceptance entry point is:

```powershell
dotnet run --project tests/McpBizagi.Acceptance -c Release -- D:/MCP-Bizagi --native --connector-ports-only
```

See [execution records](validation.md). Unit policy tests are separate from this
installed-engine circuit. The global-layout candidate, anchored-event placement,
all offset-port geometries and independent desktop compatibility have their own
pending acceptance requirements; they are not accredited by this metadata test.
