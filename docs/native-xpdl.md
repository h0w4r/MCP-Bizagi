# Native XPDL interchange

`native_xpdl_export` and `native_xpdl_import` use the installed Modeler
4.3.0.008 XPDL 2.2 manager. They do not construct an exchange file by hand,
rewrite native archive XML, automate clicks or replace the native `.bpm` source.

This is an **experimental file-based capability**, not full Modeler automation.
The implementation is newer than the immutable 0.4.0-alpha.1 release ZIP.

## Explicit format boundary

**XPDL is not a native backup.** Both tools require
`acknowledgeFormatLimits: true`. This acknowledges a reviewed interchange
projection; it does not waive unexpected changes during native persistence.

Observed on the local rich test model:

- Unicode diagram/task names and extended text survive the Unicode file route.
- Selected diagrams, nested tasks, subprocesses and connections are loaded
  through the installed engine; only selected diagrams are exported.
- The tested role and all four activity RACI assignments survive exchange.
- Extended definitions are recreated with new identities as `LongText`, not
  retained as the original `Text` or `FileEmbedded` types.
- Embedded attachment **bytes are not included**. An exported attribute can
  contain a local extraction path as ordinary text instead of a portable file.
- Native simulation scenario content can be omitted, and graphical properties
  such as label alignment can change.
- The installed importer normalizes some uppercase tokens globally, including
  occurrences inside user text. Differences remain visible in the result.

These are tested observations, not an exhaustive specification of every XPDL
element. Unknown changes are included in XML/native archive comparison rather
than hidden behind element-count equality.

**Review before sharing.** Native exports, attributes and operation results can
contain private text, author metadata and local filesystem paths. The server
does not silently sanitize those values or present a modified file as an exact
vendor export. Public examples contain placeholders, never operator data.

## Export request

Call `native_inspect` first to obtain native diagram IDs and the source SHA-256.
Use `native_xpdl_export` with:

| Field | Requirement |
| --- | --- |
| `path` | Workspace `.bpm` path or completed native model artifact |
| `expectedRevision` | Exact SHA-256 of the source bytes |
| `diagramIds` | 1–100 distinct existing native collaboration GUIDs, in desired output order |
| `acknowledgeFormatLimits` | Must explicitly be `true` |

The operation stages the source, starts an exporter, and writes one
`<diagram-guid>.xpdl` artifact per selected diagram. The actual native file
serializer preserves Unicode; the installed ASCII memory-stream overload is
intentionally not used. Output names do not depend on diagram labels.

A second worker imports all exported files and saves a verification `.bpm`.
A third worker reads that durable file. Native graph, keyed metadata,
documentation XML and attachment receipts must remain equal across this
save/restart boundary. Missing snapshots are failures, not evidence.

The completed `Result` includes:

- `files[]`: native `DiagramId`, reusable `artifact`, SHA-256 `revision`, byte count.
- `exported`, `imported`, `reopened`: actual native worker snapshots.
- `graphDifferences`: complete observed element-record differences across exchange.
- `metadataDifferences`: resource/RACI/scenario/definition/value/file differences.
- `nativeRoundtripComparison`: whole-container comparison of the original
  `.bpm` against the re-imported `.bpm`, including unselected source diagrams.
- `verificationModel`, `verificationRevision`: a separate native verification
  artifact, not a replacement for the source.
- `warning`: explicit interchange and sharing boundaries.

`nativeRoundtripComparison.Preserved: false` is an expected possible **exchange
outcome**, not permission to claim losslessness. Review each difference before
using the output. A successful native execution and content fidelity are
different facts.

## Import request

Call `native_xpdl_import` with `inputs`, `acknowledgeFormatLimits: true`, and
an optional `modelName`. Each `NativeExchangeInput` contains:

| Field | Requirement |
| --- | --- |
| `Path` | Confined workspace `.xpdl` path or `artifact:<operation-id>:<diagram-guid>.xpdl` |
| `ExpectedRevision` | Exact SHA-256 of those XPDL bytes |

Artifact references must belong to a completed `native_xpdl_export`. Private
diagnostic paths, in-progress operations and other artifact families are not
accepted by this resolver. All bytes are captured before queueing, with a
128 MiB aggregate input limit; the workspace reader also bounds each file.

Preflight requires a matching XPDL 2.2 namespace and `PackageHeader/XPDLVersion`.
DTD/entity resolution, processing instructions, XInclude, external packages and
excessive XML size/nesting are rejected. This is bounded input validation,
**not complete XPDL XSD or behavioral validation**. Other syntax/engine errors
remain actual failed operations.

The importer adds each native collaboration to a new model, records its input
mapping, rejects ambiguous identities, saves `.bpm`, and reads it in another
process. A third worker re-exports the imported diagrams in receipt order.
Results include per-input XML differences, source/roundtrip hashes and a new
`outputArtifact`/`outputRevision` pair. Labels and incidental enumeration order
are never used to match input files to output diagrams.

Definition catalog enumeration is matched by identity; explicit native XML is
compared without stripping user content. XML difference reporting ignores
serializer indentation only, retains mixed/`xml:space` text and comments, and
does not silently remove unknown elements or attributes from the comparison.

## Recovery and publication

Both tools return an operation ID. Poll `operation_get` until terminal;
`running`, `cancelling` and a created file are not success receipts.
`operation_cancel` applies to the owned worker, never the operator's Modeler.
There is no operation-wide fixed timeout while real progress continues.

Failed attempts retain private staged inputs and diagnostics. They do not
publish over the source, turn into a BPMN fallback, or blindly retry a write.
Only a completed result can be reused as an artifact input. If a reviewed
native import should become a workspace file, use the separate revision-checked
[`native_commit`](native-commit.md) contract.

## Reproduce the real circuit

```powershell
dotnet build -c Release
dotnet test tests/McpBizagi.Core.Tests -c Release --no-build
dotnet tests/McpBizagi.Acceptance/bin/Release/net10.0-windows/McpBizagi.Acceptance.dll . --native --xpdl-only
```

The independent client uses MCP stdio, the actual installed serializer/importer,
native durable files and fresh workers. It authors its own two-diagram model,
Unicode/nested graph, role/RACI, extended values and attachment through native
MCP tools. It checks selective export, reusable artifacts, no-op persistence,
stale revisions, forbidden XML, duplicate diagram import, native failure and
subsequent recovery. Unit parser tests are reported separately.

See [the request examples](../examples/native-xpdl.json) and
[the execution ledger](validation.md). No Bizagi binaries or private library
dumps are part of this repository. Independent desktop visual compatibility,
live unsaved documents, other XPDL versions and other exchange formats remain
separate gates.

## Primary vendor references

- [Modeler XPDL export](https://help.bizagi.com/platform/en/xpdl_for_attributes.htm)
- [Modeler XPDL import](https://help.bizagi.com/platform/en/import_from_xpdl.htm)
- [Exchanging processes](https://help.bizagi.com/platform/en/exchanging_processes.htm)

The local execution receipts, not an API's existence or documentation alone,
determine the integration's accredited scope.
