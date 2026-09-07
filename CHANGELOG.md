# Changelog

## 0.4.0-alpha.1

- Add revision-checked native resource creation, updates and guarded deletion.
- Add complete activity RACI assignment replacement and native readback of every role.
- Add native metadata inspection and explicit per-diagram BPSim configuration replacement, including scenarios, resources and calendars.
- Reject unknown simulation fields, dangling element references, ambiguous comma-separated ACI names and silent saved-result loss.
- Compare catalog copies inside every native diagram as well as the model-level resource catalog.
- Execute configured levels 2–4 through real MCP, asserting completion counts, processing times, task costs, resource contention and calendar delays.
- Add native what-if execution with synchronous capture of every replication before native result files are reused.
- Return actual engine metrics as structured MCP reports while retaining raw result XML.
- Add metadata/RACI fidelity regressions and full native metadata acceptance, including rejected writes, recovery and rich no-op save.
- Full Modeler automation remains open: broad containers/layout, extended attributes/attachments, additional publication and visual/live-session capabilities are not implied by this release.

## 0.3.0-alpha.1

- Add `native_mutate`: explicit create/update/delete/reconnect batches with native identities and revision checks.
- Verify native bounds, colors, descriptions, endpoints and waypoints after fresh-worker readback.
- Gate structural edits using an explicit mutation projection plus the existing whole-container comparator.
- Exercise creation and deletion of 22 native task/event/gateway variants through a real MCP client.
- Add Excel, Word and PDF publication using the installed generators and separate document-reader workers.
- Exclude Excel's desktop-opening step; do not launch Office or a document viewer.
- Add explicit publication titles, selection, image evidence and opt-in warnings for native PDF downsampling.
- Render nested subprocess surfaces programmatically and reuse the native offscreen engine within a publication.
- Wait for expanded children before composing parent SVGs; verify nested graphical IDs and dimensions through MCP.
- Preserve supplied expanded BPMN DI bounds on new native imports instead of accepting the importer's threefold scaling; record integration adjustments and expose separate expanded geometry.
- Expand unit tests for edit-plan validation, unknown-content rejection and publication image matching.
- Keep container editing, extended-attribute/attachment/resource editing, advanced simulation, additional publication formats and full Modeler automation open.

## 0.2.0-alpha.1

- Add whole-native-container comparison and a rejection gate for unexplained changes.
- Add native no-op copy saves with independent fresh-worker readback.
- Inspect native containment, geometry, descriptions, references and scenarios.
- Exercise multi-diagram import/export and two-level nested task edits through MCP.
- Add actual native validation and experimental simulation; verify a level-one scenario completing 1,000 instances.
- Add native offscreen SVG and transparent PNG rendering with explicit script/result checks.
- Wait for actual renderer element completion and stable SVG; reject stalled partial diagrams.
- Synchronize native graphical defaults before creating renderer DTOs and surface asynchronous browser errors.
- Verify the worker-only settings namespace and exclude concurrent native workers across different state directories.
- Add opaque native artifact references for MCP-only chaining across private state boundaries.
- Supervise all owned job processes and their CPU/I/O activity, not only the worker PID.
- Add an exclusive state lease, durable flushed journals and orderly shutdown cancellation.
- Exercise abrupt host death, owned-worker termination, journal recovery and a subsequent native request.
- Keep full Modeler automation, publication, rich structural edits and broad native/visual fidelity explicitly open.

## 0.1.0-alpha.1

- Establish the .NET 10 official MCP host, isolated .NET Framework 4.8 worker and private StreamJsonRpc pipe.
- Add guarded BPMN XML tools and native import/save/reload/export diagnostics.
- Verify copy-only task/event name batches, active cancellation and recovery through a real MCP client.
- Publish the initial Windows package without proprietary Bizagi components.
