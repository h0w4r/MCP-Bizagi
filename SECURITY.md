# Security

Please report sensitive issues privately using GitHub private vulnerability
reporting when available. Do not publish credentials, private models, or raw
native diagnostics in an issue.

## Boundaries

- A local stdio server, not a multi-tenant service.
- Named-pipe access limited to the Windows identity running the host.
- Explicit workspace root; no arbitrary assembly or method invocation tools.
- XML DTD processing prohibited and document size bounded.
- Native diagnostics opt-in and version gated.
- No credential extraction, account impersonation, or licensing bypass.
- No proprietary binary distribution or patching.

Workspace validation is not a substitute for filesystem permissions. A process
running under the same user can race filesystem operations or inspect that user's
data. Use an isolated account or machine for untrusted workloads.

Experimental native parsing may depend on vendor behavior beyond the host's XML
checks. Use trusted test models and retain original backups. Expanded native file
ingestion remains a separate validation gate.
