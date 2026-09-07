# Native diagram lifecycle

Current source adds `native_diagrams_get` and `native_diagrams_apply` for the
installed Modeler 4.3.0.008 engine. This is file-based, copy-only automation, not
control of a running desktop session. These tools are not in the existing
0.4.0-alpha.1 release archive.

## Read and write contract

1. Call `native_diagrams_get` with a workspace-relative native `.bpm` path or a
   completed native artifact reference.
2. Poll `operation_get` until the operation completes. Read `Result.sourceRevision`
   and `Result.result.DiagramState`.
3. Use `NativeDiagramSnapshot.Diagrams` for actual native diagram IDs and names.
   `OpenedItems` is the persisted ordered tab-preference list. It is not the order
   in which files happen to be enumerated inside the ZIP or on disk.
4. Submit `native_diagrams_apply` with `path`, `expectedRevision` and `patch`.
5. Poll the operation. A successful result includes a new `outputArtifact`, its
   `outputRevision`, the native editor receipt, an independent reader result and
   a whole-archive `fidelity` report. The source remains untouched.

`NativeDiagramPatch.Changes` is a list of up to 100 explicit operations:

| `NativeDiagramChange.Operation` | `DiagramId` | `Name` | Behavior |
| --- | --- | --- | --- |
| `create` | Caller-generated fresh GUID | Required | New native diagram with main and visible pools/processes and a default scenario |
| `rename` | Existing diagram | Required | Renames the native package and verifies its derived header description |
| `clone` | Existing source diagram | Required | Uses installed domain cloners and returns new identities |
| `delete` | Existing diagram | Must be absent | Removes that diagram, not the final diagram in the model |

Each diagram target can occur once per batch. Names must be unique ignoring case
and must be safe export labels. Missing targets, duplicate names and ambiguous
identities fail explicitly; they are not silently repaired or renamed.

`create` materializes native defaults before persistence, including the native
main-pool dimensions and empty scenario/attribute collections. This avoids a
second save changing the supposedly stable new document just because the native
reader initialized a collection.

## Persisted tab order and selection

`NativeDiagramPatch.OpenedItems` has three distinct meanings:

- Omitted or `null`: preserve existing preferences.
- Empty array: explicitly clear the opened-item list.
- Nonempty array: replace the complete list, in the supplied order.

Each `NativeOpenedItem` contains `DiagramId`, optional `SubProcessId` (empty for a
diagram tab), and `IsSelected`. At most one item can be selected. Duplicate items
are rejected. A subprocess must belong to the specified diagram.

Deleting an opened diagram requires an explicit replacement list that excludes
it. Tab preferences are checked against both fresh-worker state and durable XML.
Only the installed engine's exact default/current-user preference entries can
be projected during comparison. Other users' preferences, unknown fields,
comments and unrelated settings remain part of the fidelity check.

`NativeDiagramSnapshot.PreferenceEntries` contains the archive-relative native
write scope. It can reveal the local user identifier: keep raw diagnostic output
private, just as with other native model metadata.

These operations do not change currently visible tabs or unsaved documents.

## Clone identities and fidelity

The native cloner generates IDs. Do not assume that a requested or source ID will
be reused. Read `Result.edited.DiagramClones`:

- `NativeDiagramClone.SourceId`: the original diagram.
- `NativeDiagramClone.TargetId`: the durable copied diagram.
- `NativeDiagramClone.Identities`: original/copied native IDs and BPMN references.

The map must form a bijection over the durable source identities. A separate
worker must find every mapped target. The comparison reverses only known native
identity/reference fields at known XML locations, then compares the entire copy
with its source. Unknown same-named extension nodes are not identity aliases.

Native attribute-value and attachment cloners are reused. Explicit empty value
containers and their collection order are retained instead of silently omitted.
Attachment bytes are compared independently of their changed owner/scratch
paths. Scenario state is separated from the source object, and native element
references are mapped to the copied elements. The original diagram and every
unrelated archive entry are checked too.

If a native component drops or changes something the policy cannot explain,
the operation fails and retains the request, reader output and diagnostic
artifacts. An internal cloner returning an object is not sufficient evidence.

A batch cannot refer to a clone's not-yet-known ID in `OpenedItems`. Submit a
second revision-checked operation after reading its actual `TargetId`.

## Reproduce acceptance

From a configured Windows checkout with the supported local installation:

```powershell
dotnet build -c Release --no-restore
dotnet test tests/McpBizagi.Core.Tests -c Release --no-build
dotnet run --project tests/McpBizagi.Acceptance -c Release --no-build -- `
  . --native --diagrams-only
```

Use `--input <own-rich-model.bpm>` to exercise an existing native model, and
`--package <extracted-package-directory>` to use the distributed host and worker.
Raw evidence stays under the ignored `.local/acceptance` directory. See
[validation](validation.md) for actually completed runs, rather than treating
this command's presence as accreditation.

`--configured-clone-simulation` is an additional acceptance-corpus option: use
the native model produced by `--metadata-only`, before its final RACI-clearing
transaction. The test requires that corpus's resource/calendar scenarios and
checks quantitative simulation behavior after cloning. It is not an option to
invent scenarios or expected results for an arbitrary input.

The circuit uses a real MCP stdio client, the isolated worker and installed
engine. It creates, orders, renames, copies, retabs, saves without changes,
renders and deletes diagrams, and checks expected failures and recovery.
Unit tests exercise comparison logic only; they do not replace that circuit.

## Remaining boundaries

The [local reusable-call contract](native-calls.md) adds tested native reference
remapping and protected deletion. External calls, every relationship/palette variant, all rich
publication settings and saved simulation-result clone cases require their own
acceptance corpus. Unknown content is not waived merely because it is outside
the current graph model. Independent desktop visual compatibility and live
unsaved-session integration remain separate gates. Full Modeler automation is
not declared complete by the addition of this tool family.
