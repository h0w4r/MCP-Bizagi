# Native Web publication

**Experimental current source, after `0.6.0-alpha.1`.** The immutable 0.6 ZIP
does not include this addition. This is a local publication capability, not a
web server, cloud publisher or live Modeler session bridge.

## Operator contract

Call `native_publish` with `format: "web"`, a workspace `.bpm` path or completed
native artifact reference, and an explicit title. Optional `diagramIds` are
distinct native collaboration GUIDs; omission selects all diagrams.

```json
{
  "path": "process.bpm",
  "format": "web",
  "title": "Process documentation",
  "diagramIds": []
}
```

Poll the returned operation with `operation_get`. A completed result includes
the `index.html` entry and the accompanying directory assets. Retain the **whole
publication directory** when moving or serving the site: the entry alone is not
a portable document. The MCP does not start a listener or upload anything.
`allowImageResampling` only affects PDF, never Web image identity checks.

## Native pipeline and independent verification

1. An isolated worker loads a native input snapshot and selects native diagrams.
2. The installed offscreen renderer produces root and nonempty embedded
   subprocess PNG surfaces. These are staged under relative `files/diagrams/`
   paths; absolute worker paths are not used as page image URLs.
3. The installed `IGeneratorFactory` / `WebGenerator` generates the site using
   the installed HTMLViewer assets and the default native logo. It only receives
   a fresh operation-owned output directory, never an existing user site.
4. The native search mapper supplies entries for selected root and subprocess
   surfaces. Modeler 4.3's generator otherwise drops nested search containers
   when filtering to root pages. The adapter retains its original configuration
   privately and projects the actual native search entries into the generated
   configuration. Installed viewer JavaScript/CSS and vendor binaries are
   unchanged; no handcrafted search entries substitute for the native mapper.
5. A separate worker reads durable files. It parses the exact configuration
   assignment as bounded JSON, never executes it, rejects duplicate JSON keys,
   links and unconfined image paths, and hashes the asset inventory.
6. `NativeWebPublicationPolicy` compares selected roots, all rendered page and
   search identities, page names, parent navigation and full PNG hashes with
   actual native graph/render outputs. General title/content checks still run.
7. The original input revision remains unchanged. Native failures and
   cancellation retain their real terminal state; a subsequent publication
   must independently succeed to demonstrate recovery.

`NativePublicationReadback.Web` contains `ModelName`, `RootPageIds`,
`SearchContainerIds`, `Pages` and `Assets`. Each page records its native ID,
parent ID, name, relative image path and SHA-256. Each asset records its relative
path, length and SHA-256. This inventory does not imply that every possible
browser, script behavior, attachment type or layout has been reviewed.

## Reproduce the real acceptance corpus

```powershell
dotnet run --project tests/McpBizagi.Acceptance -c Release --no-build -- . --native --web-publication-only
```

The independent MCP client creates two native diagrams, adds root/nested tasks
and Unicode descriptions, stores an actual embedded text attachment, publishes
selected/all diagrams, checks exclusions and attachment bytes, exercises invalid
selection and active-registration cancellation, recovers, and rereads the source.
It does not call server implementation classes or use a simulated generator.

## Scope and privacy

- Native execution/readback and browser usability are separate acceptance gates.
  Desktop navigation, nested search, loaded native diagram images and the
  documented attachment were exercised against actual generated output. The
  broader desktop/mobile accessibility and visual-quality gate is not accredited.
- Site metadata can contain authors, local identities, descriptions and embedded
  files. Review actual output before sharing. Raw acceptance sites and screenshots
  remain private; no fabricated public demonstration or sanitized success fixture
  is substituted for them.
- Generated HTMLViewer files are user publication artifacts. They are not added
  to the MCP source repository or Windows distribution.
- Custom logos/templates, SharePoint/Wiki publishing, every extended-attribute
  representation, remote hosting, universal mobile behavior and browser coverage
  remain separate work. Successful file publication does not prove independent
  desktop Modeler GUI compatibility or live-unsaved editing.

See [the execution baseline](validation-web-publication.md) for exact tested scope
and retained diagnostics, including unsuccessful attempts.
