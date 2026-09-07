# Contributing

Thank you for helping build reliable programmatic Modeler tooling.

1. Use English for code comments, documentation, issues, and commit messages.
2. Restore locked dependencies and build with the pinned .NET SDK.
3. Add focused unit tests for changes and real MCP acceptance for changed workflows.
4. Keep native integration behind the adapter boundary; do not add click automation.
5. Do not commit Bizagi binaries, decompiled code, credentials, private models,
   internal planning, or raw diagnostic artifacts.
6. Keep generated outputs and mocks separate from operational evidence.
7. Preserve attribution and dependency license notices.

Use small commits such as `fix: preserve XML extensions during name updates`.
Run `dotnet test tests/McpBizagi.Core.Tests -c Release` and the non-native
acceptance client before submitting. State explicitly which native tests were
not run. Native results require your own installed Bizagi environment.

Public pull requests run on GitHub-hosted runners only. Maintainers must review
code before any native acceptance on their own Windows machines. No automatic
self-hosted execution is allowed for external contributions.

Contributions are submitted under the repository's custom attribution license.
