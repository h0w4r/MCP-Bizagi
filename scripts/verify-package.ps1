#Requires -Version 7.2
<#
.SYNOPSIS
Verify an extracted distribution's complete file set, hashes and source provenance.
.DESCRIPTION
Read-only inspection. Does not execute packaged code or certify native capabilities.
Compare the ZIP checksum from a trusted release before trusting its own manifest.
#>
param(
    [Parameter(Mandatory)][string]$PackageDirectory,
    [string]$ExpectedCommit,
    [switch]$AllowDirtyCheckout
)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($PackageDirectory))
$prefix = $root.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
if (-not (Test-Path -LiteralPath $root -PathType Container)) { throw 'Package directory does not exist.' }
if ((Get-Item -LiteralPath $root -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Package root must not be a link.' }
# Inspect links before enumerating files so the verifier cannot leave the selected tree.
$entries = @(Get-ChildItem -LiteralPath $root -Recurse -Force)
if ($entries | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }) { throw 'Package links are not supported.' }
$manifestPath = Join-Path $root 'manifest.sha256.json'
$manifest = @(Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json -AsHashtable)
if ($manifest.Count -eq 0) { throw 'Package manifest is empty.' }
$expected = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($item in $manifest) {
    # A manifest entry must be a portable relative file path, not an alternate data
    # stream, escaped path, duplicate spelling, drive path or manifest self-reference.
    $relative = [string]$item.path
    if (-not $relative -or $relative.Contains('\') -or $relative.Contains(':') -or
        $relative.StartsWith('/') -or $relative -eq 'manifest.sha256.json' -or
        @($relative.Split('/') | Where-Object { $_ -in '', '.', '..' -or $_.TrimEnd(' ', '.') -ne $_ }).Count -ne 0 -or
        -not $expected.Add($relative) -or [string]$item.sha256 -cnotmatch '^[0-9a-f]{64}$') {
        throw "Invalid or duplicate manifest entry: $relative"
    }
    $path = [IO.Path]::GetFullPath((Join-Path $root $relative))
    if (-not $path.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase) -or
        -not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing or unconfined package file: $relative" }
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -cne [string]$item.sha256) {
        throw "Package hash mismatch: $relative"
    }
}
$files = @($entries | Where-Object { -not $_.PSIsContainer })
$actual = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($file in $files) {
    $relative = [IO.Path]::GetRelativePath($root, $file.FullName).Replace('\', '/')
    if ($relative -ne 'manifest.sha256.json') { [void]$actual.Add($relative) }
    if ($file.Name -match '^(Bizagi|Aspose\.|CefSharp\.|libcef\.|Lanner\.)' -or $file.Extension -in '.bpm', '.bca', '.pdb') {
        throw "Unexpected vendor component, native model or symbol file: $relative"
    }
}
if (-not $expected.SetEquals($actual)) { throw 'Package contains unmanifested or missing files.' }
$provenance = Get-Content -LiteralPath (Join-Path $root 'package.json') -Raw | ConvertFrom-Json -AsHashtable
if ([string]$provenance.commit -cnotmatch '^[0-9a-f]{40}$' -or [string]::IsNullOrWhiteSpace($provenance.version) -or
    $provenance.dirtyCheckout -isnot [bool] -or $provenance.frameworkDependent -ne $true -or $provenance.architecture -ne 'win-x64') {
    throw 'Invalid package provenance.'
}
if (-not $AllowDirtyCheckout -and $provenance.dirtyCheckout) { throw 'Release verification requires a clean source checkout.' }
if ($ExpectedCommit -and $provenance.commit -cne $ExpectedCommit) { throw 'Package source commit does not match the expected commit.' }
foreach ($required in 'McpBizagi.Server.dll', 'McpBizagi.Server.deps.json', 'McpBizagi.Server.runtimeconfig.json',
    'worker/McpBizagi.Worker.exe', 'worker/McpBizagi.Worker.exe.config', 'worker/McpBizagi.BizagiAdapter.dll',
    'live/McpBizagi.LiveHost.exe', 'live/McpBizagi.LiveHost.exe.config', 'live/McpBizagi.BizagiAdapter.dll',
    'owner/McpBizagi.LiveOwner.exe', 'owner/McpBizagi.LiveOwner.dll', 'owner/McpBizagi.LiveOwner.runtimeconfig.json',
    'LICENSE', 'ATTRIBUTION.md', 'THIRD-PARTY-NOTICES.md', 'dependencies.json') {
    if (-not $expected.Contains($required)) { throw "Required distribution file is missing: $required" }
}
[ordered]@{ verified = $true; directory = $root; files = $expected.Count; version = $provenance.version;
    commit = $provenance.commit; dirtyCheckout = $provenance.dirtyCheckout; operationalAcceptance = 'not_performed_by_manifest_verifier' } |
    ConvertTo-Json
