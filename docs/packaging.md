# Windows packaging

The initial distribution is framework-dependent. Operators need the x64 .NET 10
runtime, .NET Framework 4.8, and their own installation of Modeler for native
operations. Building requires the SDK pinned in `global.json` and PowerShell 7.2+.

## Build and inspect

Run `scripts/package.ps1` from a reviewed checkout. The script uses locked restore,
publishes the host, builds the worker, collects redistributable dependency
licenses, and creates a ZIP under ignored `artifacts/` storage. No Bizagi
installation is accessed by this script. It never overwrites an existing output
directory or automatically uploads a release.

The package contains:

- `McpBizagi.Server.dll`, its dependency manifest, and host dependencies.
- `worker/McpBizagi.Worker.exe`, its own configuration, adapter, and dependencies.
- Project documentation, example BPMN, project license, attribution, and notices.
- `licenses/` with actual third-party texts and NuGet metadata.
- `dependencies.json` with package identities, hashes, and license sources.
- `package.json` with version, source commit, dirty-checkout indicator, and build time.
- `manifest.sha256.json` with hashes of the packaged files (excluding itself).

Dependency licenses absent from NuGet are fetched from pinned upstream commits.
Network access is required for this step. Missing licenses stop packaging; do not
remove this check to produce a distributable ZIP.

## Verify the packaged server

Extract the ZIP to a fresh directory. From the source checkout, point the
independent client at that extracted package:

```powershell
dotnet run --project tests/McpBizagi.Acceptance -c Release --no-build -- . --package C:/Packages/MCP-Bizagi
dotnet run --project tests/McpBizagi.Acceptance -c Release --no-build -- . --package C:/Packages/MCP-Bizagi --native
dotnet run --project tests/McpBizagi.Acceptance -c Release --no-build -- . --package C:/Packages/MCP-Bizagi --native --extended --simulation --render --recovery --external-state --settings-contention
```

The first command proves actual packaged stdio/XML behavior. The second also
requires the native installation and tests native persistence, copy-only edits,
failure reporting, cancellation, and recovery. Neither substitutes for broad
rich-content or visual Modeler acceptance.

The expanded command also checks the worker settings namespace, explicit native
settings contention and recovery. Repeat focused renderer startup checks with
`--native --render-only --input C:/Processes/example.bpm` against the same extracted
package. A single successful image is not sufficient to dismiss a reproducible
asynchronous initialization failure.

Add `--diagram-id <native-guid> --render-repetitions 3` to the focused renderer
client to check the same exact diagram in three separate workers. Each attempt
must succeed; a failed attempt stops acceptance rather than being hidden by a
retry. The selected ID must exist in a fresh native inspection.

Configure an MCP client with command `dotnet` and argument
`C:/Packages/MCP-Bizagi/McpBizagi.Server.dll`, adjusting the path. The worker is
discovered in the sibling `worker/` directory unless explicitly overridden.
Set `MCP_BIZAGI_ROOT` and opt into native diagnostics as described in
[configuration](configuration.md).

## Additional native acceptance families

Run the focused clients against the same extracted package, using a reviewed
native file with a task and incoming flow for the mutation test:

```powershell
dotnet run --project tests/McpBizagi.Acceptance -c Release --no-build -- . --package C:/Packages/MCP-Bizagi --native --mutations-only --input C:/Processes/example.bpm
dotnet run --project tests/McpBizagi.Acceptance -c Release --no-build -- . --package C:/Packages/MCP-Bizagi --native --palette-only --input C:/Processes/example.bpm
dotnet run --project tests/McpBizagi.Acceptance -c Release --no-build -- . --package C:/Packages/MCP-Bizagi --native --publication-only --input C:/Processes/example.bpm
dotnet run --project tests/McpBizagi.Acceptance -c Release --no-build -- . --package C:/Packages/MCP-Bizagi --native --expanded-render-only
dotnet run --project tests/McpBizagi.Acceptance -c Release --no-build -- . --package C:/Packages/MCP-Bizagi --native --metadata-only
dotnet run --project tests/McpBizagi.Acceptance -c Release --no-build -- . --package C:/Packages/MCP-Bizagi --native --attributes-only
dotnet run --project tests/McpBizagi.Acceptance -c Release --no-build -- . --package C:/Packages/MCP-Bizagi --native --containers-only
dotnet run --project tests/McpBizagi.Acceptance -c Release --no-build -- . --package C:/Packages/MCP-Bizagi --native --diagrams-only

dotnet run --project tests/McpBizagi.Acceptance -c Release --no-build -- . --package C:/Packages/MCP-Bizagi --native --model-create-only

dotnet run --project tests/McpBizagi.Acceptance -c Release --no-build -- . --package C:/Packages/MCP-Bizagi --native --commit-only --external-state
```

Use `--containers-only --input C:/Processes/rich-model.bpm` to exercise the
container lifecycle while preserving an existing model's other native content.
Run focused native circuits sequentially because the installed engine's settings
namespace is protected by an exclusive worker lease. Attribute, container and diagram
tools require the newer source snapshot, not the old published 0.4 ZIP.
The commit suite additionally exercises real post-publication host death,
cancellation, retained staged bytes and independent reconciliation. Add
`--input C:/Processes/rich-model.bpm` for a rich native byte-preservation corpus.

For a separately reviewed PDF downsampling test, add `--allow-image-resampling`
to publication acceptance. This is explicit fidelity acceptance, not an automatic
retry or a claim of pixel equivalence. Review generated document layout separately.

## Public CI boundary

GitHub-hosted Windows CI restores, builds, runs unit tests, and executes the real
MCP XML client. It does not include Modeler or native test credentials. Native
acceptance runs only on a reviewed, controlled Windows installation. Public pull
requests must never automatically execute on the operator's machine.
