# Third-party components

MCP-Bizagi's custom license applies to this repository's original code, not to
its dependencies. Exact dependency versions, including transitives, are pinned
in each project's `packages.lock.json`.

| Direct component | Upstream | License |
| --- | --- | --- |
| MCP C# SDK | https://github.com/modelcontextprotocol/csharp-sdk | Apache-2.0 |
| StreamJsonRpc | https://github.com/microsoft/vs-streamjsonrpc | MIT |
| Json.NET | https://github.com/JamesNK/Newtonsoft.Json | MIT |
| Microsoft.Extensions.Hosting | https://github.com/dotnet/runtime | MIT |
| .NET reference assemblies | https://github.com/microsoft/dotnet | MIT |
| xUnit.net | https://github.com/xunit/xunit | Apache-2.0 |
| Visual Studio Test Platform | https://github.com/microsoft/vstest | MIT |

Build and test dependencies are not necessarily runtime dependencies. Redistributed
packages must retain the applicable license texts and notices for their included
components, not only this summary.

`scripts/package.ps1` includes the runtime dependency closure's NuGet metadata,
embedded license/notice files, and a dependency inventory. When a package omits
its license text, the script fetches it from the exact upstream repository commit
recorded in that package's metadata. An unresolved license stops packaging.

Bizagi Modeler and its bundled third-party components remain separately licensed.
They are loaded from the operator's installation and must not be added to this
repository or release archives.
