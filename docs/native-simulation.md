# Native metadata and simulation

These tools operate on the installed Modeler engine and its native objects.
They do not convert an existing `.bpm` to BPMN to edit it, run a replacement
simulation engine, or automate desktop controls.

## Read, edit, verify

1. Call `native_metadata_get` with a native file or completed artifact reference.
2. Poll `operation_get`. Keep `Result.sourceRevision` and `Result.result.Metadata`.
3. Submit `native_metadata_apply` with that revision and an explicit `patch`.
4. Poll the operation. Successful results include `reopened.Metadata`,
   `fidelity.Preserved`, `outputArtifact`, and `outputRevision`.
5. Use the new artifact for subsequent operations; the original is not overwritten.

Every native writer is followed by a **new worker process** that reopens the
durable file. The gate verifies requested values and compares all other native
archive leaves, including unknown XML and binary content. Unsupported changes
fail the operation instead of being published as a successful edit.

## Resource catalog

`NativeMetadataPatch.Resources` accepts `NativeResourceChange` entries:

| Field | Meaning |
| --- | --- |
| `Operation` | `upsert` (default) or `delete` |
| `Id` | Canonical, nonempty native resource GUID; generate one for creation |
| `Name` | Nonempty resource display name for upsert |
| `Description` | Resource documentation, including Unicode |
| `Type` | Native `Role` or `Entity` |

The native resource-ID generator creates the corresponding `NativeResource.BpmnId`.
Names must be unique ignoring case. Deletion fails while the resource is
referenced by RACI or simulation configuration. Remove references in a previous
verified operation, then delete; batches are not silently reordered.

The fidelity gate verifies both the model-level `Participants.xml` and the
catalog copy in every diagram's native XPDL package. Global/cloud catalog
resources are not supported by the local editing contract.

## Activity RACI

`NativeMetadataPatch.Assignments` contains complete `NativeResourceAssignments`
sets for activities, including tasks and supported activity subclasses:

- `ElementId`: native activity GUID, **not** its BPMN ID.
- `Responsible`, `Accountable`, `Consulted`, `Informed`: ordered arrays of
  native resource GUIDs. Empty arrays explicitly clear that role.

Omitting `Assignments` leaves every assignment untouched. Supplying an assignment
entry replaces all four role sets for that activity. Duplicate resource IDs in
one set and unknown references are rejected. Process-level RACI edits are not
included in this contract.

The installed engine stores ACI names as comma-separated values. ACI assignment
of a resource whose display name contains a comma is rejected rather than
producing an ambiguous persisted relationship. Resource renames which would
change existing ACI representations without an explicit assignment replacement
can fail the fidelity gate; include the affected assignment sets explicitly.

## Complete BPSim configuration

`NativeMetadataPatch.Simulations` contains `NativeSimulationConfiguration` entries:

- `DiagramId`: native collaboration GUID.
- `Xml`: complete native BPSim 1.0 configuration for **that diagram**.

The XML root is `BPSimData` in `http://www.bpsim.org/schemas/1.0`, with an explicit
`simulationLevel` of `LevelOne`, `LevelTwo`, `LevelThree` or `LevelFour`. This is
not a BPMN interchange document. The complete replacement makes scenario
addition, deletion and parameter edits explicit instead of hiding partial merges.

Start with XML returned by `native_metadata_get`. Retain unmodified scenarios,
parameters and native serialization defaults. In particular:

- Scenario IDs are unique XML identifiers. Set explicit author/version values
  when creating scenarios; do not rely on account-derived defaults.
- `ElementParameters.elementRef` uses `NativeElement.BpmnId` or
  `NativeResource.BpmnId`. The adapter rejects unknown references in the selected
  diagram, including elements belonging to another diagram.
- Calendar IDs and `validFor` references must agree within the scenario.
- `ScenarioParameters.baseTimeUnit` governs native floating time parameters.
  Do not add attributes that the installed serializer marks as unsupported.
- The native serializer materializes empty `PropertyParameters` arrays. Include
  these explicitly when constructing new scenario/element parameter objects.
- Years are rejected because this engine normalizes them to days on loading.
- Unknown native elements/attributes and changed readback values fail explicitly.

`DiscardSimulationResults: true` explicitly clears saved simulation results for
the diagrams being replaced. The gate verifies empty native result containers.
Replacing configuration while saved results exist without this flag is rejected,
preventing stale results or silent result loss. Other diagrams are untouched.

## Running scenarios

`native_simulate` runs one scenario using the installed simulation manager.
An empty `scenarioId` explicitly requests native defaults. A nonempty ID selects
the persisted scenario. `simulationLevel` is an integer from 1 to 4.

`native_simulate_what_if` requires explicit `scenarioIds`. It uses the native
what-if manager and each scenario's replication count. Every replication result
is copied during its completion event, before the engine reuses its output file.
Duplicate, missing or unexpected replication identities fail the operation.

Both return `EngineReply.SimulationReports` with:

- Scenario identity, optional replication number and result artifact.
- Per-element identity, display name, metric kind and a dictionary of actual
  engine metric values. Values remain strings to preserve the engine's encoding.
- Original XML artifacts containing additional nested statistics and native input.

Simulation is read-only with respect to the source model. Results are operation
artifacts, not automatically saved into `.bpm`. The native engine may filter
parameters according to the selected level; running level 1 does not prove that
resource or calendar settings were exercised.

## Real acceptance and boundaries

Run against an installed engine, or add `--package <extracted-directory>`:

```powershell
dotnet run --project tests/McpBizagi.Acceptance -c Release --no-build -- . --native --metadata-only
```

The original test process completes 12 instances. Its two timing scenarios use
3 and 9 minutes per task, with two what-if replications each. Additional scenarios
assert constrained-resource waiting, a task completion cost of 84, and delayed
work until an 08:00 calendar shift. Acceptance also sets and clears activity RACI,
rejects referenced-resource deletion and an unknown simulation element, verifies
recovery, compares structured MCP metrics with actual XML, and performs a no-op
save of the configured native model.

This corpus is not exhaustive stochastic validation, universal calendar support,
or full Modeler automation. Broad distributions, scenario inheritance, multiple
interacting processes, all resource/calendar combinations, saved-result editing,
and independent visual compatibility require further dedicated acceptance.

## Primary references

- [Bizagi scenarios](https://help.bizagi.com/platform/en/scenarios.htm)
- [Simulation levels](https://help.bizagi.com/platform/en/simulation_levels.htm)
- [Resource analysis](https://help.bizagi.com/platform/en/level_3_example.htm)
- [Performers and RACI](https://help.bizagi.com/platform/en/define_performers.htm)
- [What-if analysis](https://help.bizagi.com/platform/en/what_if_analysis_example.htm)
