# Inline a local reusable subprocess body

**Experimental source integration after 0.6; not live-session automation.**
`native_subprocess_inline` converts one bound local `CallActivity` to an ordinary
embedded `SubProcess` and copies the referenced process's complete closed body.
The shared process and all other callers remain in the model. The immutable
`0.6.0-alpha.1` package predates this tool.

## Explicit request

Supply `path`, its SHA-256 `expectedRevision`, and `inlining`:

| `NativeSubProcessInlining` field | Meaning |
| --- | --- |
| `ElementId` | Native identity of the call to convert |
| `ExpectedProcessId` | Expected existing local process binding; not a diagram ID |
| `Position.X`, `Position.Y` | Explicit origin of the copied body in the embedded canvas |

Native IDs must be canonical nonempty GUIDs obtained through inspection, not
labels, BPMN IDs or guessed identifiers. The tool returns an operation ID;
poll `operation_get` and use `operation_cancel` for owned work.

The result is a **new staged `.bpm` artifact**, never an implicit overwrite.
Use the separate revision-checked publication workflow to replace an operator
file. Failed output remains quarantined and the original file is retained.

## Native execution and fidelity

1. A native reader loads a revision-captured source and derives the body closure.
2. The installed `ChangeElementTypeCommand` converts the call, preserving its
   identity, original ordinal, graphics, label fields and existing references.
3. A fresh native reader verifies the persisted empty embedded container.
4. The existing installed paste/clone integration copies the source body into it,
   including supported nested contents, attributes, attachments and images.
5. Another fresh reader verifies the final durable model and native clone map.

The host verifies the entire native archive at **both stages**. For conversion,
only the exact bound `SubFlow` selector and the new empty `ActivitySet` are
projected in memory for comparison. Unknown implementation content, extra body
data or unrelated edits reject. These comparison projections never write a
model. The copy stage reuses the complete
[selection fidelity contract](native-selection-copy.md), including original
content preservation, copied records, byte-exact payloads and explicit placement.

`receipt.Identities` contains the real native source/target identity map. Original
body identities remain in the shared process; cloned body elements receive new
identities. Only the converted call keeps its identity and changes native type.
The host derives expected content from the source rather than accepting the
worker's receipt as proof of completeness.

## Interpretation and boundaries

- This is **body copying**, not deletion or retirement of the shared process.
  Its process/diagram name, pool identity and process-level metadata remain on
  that source; they are not substituted for the caller's metadata.
- Reusable calls and embedded execution are not behaviorally equivalent in the
  installed simulator. Successful structural conversion is not simulation or
  desktop visual equivalence.
- A call inside its own referenced body requires a separate recursive-expansion
  contract and is rejected. External or unresolved references are rejected.
- Configured simulation inputs referencing the source process/body and source
  presentation actions require explicit migration and are rejected here.
- The entire selected body must meet the existing native selection-copy contract.
  Empty bodies, unrepresented partitions/catalogs, open reference closures and
  unrepresentable placement are rejected, not silently reduced to tasks.
- Caller-owned extended-attribute definitions must explicitly apply to
  `SubProcess`; keeping an inapplicable value is not editor fidelity.
- Editing the disk model does not synchronize an already open editor or its
  unsaved changes. See [live-session boundaries](live-sessions.md).

## Reproduce acceptance

From a Release build with the supported installed engine:

```powershell
dotnet tests/McpBizagi.Acceptance/bin/Release/net10.0-windows/McpBizagi.Acceptance.dll . --native --inlining-only
```

The real MCP client creates two native diagrams and a shared process with two
callers, inlines one, checks source revision and binding failures, cancels an
active native reader, then verifies a successful operation after cancellation.
`--input <model.bpm> --inlining-request <request.json>` runs the same circuit on
a caller-owned corpus copied into the acceptance workspace. The request file
contains the typed `NativeSubProcessInlining`, not an engine method name.

Evidence includes the MCP transcript, `inlining-request.json`, per-stage native
readback and fidelity records, worker fingerprints, process exits and sampled
desktop observations. Test doubles establish comparison policy behavior only.

## Source MCP acceptance — 2026-09-09 UTC

Three real stdio circuits passed on installed **4.3.0.008**. Each completed an
inlining, rejected an incorrect expected process, cancelled an active native
reader and completed another inlining afterward. Each also rejected a stale
source revision at the tool boundary.

| Corpus | Run | New native identities per copy | Transcript SHA-256 |
| --- | --- | ---: | --- |
| MCP-created shared process and two root callers | `20260909-055940-64533a` | 3 | `246811051d2ea04153a5841859477152a345dec3c03dfa08caed331e3c04cb4e` |
| Nested caller, nested body and embedded attachment | `20260909-060217-4adda4` | 5 | `e573b3727c5367b6c1f339a381d528529756c37937923193d41237f378ae4fc5` |
| Nested caller, incident flows and attached timer | `20260909-060321-fc06a4` | 6 | `6e9bf026af53eca0833fd6ecf9b31ebc8c2012fc4a90bafc0e318889abb13b29` |

Together these are **six completed inlinings**, two prerequisite native creation/
mutation operations, three expected failures and three cancellations. All **41
workers and 51 recorded owned processes exited**. The **233 sampled desktop
observations** reported no owned visible window or foreground takeover; samples
are not continuous observation or independent Modeler GUI acceptance.

Both fidelity stages passed for every completed inlining. The protocol client
independently checked all original graph fields outside the four intended caller
type/reference fields, retained shared callers, mapped body ownership and names.
A separate ZIP-byte audit verified the copied embedded attachment in both rich
outputs. This does not establish every image, data-I/O or simulation combination.

Separately, **1,434 unit/component tests** passed, including 24 inlining-policy
cases. The real XML MCP circuit `20260909-060529-e940d6` passed with 49 tools;
enumeration is not accreditation of every tool. Packaging is deferred to the
next consolidated distribution, not retroactively attributed to 0.6.
