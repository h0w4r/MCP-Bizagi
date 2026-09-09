# Native embedded-subprocess extraction

`native_subprocess_extract` runs the installed Modeler 4.3.0.008
`RefactorElementsCommand` for an ordinary embedded subprocess. It produces a
reusable call at the original identity and moves the subprocess content into a
new native diagram. This is not BPMN regeneration, clipboard automation, or a
series of desktop clicks.

The capability is **experimental and file-based**. It does not complete full
Modeler automation and is not included in the earlier 0.4.0-alpha.1 release ZIP.

## Request

First inspect a native model with `native_inspect`. Submit:

| Field | Meaning |
| --- | --- |
| `path` | Workspace `.bpm` path or completed native model artifact |
| `expectedRevision` | Exact source SHA-256 from inspection |
| `extraction.ElementId` | Existing ordinary embedded `SubProcess` GUID |
| `extraction.NewDiagramName` | New, unique, safe native export label |

One request extracts one subtree. The source can be at process level or nested
inside another embedded subprocess. The generated diagram, hidden participant
and process identities come from native constructors and are retained in the
receipt; descendant IDs are not regenerated.

The [JSON example](../examples/native-refactoring.json) contains placeholders.
An accepted request returns an operation ID, not a synchronous success claim.
Poll `operation_get` and inspect its terminal state and result.

## Native execution and preservation

Three independent workers participate:

1. Read the captured original through the native loader, including metadata,
   documentation, images and persisted tab preferences.
2. Execute the installed refactoring command, relocate associated content,
   preserve collection order and graphical fields, then persist a new `.bpm`.
3. Load that durable `.bpm` and verify its actual state after process restart.

The adapter supplements specific gaps in the installed command:

- Native catalog and QName call representations are made consistent before
  persistence, rather than exempting their difference after restart.
- Immediate flow/artifact collection order and omitted label fields are retained.
- Nested image sidecars and complete attachment owner folders are relocated
  within isolated native scratch storage. The workspace source is not moved.
- Existing native extended-value objects move between diagram dictionaries;
  embedded file references are relocated, while linked files are never opened.
- Current-user persisted tabs referencing the extracted root become a tab for
  the new diagram. Tabs for moved nested subprocesses receive the new diagram
  ID. Their order and selected state are retained without opening a GUI.

The comparison policy verifies the complete descendant closure, new native
target identities, call reference, unchanged incident flows and every observed
original graph field except the intended type/containment changes. It then
reverses those verified XML/file moves **in comparison copies only** and checks
all original archive leaves. Unknown original changes are not silently ignored.
The generated diagram's scope is distinguished from original content; arbitrary
additional target payload files, extra native identities and unexpected
scenario/presentation content are rejected.

The original subprocess's identity, name and description remain on the call
shape. Descendant objects retain their IDs, names, geometry and metadata.
The new diagram/process are native-generated containers, not copies of all
source-diagram settings. Other users' stored preferences are unchanged; this
is not multi-user live-session synchronization.

## Results and failure handling

A completed `Result` contains:

- `before`, `edited`, `reopened`: actual worker observations.
- `edited.Extraction`: source element/diagram, target diagram/participant/process,
  and exact moved descendant IDs.
- `fidelity`: original-container preservation after verified relocation.
- `outputArtifact`, `outputRevision`: a separate native result file.
- `nativeSourceUnmodified`: the captured source is not overwritten.
- `extractionInterpretationWarning`: execution and compatibility boundaries.

Unexplained changes fail the operation and quarantine the output. A failed,
cancelled or interrupted artifact cannot become another operation's input.
Diagnostics remain private. Use `operation_cancel` for owned work and never
blindly replay a write after a disconnect. Workspace publication remains the
separate [`native_commit`](native-commit.md) operation.

## Explicit semantic boundaries

**Reusable calls are not equivalent to embedded execution in Modeler's
simulator.** The installed simulator treats them as black boxes; extracted child
activities are not automatically executed through the call. Persistence and
rendering checks do not prove behavioral equivalence or GUI compatibility.

The current request rejects rather than silently interpreting:

- Transaction, ad hoc or event-triggered subprocess extraction.
- Configured child BPSim `elementRef` inputs requiring scenario migration.
- Source diagrams containing presentation actions requiring their own migration.
- Subprocess-owned extended values whose definitions do not explicitly include
  `CallActivity`. Such definitions can be deliberately updated with
  `native_attributes_apply` before extraction; global scope is not silently
  broadened by refactoring.
- Unrepresented activity-set semantics, shared Enterprise models, a stale
  revision, an absent/wrong element kind, or a colliding diagram name.

Arbitrary selection extraction and scenario/action migration remain separate work.
[Local reusable-body inlining](native-inlining.md), task/call conversion and explicit
[native reparenting](native-reparenting.md) have their own tools and contracts;
the latter adds guarded cross-diagram content migration in source after the
immutable 0.6.0-alpha.1 package. They are not implemented by this extraction tool. This
contract does not redefine the project's full-automation objective around the
implemented subset.

## Independent acceptance

```powershell
dotnet build -c Release
dotnet test tests/McpBizagi.Core.Tests -c Release --no-build
dotnet tests/McpBizagi.Acceptance/bin/Release/net10.0-windows/McpBizagi.Acceptance.dll . --native --refactoring-only
```

The real MCP client authors two native diagrams, root/nested subprocesses,
internal and incident external flows, Unicode labels, role/RACI assignments,
extended text, an embedded file and a nested transparent image. It exercises
both root and nested extraction from the unchanged source, checks tab remapping,
performs a native no-op save and native rendering, and verifies rejection and
subsequent recovery. Parser/comparison unit tests are separate evidence.

For a focused rerun over an already authored real model, append
`--input C:/models/extraction-source.bpm`. The client copies the file into its
isolated workspace and runs each ordinary embedded-subprocess extraction
through MCP and fresh workers; unsupported source semantics remain failures.

See [the verification ledger](validation.md) for actual run identifiers and
hashes. Private native artifacts and library inspection dumps are not published.
