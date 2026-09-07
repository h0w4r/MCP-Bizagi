# Native content artifacts

The source adapter exposes explicit native artifact content through
`native_mutate`, with revision checks, private copies, native persistence,
independent worker readback and whole-archive fidelity. This is not an arbitrary
property setter or a conversion of `.bpm` contents into a reconstructed XML file.

## Typed fields and ownership

| Native kind | Durable content | Parent | Important constraint |
| --- | --- | --- | --- |
| `TextAnnotation` | `ArtifactProperties.Text` | Process or embedded subprocess | Plain native text; not `Name` |
| `FormattedTextArtifact` | `ArtifactProperties.Text` | Process or embedded subprocess | Native markup string; not a generic web page |
| `Group` | `Name` and explicit geometry | Collaboration/diagram | Intrinsic `Geometry.Expanded: true`; not a collapsible subprocess |
| `HeaderArtifact` | Native diagram context and documentation | Root process | Content is derived from diagram metadata, not `Name` |

`NativeElement.Artifact` reports the native artifact type, actual text when
applicable, the observed annotation text format and the actual header diagram
identity. Observation of `TextFormat` is not support for editing it: the installed
XPDL route does not durably serialize that field.

Artifact text is sparse: omission preserves existing content and an empty string
explicitly clears it. The adapter writes the installed `TextAnnotation.Text.Content`
or `FormattedTextArtifact.Text` property. It does not alias either to
`DisplayName`, strip markup or silently replace formatted content with plain text.
The field is bounded to 1,048,576 UTF-16 code units and must be XML-representable.

`Name` requests on text annotations, formatted-text artifacts and headers fail
explicitly because this native route does not persist those display names.
Group documentation requests also fail explicitly: the installed group reader
does not preserve that property. Text annotations, formatted text and headers
retain a separate `Documentation` field where the native reader supports it.

## Native geometry, not an implicit move

The native root-process artifact loader selects the first visible pool containing
the artifact's center, otherwise the hidden main pool. Root-process artifact
geometry must therefore remain consistent with its explicit process owner.
An edit that would move an artifact to another process after a worker restart
fails rather than silently changing containment. Nested subprocess artifacts
retain their explicit native activity-set ownership.

Groups are different: they belong to the collaboration's artifact collection.
The installed group reader materializes its intrinsic expanded view and derives
expanded dimensions from visible bounds. Explicit group geometry must set
`Expanded: true`; it does not take a subprocess `ExpandedSize`. A group's geometric
membership is not reinterpreted as semantic ownership of every enclosed task.

Annotations can be connected with native `Association` mutations using actual
same-container endpoint identities and explicit points. Delete or reconnect
incident associations before deleting their referenced artifact. Connections
are not inferred from nearby shapes.

## Independent fidelity checks

- Native creation and updates verify the requested kind, stable identity,
  ownership, text, geometry and documentation after a different worker reloads
  the persisted `.bpm`.
- The native `Artifact.TextAnnotation` XPDL attribute is the exact text carrier;
  it is not an XML child or a license to normalize neighboring extension data.
- A group persists its name and identity twice: on the owning artifact and its
  direct native `Group` child. Only the verified duplicate name and actual clone
  identity references are projected in comparisons.
- Unknown attributes, foreign namespaces, children, comments, unrelated labels,
  file contents and metadata remain outside the requested-change projection.
- Headers must resolve their actual collaboration after reload and map to the
  target collaboration after cloning. No header text is fabricated by the host.

## Usage and acceptance

[The request example](../examples/native-artifacts.json) is a mutation array,
not a complete model. Replace every sample identity with an actual canonical
native GUID from inspection. Supply the current `.bpm` path or operation artifact
handle and its expected SHA-256 revision. Operation artifact handles are protocol
references, not Windows filesystem paths.

The independent client has `--native --artifacts-only`. It creates its own native
model, root and nested content, checks rejected edits and later recovery, performs
a native clone, invokes the installed renderer/publication engine and checks
clearing, deletion, no-op persistence and original-file preservation.
The final source run `20260907-220740-15048c` passed **18 terminal operations**:
14 completed and four expected real worker failures. It also updated existing
group/header geometry and required the exact requested root/subprocess output
identities. The root SVG verified **8/8** graphical identities; the nested SVG
verified **2/2**. Word readback found **6 pages and 23 images**, including document
icons rather than only diagram pictures. These checks do not accredit the visual
layout of every artifact or every rich-text feature.

The run retained **30 worker observations and 303 periodic desktop samples**;
no visible owned-worker window or worker foreground ownership was observed.
Transcript SHA-256:
`d4fe6190cad14ffb8495aab65ee51ba7d49a24baa9bd8e010aa5ec13ffe844c9`.
An earlier complete source run also passed; retained development failures were
diagnosed, not relabeled as successful engine operations.

Source regression and public build evidence are described in
[verification baselines](validation.md#native-content-artifacts--2026-09-07).
This source milestone is not included in the earlier immutable
[consolidated data/event package](validation-native-data-package.md).

Image artifacts now have a separate [typed image contract](native-images.md).
Custom artifact lifecycle, full rich-text editor behavior, header template
configuration and independent visual compatibility inside Modeler remain
separate capabilities. Offscreen native SVG or Word generation does not prove
that every rich-text layout matches the interactive application.
