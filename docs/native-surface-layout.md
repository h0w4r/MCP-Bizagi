# Automatic native surface placement

`native_surface_layout` calculates positions on the modern MCP server, then uses
the installed editor's `modeling.alignElements` command to apply all movements
and dependent routes in one native transaction. It does not require client-side
coordinate planning, simulated input or a foreground Modeler window.

This is an experimental source capability after the immutable 0.6 package.
One closed surface is not full, multi-owner Modeler auto-layout.

## Request and result

Call `native_inspect` first to obtain the real diagram, process/subprocess IDs
and source SHA-256 revision. Then call `native_surface_layout` with:

- `path`: a confined native model path or completed native artifact handle.
- `expectedRevision`: the exact inspected source SHA-256.
- `layout.DiagramId`: the native diagram UUID.
- `layout.OwnerId`: its actual Process or embedded SubProcess UUID.
- `layout.Direction`: `Right` (default) or `Down`.

The client supplies neither positions nor a partial node selection. Every direct
node belongs to the operation. Poll `operation_get` for its terminal state.
Only a completed operation produces an adoptable `outputArtifact` and
`outputRevision`; the original is never replaced by this operation.

`Result.layoutPlan` records the pinned solver, assembly fingerprint, source graph
hash, pre-editor placement intent, direction, node/connection counts and the
calculated node/attachment/manual-label envelopes. `Result.receipt` records the
actual installed-editor transaction and callback hash. Native source, edited and
fresh-reader graphs and full-archive fidelity are separate observations.

## Architecture and boundaries

1. Snapshot and revision-check the original; open it through an isolated worker.
2. `NativeSurfaceLayoutPlanner.Calculate` validates complete direct ownership and
   builds one MSAGL geometry graph, retaining cycles, self-edges and parallel edges.
3. The pinned **Msagl 1.2.1** dependency runs only in the modern host. Its actual
   `LayeredLayout` uses seed 17, layer separation 100 and node separation 70.
   Its route stage is disabled: routing remains an installed-editor operation.
4. Existing attached events and manually positioned labels contribute to their
   host's rectangular packing envelope. Host dimensions and attachment offsets
   are preserved. Positions are quantized before execution, never after readback.
5. The actual native grouped move command invokes the native movement and router
   services. During calculated movement an existing native hint prevents temporary
   geometry-based participant reassignment. No failed callback or saved XML is
   rewritten to conceal a mismatch.
   For supported host-edge boundary attachments, the router receives an explicit
   current outward `connectionStart` hint instead of the move helper's corner
   origin. An independent gate checks the durable endpoint again.
6. The modern host independently translates the retained callback into typed
   intent and compares it with the worker receipt. A separate worker reopens the
   durable `.bpm`; graph, metadata and whole-archive gates must pass.

MSAGL's real `ProgressChanged` events update the operation phase with an
algorithm-local ratio, not a fabricated whole-operation percentage. The actual
solver cancellation token is linked to operator cancellation. Native worker
inactivity/ownership/cancellation and recovery policies remain unchanged.
Native worker phases now await the host RPC handler's acknowledgment, rather
than only notification transmission; see StreamJsonRpc's
[request and notification semantics](https://microsoft.github.io/vs-streamjsonrpc/docs/sendrequest.html).
Every received phase updates live `operation_get` status; bursty intermediate
disk snapshots are coalesced at 250 ms. Polling need not observe every transient phase.
Operation start, cancellation and terminal states still persist immediately.
This cadence is not a runtime/cancellation timeout, and crash recovery never
silently replays a write.

## Explicit unsupported and unaccredited cases

- Fewer than two or more than 1,000 direct movable nodes.
- More than 1,000 total direct records, including attachments and routes, exceeding
  the native callback transaction bound.
- Lanes/milestones, expanded nodes and unsupported direct artifact surfaces.
- Cross-owner or cross-pool connections, missing endpoints and unresolved anchors.
- Boundary route origins not on the unambiguous outward side of a host-edge
  attachment. Tangential, corner and offset anchors need a separate routing
  contract. This origin gate applies before planning and after fresh readback.
- Fractional/nonrepresentable manual-label changes, unsupported geometry, unknown
  semantic changes and content that does not survive the native fresh reader.
- Automatic resizing of pools or expanded containers; geometry leaving the valid
  native surface can be rejected instead of silently reparented.
- Repair of malformed original routes. A native route result that cannot survive
  restart is quarantined, not returned as partial success.

Packing envelopes do not prove text rendering quality or global connector obstacle
clearance. SVG endpoint equality does not prove every rounded curve, arrowhead,
crossing or port/shape combination. Native rendering is not independent desktop
GUI compatibility. Live unsaved documents and whole-model automatic layout remain
separate unfinished capabilities.

## Reproduce the operator circuit

Build the source using the repository's locked dependencies. On a controlled
Windows installation, run the independent official-SDK acceptance client with
`--native --surface-layout-only`. It authors its own model through MCP, includes
root/embedded cycles and self-loops, parallel flows, manual labels, an attached
timer and an opaque presentation file, then executes calculated movement, fresh
readback, save copy, native renders and original-source verification.

Private raw evidence remains outside the repository. Sanitized execution records
belong in [validation.md](validation.md); unit tests are not native accreditation.

The dependency's reviewed MIT license source is pinned separately from its binary
build provenance; see [third-party notices](../THIRD-PARTY-NOTICES.md).
