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
dotnet run --project tests/McpBizagi.Acceptance -c Release --no-build -- . --package C:/Packages/MCP-Bizagi --native --extended --simulation --render --recovery --external-state
```

The first command proves actual packaged stdio/XML behavior. The second also
requires the native installation and tests native persistence, copy-only edits,
failure reporting, cancellation, and recovery. Neither substitutes for broad
rich-content or visual Modeler acceptance.

Configure an MCP client with command `dotnet` and argument
`C:/Packages/MCP-Bizagi/McpBizagi.Server.dll`, adjusting the path. The worker is
discovered in the sibling `worker/` directory unless explicitly overridden.
Set `MCP_BIZAGI_ROOT` and opt into native diagnostics as described in
[configuration](configuration.md).

## Public CI boundary

GitHub-hosted Windows CI restores, builds, runs unit tests, and executes the real
MCP XML client. It does not include Modeler or native test credentials. Native
acceptance runs only on a reviewed, controlled Windows installation. Public pull
requests must never automatically execute on the operator's machine.
