# Native presentation actions

This is an experimental **current-source** integration for the installed
Modeler 4.3.0.008 engine. It is not included in the immutable 0.6.0-alpha.1 ZIP.
Operational scope is limited to the circuits recorded in [validation](validation.md).
Editing action definitions is not full Modeler automation or proof of GUI playback.

## Inspect first

Call `native_presentation_get` with a confined `.bpm` path or native artifact URI,
then poll `operation_get`. `OperationView.Result.sourceRevision` is the captured
file SHA-256. `result.Presentation.Actions` contains native diagram/owner IDs,
type, value source, optional attribute ID, display name and content.

Bizagi identifies an action by its **owning graphical element**, not an
independently allocated action ID. There is at most one represented action per
owner. Existing duplicate or unrepresented action records fail the write gate.

`result.Presentation.Files` reports hashes and lengths for normal embedded action
files. These are distinct from extended-attribute attachments. Links and linked
files are stored as literal strings: inspection never fetches or activates them.

## Explicit replacement or deletion

Call `native_presentation_apply` with `path`, `expectedRevision` and `changes`.
Each change has `Operation` (`upsert` by default, or `delete`), `Action`, and an
optional `DataBase64` payload. Upsert replaces the complete selected action at
its existing list position; new actions append. It is not a sparse field patch.

```json
{
  "path": "<native path or artifact URI>",
  "expectedRevision": "<sourceRevision from inspection>",
  "changes": [{
    "Action": {
      "DiagramId": "<native diagram GUID>",
      "ElementId": "<native graphical owner GUID>",
      "Type": "Text",
      "TypeValue": "Normal",
      "DisplayName": "Review guidance",
      "Content": "Original instructions for this element."
    }
  }]
}
```

The [request template](../examples/native-presentation.json) contains placeholders;
it is not a runnable native model. Replace them with actual inspection results.

| `Action.TypeValue` | `Action.Type` | Content contract |
| --- | --- | --- |
| `Normal` | `None` | Empty content, no payload |
| `Normal` | `Text`, `Link` | Literal content; no payload |
| `Normal` | `File`, `Image` | `action-file:<filename>` plus explicit `DataBase64` |
| `Description` | `Text` | Empty input content; native selected-owner description resolver |
| `ExtendedAttribute` | `Text` | Actual Text, LongText, Number or Date attribute |
| `ExtendedAttribute` | `Link` | Actual Link attribute |
| `ExtendedAttribute` | `File` | Actual FileEmbedded or FileLinked attribute |
| `ExtendedAttribute` | `Image` | Actual Image attribute |

Extended-attribute actions require `Action.ExtendedAttributeId` and an existing
value on that same owner. Use `native_attributes_get` to inspect these IDs.
Referenced content is obtained through the installed selected-action resolver;
clients cannot supply a competing cache. Referenced embedded content is exposed
as `attachment:<filename>`. Other action caches are not implicitly refreshed.

For deletion, supply `Operation: "delete"` and only `Action.DiagramId` and
`Action.ElementId`. Delete is not equivalent to a retained `Type: "None"` action.

## Files and integrity

- Each batch has 1–1000 distinct owners. IDs must be canonical native GUIDs.
- Normal action payloads allow at most 8 MiB per file and 32 MiB per batch.
- Use a plain Windows filename, not an external path. If its stem is a GUID,
  it must identify the same owner because native persistence reserves that form.
- Images are decoded in the worker before persistence, without re-encoding, and
  use the existing native image bound of 64 Mi pixels.
- Existing unrelated/shared payloads cannot be overwritten with different bytes.
  Deletion retains a file still referenced by another normal action.
- Files under native `Actions/` remain opaque bytes, including `.xml` and `.diag`
  names. Extended-attribute files remain in their separate native `Files/` tree.
- Native scratch-path relocation is compared only for proven archive-owned file
  references. Literal text, links, unknown XML and binary bytes stay exact.

The workflow captures the source, reads through one worker, applies explicit
intent through a second worker, persists through Bizagi, and reopens through a
third process. Returned `edited`, `reopened`, `fidelity`, `sourceRevision`,
`outputArtifact` and `outputRevision` distinguish observations from durable output.
Unexplained archive changes reject the result. The source is not overwritten;
adopting a verified copy remains a separate revision-checked commit operation.

## Boundaries

This does not activate presentation playback, open linked applications, fetch
remote URLs, edit unsaved desktop documents, or migrate actions between diagrams.
Cross-diagram reparenting still rejects affected nonempty action collections.
Changing an owner's description/attribute elsewhere is not an automatic promise
that its presentation cache has been refreshed: explicitly upsert that action.
Independent Modeler GUI interpretation and broader existing-model corpora require
separate acceptance. Do not infer them from successful serialization.

## Reproduce the installed-engine circuit

After the documented source build on the supported Windows installation:

```powershell
dotnet tests/McpBizagi.Acceptance/bin/Release/net10.0-windows/McpBizagi.Acceptance.dll . --native --presentation-only
```

This independent client uses real MCP stdio calls and native-produced models and
images. It records actual operations, fresh-reader results, archive bytes, errors,
worker exits and sampled desktop observations in ignored local evidence. Pure
policy tests are separate and do not establish native functionality.
