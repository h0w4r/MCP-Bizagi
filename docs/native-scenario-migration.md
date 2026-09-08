# Explicit native scenario migration

Current source after **0.6.0-alpha.1** adds optional `simulationMigration` to
`native_elements_reparent`. It is not included in the immutable 0.6 ZIP.
The operation moves installed native `ElementParameters` objects alongside the
selected graph, then persists and reads the model in separate worker processes.
It does not replace a native model with reconstructed BPMN or rewritten ZIP XML.

## Explicit request

Use the normal [reparenting contract](native-reparenting.md), including the source
revision, complete reference closure, and original/final diagram GUIDs. Add:

```json
{
  "simulationMigration": {
    "Mappings": [
      {
        "SourceDiagramId": "<original native diagram GUID>",
        "SourceScenarioId": "<existing source scenario ID>",
        "TargetDiagramId": "<destination native diagram GUID>",
        "TargetScenarioId": "<existing destination scenario ID>"
      }
    ],
    "CopyMissingDependencies": true,
    "DiscardSimulationResults": false
  }
}
```

These placeholders are not runnable model identities. Inspect the actual native
model first. [The complete request template](../examples/native-scenario-migration.json)
includes the containment move. The independent acceptance command below creates
its own real models and discovers their identities through MCP.

| Field | Contract |
| --- | --- |
| `NativeSimulationMigration.Mappings` | 1–1000 explicit source/destination scenario correspondences; destination scenarios must already exist |
| `NativeScenarioMapping.SourceDiagramId` / `TargetDiagramId` | Distinct affected native diagrams, not names or inferred destinations |
| `NativeScenarioMapping.SourceScenarioId` / `TargetScenarioId` | Existing XML scenario identities; one destination per source scenario and target diagram |
| `NativeSimulationMigration.CopyMissingDependencies` | Explicit permission to copy missing resource-parameter records and calendars; default false |
| `NativeSimulationMigration.DiscardSimulationResults` | Explicit retirement of saved results in affected diagrams; default false; native result-bearing acceptance is recorded separately |

## Context and dependency preservation

- Every source scenario with effective moved-element parameters requires a
  mapping, including scenarios that inherit their parameters without a local
  override. Both `inherits` and `result` provenance links must map consistently.
- Source/destination simulation levels, global parameters, units and vendor
  context must match. Ordinary scenario identity/label metadata may differ;
  namespaced vendor attributes with similar names remain protected context.
- Units are never converted automatically. BPSim defines scenario-level time and
  currency context, inheritance, calendars and parameter records separately;
  moving records without that context can change their interpretation.
  See the [BPSim 1.0 specification](https://www.bpsim.org/specifications/1.0/WFMC-BPSWG-2012-01.pdf).
- The complete source resource-parameter context and calendar collection are
  considered, rather than guessing dependencies from one expression syntax.
  Missing records require explicit copy permission. Equal destination records
  are reused; conflicting records and incoming-parameter overrides reject.
- Source resource parameters and calendars remain intact. Only parameters for
  actually moved native/BPMN element references leave their source scenario.
- Native dependency copies use the installed runtime types and their XML
  serializer with strict unknown-element/attribute rejection. No vendor
  components are redistributed or modified.

## Independent evidence surfaces

`NativeSimulationMigrationPolicy.Prepare` checks that the native source snapshot
preserves the durable BPSim document before deriving a complete expected result.
The host sends only allowlisted collection operations to the worker, not public
reflection calls or arbitrary code. `NativeEngine.MigrateScenarioParameters`
moves the actual records after their graph owners reach the target diagram.

`NativeSimulationMigrationPolicy.Project` compares the entire expected BPSim
configuration with the editor snapshot, a newly started reader, and the durable
archive. It reverses only verified changes in comparison copies for the existing
whole-archive gate. Unknown XML, unrelated scenarios, other diagrams and binary
content remain protected. A changed value on any evidence surface fails.

The operation receipt includes `simulationMigration.Transfers`,
`simulationMigration.ExpectedConfigurations`,
`simulationMigration.DiscardResultDiagrams`, the native readbacks and fidelity
report. These are operation-specific evidence and may contain private model data.
The source file is unchanged; publication into a workspace file remains the
separate revision-checked `native_commit` operation.

## Saved results and remaining boundaries

Nonempty presentation actions remain rejected. Saved simulation results are not
migrated as if they still described the modified graph. Without explicit discard
consent, existing nonempty result containers reject. With consent, the adapter
clears native scenario result strings and the comparator requires an empty
native-persisted result container. Unknown container annotations or unrepresented
records reject rather than disappear.

The [saved-results circuit](native-saved-simulation.md) exercises genuine nonempty
results, default rejection and explicit native retirement, without injecting
synthetic archive payloads.
Likewise, mapping an inherited scenario's configuration is not proof of its
runtime execution. Resource/calendar execution below tests explicit base
scenarios. Arbitrary distributions, behavioral equivalence, presentation-action
migration, automatic layout, live unsaved sessions and Modeler GUI compatibility
remain separate gates.

## Repeat the real circuit

```powershell
dotnet restore --locked-mode
dotnet build -c Release --no-restore
dotnet test tests/McpBizagi.Core.Tests -c Release --no-build
dotnet tests/McpBizagi.Acceptance/bin/Release/net10.0-windows/McpBizagi.Acceptance.dll . --native --scenario-migration-only
```

Run native clients sequentially. The client uses the official MCP SDK and actual
stdio tools, not server policy classes. It authors a Unicode native model with
three diagrams, resources/RACI, explicit timing/cost parameters, working calendars
and mapped inheritance. It tests missing intent, missing copy consent and an
incomplete mapping, then a forward migration, native no-op save and inverse move.

Before and after migration it runs the installed level-3 and level-4 simulator:
12 task completions, three minutes average processing, 36 minutes total busy time,
84 fixed cost, resource contention and working-calendar waiting are asserted.
The verifier selects the actual native process/task identities; auxiliary native
processes/tasks must not contribute completed work. The original file revision
and unrelated diagram configuration remain unchanged.

See the [verification ledger](validation.md) for retained run IDs, hashes and
worker/desktop observations. Unit tests, this native source circuit, packaged
acceptance and independent visual verification remain distinct.
