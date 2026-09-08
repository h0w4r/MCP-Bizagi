# Native saved simulation results

Current source after **0.6.0-alpha.1** supports explicit native result persistence
and historical-result reading. The immutable 0.6 ZIP predates these additions.
This is an experimental installed-engine capability, not full Modeler automation
or an assertion of independent GUI compatibility.

## Simulate and keep the real result in a native copy

Call `native_simulate` with the usual confined path and actual native IDs:

```json
{
  "path": "<workspace-relative .bpm or native artifact URI>",
  "diagramId": "<native diagram GUID>",
  "scenarioId": "<existing scenario ID>",
  "simulationLevel": 3,
  "saveResultsAsNativeCopy": true
}
```

The [request template](../examples/native-saved-simulation.json) is not a runnable
model: replace its placeholders with IDs from `native_metadata_get` or
`native_inspect`. `saveResultsAsNativeCopy` defaults to false. When true, an
explicit existing scenario is required; native defaults are not guessed.

Poll `operation_get`. A successful `OperationView.Result` contains:

| Field | Meaning |
| --- | --- |
| `result` | Actual analyzer reply, input XML, result XML, structured metrics and explicit simulation limitations |
| `sourceRevision` | SHA-256 of the captured original native model |
| `savedNative.outputArtifact` | New `artifact:<operationId>:edited.bpm`, not an overwrite of the source |
| `savedNative.outputRevision` | SHA-256 of that durable native copy |
| `savedNative.PreviousResult` | Previous selected-scenario result digest/character count, or null when absent |
| `savedNative.resultFileSha256` | Byte hash of this operation's actual simulator output |
| `savedNative.edited` / `reopened` | Native editor and independent fresh-reader replies |
| `savedNative.fidelity` | Whole-native-archive preservation gate, permitting only the exact chosen result change |

Saving replaces only the selected scenario's result **in the new copy**. Other
scenarios' result strings remain exact, including opaque nested XML. Resource,
calendar, BPSim configuration, model graph, attachments and unknown archive leaves
remain subject to the complete fidelity gate. Result-record collection order is
not treated as semantic; per-scenario identities and payloads are exact.

Simulation level remains a runtime selection: saving results does not implicitly
rewrite scenario settings or change their active selection. Retained historical
results do not prove that the current settings would reproduce them.

## Read historical metrics without rerunning the simulator

Call `native_simulation_results_get(path, diagramId, scenarioId)` with explicit
identities. The native loader reopens the `.bpm` and reads the actual installed
`Scenario.SimulationResult` property. This path does not start a simulation.

- `Result.result.SimulationReports` exposes the existing structured metric format.
- `Result.result.Artifacts` includes a UTF-8 `Results.xml` export.
- `Result.result.SavedSimulationResults` lists native diagram/scenario IDs,
  `Sha256` and `CharacterCount` for nonempty persisted property strings.
- `Result.sourceRevision` and unchanged-input checks preserve the original model.
- A valid scenario with no saved result fails explicitly. There is no fabricated
  empty report or automatic rerun disguised as a historical result.

Native result strings can retain a Latin-1 XML declaration. Digest receipts hash
the original native Unicode string encoded as UTF-8, **not** the exported file.
Historical export uses a matching UTF-8 declaration and preserves the XML content;
its byte hash can therefore differ. Original simulator artifacts remain separate.

## Actual native write and recovery boundaries

1. Capture the native input and verify the explicit diagram/scenario identity.
2. Run the installed simulation manager in an isolated analyzer worker.
3. Take only the current operation's actual `Results.xml`, check its hash, and
   parse bytes with their declared encoding. DTD/external resolution is disabled.
4. Start a **different editor worker** and reload the original captured `.bpm`.
   Never persist the analyzer's model: it may contain transient input projections.
5. Set the selected native `Scenario.SimulationResult` to the native completion
   equivalent `XmlDocument.OuterXml`, then save through installed persistence.
6. Start a fresh native reader. Require every expected saved-result property
   digest and exact durable payload, plus the whole-archive fidelity gate.

No public tool accepts arbitrary result XML, reflection paths or an external
result-file pathname for injection into a model. The worker's internal
`NativeSimulationResultWrite` links the authorized analyzer artifact by hash.
No vendor components or configurations are modified or redistributed.

Unknown result-container attributes, comments, processing instructions, orphaned
scenario records, duplicate identities and structured children outside the native
opaque-text contract reject instead of disappearing. The same validation protects
explicit result retirement during metadata replacement and cross-diagram migration.

Use [native commit](native-commit.md) to adopt a completed result artifact into a
workspace file with revision checks and backup. Failed/cancelled operations do not
publish adoptable artifacts. Do not blindly retry after a worker disconnect;
inspect the durable operation state. The shared owned-process cancellation and
recovery infrastructure applies; this feature's acceptance is not a new exhaustive
crash-at-every-instruction or persistence-cancellation proof.

## Independent MCP acceptance

```powershell
dotnet restore --locked-mode
dotnet build -c Release --no-restore
dotnet test tests/McpBizagi.Core.Tests -c Release --no-build
dotnet tests/McpBizagi.Acceptance/bin/Release/net10.0-windows/McpBizagi.Acceptance.dll . --native --saved-simulation-only
```

The client uses actual MCP stdio tools and installed workers. It creates three
diagrams with resources, RACI, timing/cost parameters, calendars and inheritance.
It saves real level-3/4 simulation results, overwrites one while retaining another,
opens the durable ZIP independently, compares native properties, performs no-op
saves, and queries historical metrics through a new native loader without reruns.

The same genuine result-bearing model exercises metadata-replacement and migration
guards: defaults reject stale results, explicit consent retires them in a native
copy, and the original remains unchanged. Missing scenarios and absent historical
results produce actual errors followed by successful recovery.

Each actual simulation and historical read checks 12 completions, three minutes
average processing, 36 minutes total busy time, 84 fixed cost, resource contention
and working-calendar waiting. Structured historical metrics must match the native
XML. Synthetic unit tests separately cover encoding, tampered readbacks, unknown
container data and unrelated archive preservation.

See the [verification ledger](validation.md) for the final run and evidence hashes.
What-if result-history persistence, arbitrary saved-result editing, general
scenario inheritance execution, exhaustive distribution/calendar semantics and
independent Modeler GUI compatibility remain outside this accredited corpus.
