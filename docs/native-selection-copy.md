# Native selection copying

**Experimental source integration, not full Modeler automation.**
`native_elements_copy` invokes the installed native paste/clone command against
an independently loaded model snapshot. It does not use the OS clipboard,
simulated input, foreground focus or an operator-owned open document. The
immutable `0.5.0-alpha.1` package does not contain this subsequent source change.

## Explicit operator contract

Supply `path`, its SHA-256 `expectedRevision`, and `selection`:

| `NativeSelectionCopyRequest` field | Meaning |
| --- | --- |
| `SourceDiagramId` | Actual source collaboration identity returned by inspection |
| `TargetParentId` | Actual destination process or embedded subprocess identity |
| `ElementIds` | 1–1,000 distinct native selection roots; descendants are included |
| `Position.X`, `Position.Y` | Requested selection origin in the destination canvas |

Use canonical nonempty native GUIDs, not BPMN IDs, labels or guessed names.
Roots must share a coordinate canvas; do not select an ancestor and its child
again. Include both endpoints of copied connectors and the targets of boundary,
default-flow, data and compensation references. A selection that cannot be
represented faithfully is rejected rather than reduced to its supported subset.
Participant partitions and diagram catalogs are not part of this copy contract.

The position is explicit even when the desired copy overlaps the original.
For connectors, source placement is derived from vertices, not their zero-size
graphical box. Only selected roots are translated; nested subprocess canvases
retain their local coordinates. Placement uses the native engine's single-precision
coordinates. Fractional connector/manual-label offsets that
the native engine would round are rejected. Geometry that makes the native
command choose a different parent fails the explicit destination check.

Poll `operation_get` with the returned operation ID. `operation_cancel` cancels
only owned work. Successful output is a new staged artifact and revision; it
never replaces the supplied source. Publishing/replacing an operator file uses
the separate guarded commit workflow. Do not retry an interrupted write blindly.

## Verification boundary

1. Read and capture the original bytes only after checking their revision.
2. Read the source through an installed-engine worker and derive selection closure.
3. Run the native command in another worker, using a separately loaded snapshot.
4. Verify actual inserted objects, not just the native cloner's temporary objects.
5. Preserve full native event definitions, exact encoded images, owned I/O and
   native reference caches through existing adapter contracts.
6. Persist `.bpm` through the installed persistence manager.
7. Load the result in a fresh worker and compare editor/readback graphs and metadata.
8. Independently check every copied XPDL record, mapped identity, parent, reference,
   root translation, extended-attribute value and source-owned image/attachment.
9. Compare the entire original archive after removing only verified copy additions
   from an **in-memory comparison projection**, never from the actual model file.

Unknown XML is retained and compared. Only exact native identity/reference slots
are remapped for comparison; arbitrary GUID-like text, extension attributes and
unknown payloads are not rewritten. A copied image/attachment must be present
with exact source bytes; scanning output additions alone is insufficient.
Inherited namespace bindings and `xml:space`, `xml:lang` and `xml:base` context
must also match; identical QName text must not silently change meaning in its
destination. Different inherited contexts are conservatively rejected.

`NativeSelectionCopyReceipt.Identities` contains actual source/target native and
BPMN identities, including owned I/O nodes. The receipt does not define success:
the host checks it against source-derived closure and durable native records.

## Evidence and diagnostics

The operation retains `copy-request.json`, `copy-readback.json`,
`copy-fidelity.json`, native worker request/reply/progress, process exits,
installation fingerprints and desktop observations. A failed fidelity check
quarantines output and keeps the source. Test doubles establish only comparison
policy behavior, never installed-engine or MCP operation.

Run the reproducible basic corpus from a Release build:

```powershell
dotnet tests/McpBizagi.Acceptance/bin/Release/net10.0-windows/McpBizagi.Acceptance.dll . --native --selection-copy-only
```

It creates a native model and Unicode tasks/connection using MCP, copies the
selection, checks stale-revision and absent-selection failures, cancels a live
native reader and runs a successful copy afterward. Caller-owned native corpora
can be tested with `--input <model.bpm> --selection-request <request.json>`;
the acceptance client copies the input inside its own workspace before use.
The JSON file contains a `NativeSelectionCopyRequest`, not an engine method name.

## Installed-engine MCP acceptance — 2026-09-08 UTC

Seven real stdio MCP corpora passed on installed **4.3.0.008**. Each completed
an initial copy and a successful copy after a missing-selection rejection and
active native-reader cancellation. A stale-revision tool call was also rejected
in each corpus. These are **14 completed copies, seven expected failed operations
and seven cancellations**, not 28 successful edits.

| Corpus | Run | Native identities per copy | Transcript SHA-256 |
| --- | --- | ---: | --- |
| Nested compensation targets | `20260908-113317-e4f9fc` | 12 | `d1614b5262639d99533c99d6aca8624b6b655174306f192bff1eebcf3648210b` |
| Data object, task and association selected separately | `20260908-113601-08275a` | 5 | `6d6a1c02377c581734a6d9c8c6924bd9cdab5d6040bf80103930c6c65f67c66f` |
| Nested subtree across diagrams | `20260908-112544-811427` | 11 | `68089179fe0a3319aa41f7383ce5042790a720d8cf56daa6edb9c8583db24189` |
| Subtree into an existing embedded canvas | `20260908-112942-45f337` | 11 | `d0d55c856cd02433ebfaff17bd9f5d02107e36bf3e087b8bab5da7419abe41e6` |
| Four nonempty event definitions | `20260908-113156-4fb819` | 1 | `c60d6a15b31254e7832785d723bea40a507fb1af36400396ed9d310a3699dba9` |
| Task roots and connection | `20260908-113701-da98a2` | 4 | `01faeb4806616b2c4f0d87899e8b00abc317be2e0f794cbe24296e2e504cd113` |
| Nonempty Date timer | `20260908-113050-c6f4d7` | 1 | `95aee8f8da6105ae9baacb7e8169af9b134288c5994a67c29b3f68b61b264489` |

All 56 workers exited. The 864 periodic read-only desktop samples observed no
owned visible window or foreground ownership. This is sampled evidence, not
continuous observation or independent GUI compatibility accreditation.

The nested corpora checked exact image/attachment bytes and unchanged original
content. The separate-root data case verified five durable identities: three
selected graphical roots plus the task's owned input and data association.
The compensation corpus preserved mapped targets rather than original aliases.

The initial promoted verifier rejected native formatting whitespace left after
removing copied attribute records from its comparison projection. The correction
restores only an exactly empty known value root with namespace declarations;
unknown attributes, comments, extension payloads and xml:space remain compared.
The earlier rejected runs remain private diagnostic evidence, not positive gates.

Separately, **999 unit/component tests** passed (29 added for copying). The real
non-native MCP XML circuit `20260908-112651-42b781` passed with **41 tools**
enumerated. Enumeration is not accreditation of every tool or argument combination.

The final caller-independent basic corpus `20260908-113751-67a67f` also passed: four
completed native operations (creation, mutation and two copies), one expected
failure and one cancellation. Transcript SHA-256:
`0cecc6b2fda84bd1b5b3491af516a2169a37abdca44217d4dc11de783f946e65`.

The shared data cloner regression `20260908-110852-3909c7` passed **16 completed
operations and four expected failures**, including full diagram cloning, native
I/O lifecycle, actual SVG rendering, Word publication and fresh readback. All
35 workers exited; 1,503 periodic desktop observations found no owned window
or foreground takeover. Transcript SHA-256:
`a46d78adfc06a9b7450dbc7d3bc3ed96111626aa1c871d3c374cf4f46f29b07c`.

## Boundaries that remain separate

- This is local file-based selection copying, not synchronization with unsaved
  documents, an OS clipboard bridge, cut/paste or universal editor automation.
- Preserving original scenario/calendar/action content does not establish that
  every selected simulation or presentation configuration is duplicated.
- Native readback does not establish desktop visual or simulation equivalence.
- Arbitrary selection combinations, reusable calls, catalog/partition copying,
  new Modeler versions and packaged distribution require their own evidence.
- No native libraries, private operator models or diagnostic dumps are shipped.
