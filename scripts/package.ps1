#Requires -Version 7.2
<#
.SYNOPSIS
Build a framework-dependent Windows package with dependency licenses and hashes.
.DESCRIPTION
Requires a restored checkout and network access for license texts absent from
NuGet packages. Upstream license requests use the exact repository commit from
the package metadata. Never reads or redistributes the Bizagi installation.
#>
param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
if (-not $IsWindows) { throw 'The native worker package must be built on Windows.' }
[xml]$properties = Get-Content -LiteralPath (Join-Path $repo 'Directory.Build.props')
$version = $properties.Project.PropertyGroup.Version
if (-not $OutputDirectory) {
    $stamp = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')
    $OutputDirectory = Join-Path $repo "artifacts/MCP-Bizagi-$version-win-x64-$stamp"
}
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw "Destination already exists: $output" }
New-Item -ItemType Directory -Path $output | Out-Null

Push-Location $repo
try {
    # Framework-dependent output deliberately excludes the .NET runtime and apphost.
    & dotnet restore --locked-mode
    if ($LASTEXITCODE) { throw 'Locked restore failed.' }
    & dotnet publish src/McpBizagi.Server -c Release --no-restore -p:UseAppHost=false -p:CopyOutputSymbolsToPublishDirectory=false -o $output
    if ($LASTEXITCODE) { throw 'Host publish failed.' }
    & dotnet build src/McpBizagi.Worker -c Release --no-restore
    if ($LASTEXITCODE) { throw 'Worker build failed.' }
    $worker = Join-Path $output 'worker'
    New-Item -ItemType Directory -Path $worker | Out-Null
    Get-ChildItem 'src/McpBizagi.Worker/bin/Release/net48' -File |
        Where-Object { $_.Extension -in '.dll', '.exe', '.config', '.json' } |
        Copy-Item -Destination $worker
    foreach ($name in 'README.md', 'CHANGELOG.md', 'LICENSE', 'ATTRIBUTION.md', 'THIRD-PARTY-NOTICES.md', 'SECURITY.md', 'CONTRIBUTING.md') {
        Copy-Item -LiteralPath (Join-Path $repo $name) -Destination $output
    }
    Copy-Item -LiteralPath (Join-Path $repo 'docs') -Destination $output -Recurse
    Copy-Item -LiteralPath (Join-Path $repo 'examples') -Destination $output -Recurse

    $licenses = Join-Path $output 'licenses'
    New-Item -ItemType Directory -Path $licenses | Out-Null
    $seen = @{}
    $downloadCache = @{}
    $inventory = [Collections.Generic.List[object]]::new()
    foreach ($project in 'McpBizagi.Server', 'McpBizagi.Worker') {
        $assets = Get-Content -LiteralPath "src/$project/obj/project.assets.json" -Raw | ConvertFrom-Json -AsHashtable
        foreach ($target in $assets.targets.Values) {
            foreach ($key in $target.Keys) {
                $entry = $target[$key]
                if ($entry.type -ne 'package' -or $seen.ContainsKey($key)) { continue }
                if (-not ($entry.runtime -or $entry.native -or $entry.runtimeTargets)) { continue }
                $seen[$key] = $true
                $meta = $assets.libraries[$key]
                $directory = $null
                foreach ($folder in $assets.packageFolders.Keys) {
                    $candidate = Join-Path $folder $meta.path
                    if (Test-Path -LiteralPath $candidate) { $directory = $candidate; break }
                }
                if (-not $directory) { throw "Restored package not found: $key" }
                $nuspec = Get-ChildItem -LiteralPath $directory -Filter '*.nuspec' | Select-Object -First 1
                [xml]$spec = Get-Content -LiteralPath $nuspec.FullName
                $metadata = $spec.package.metadata
                $destination = Join-Path $licenses ($key -replace '/', '-')
                New-Item -ItemType Directory -Path $destination | Out-Null
                Copy-Item -LiteralPath $nuspec.FullName -Destination $destination
                $licenseFiles = @($meta.files | Where-Object { $_ -match '(?i)(license|notice|copyright)' })
                foreach ($relative in $licenseFiles) {
                    # Preserve subdirectories so similarly named notices cannot overwrite each other.
                    $to = Join-Path $destination $relative
                    New-Item -ItemType Directory -Path (Split-Path -Parent $to) -Force | Out-Null
                    Copy-Item -LiteralPath (Join-Path $directory $relative) -Destination $to
                }
                $source = 'NuGet package'
                if (-not ($licenseFiles | Where-Object { $_ -match '(?i)license' })) {
                    $repository = [string]$metadata.repository.url
                    $commit = [string]$metadata.repository.commit
                    if ($repository -notmatch '^https://github\.com/([\w.-]+/[\w.-]+?)(?:\.git)?/?$' -or $commit -notmatch '^[0-9a-f]{40}$') {
                        throw "No embedded license or pinned GitHub license source for $key. Review manually."
                    }
                    # Extract independently because the preceding commit check also uses regex state.
                    $upstream = ([regex]::Match($repository, '^https://github\.com/(.+?)(?:\.git)?/?$')).Groups[1].Value
                    $cacheKey = "$upstream/$commit"
                    if (-not $downloadCache.ContainsKey($cacheKey)) {
                        $found = $null
                        foreach ($name in 'LICENSE', 'LICENSE.txt', 'LICENSE.md', 'License.txt', 'LICENSE.TXT') {
                            $url = "https://raw.githubusercontent.com/$cacheKey/$name"
                            try {
                                $response = Invoke-WebRequest -Uri $url
                                $found = @{ text = [string]$response.Content; source = $url }
                                break
                            } catch {
                                if ($_.Exception.Response.StatusCode.value__ -ne 404) { throw }
                            }
                        }
                        if (-not $found) { throw "Pinned upstream license not found for $key. Review manually." }
                        $downloadCache[$cacheKey] = $found
                    }
                    $found = $downloadCache[$cacheKey]
                    $source = $found.source
                    [IO.File]::WriteAllText((Join-Path $destination 'UPSTREAM-LICENSE.txt'), $found.text)
                }
                $inventory.Add([ordered]@{
                    package = $key; license = [string]$metadata.license.InnerText
                    copyright = [string]$metadata.copyright; upstream = [string]$metadata.repository.url
                    licenseSource = $source; nugetSha512 = $meta.sha512
                })
            }
        }
    }
    $inventory | Sort-Object { $_.package } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $output 'dependencies.json') -Encoding utf8
    # Defense in depth against accidentally copying locally loaded engine/renderer components from a build folder.
    $vendor = Get-ChildItem -LiteralPath $output -File -Recurse | Where-Object { $_.Name -match '^(Bizagi|CefSharp\.|libcef\.|Lanner\.)' -or $_.Extension -eq '.bpm' }
    if ($vendor) { throw 'Unexpected vendor component or native operator model in package.' }
    $head = & git rev-parse HEAD
    $dirty = [bool](& git status --porcelain)
    [ordered]@{ version = $version; commit = $head; dirtyCheckout = $dirty; builtAtUtc = [DateTime]::UtcNow.ToString('O')
        architecture = 'win-x64'; frameworkDependent = $true; requires = @('.NET 10 runtime', '.NET Framework 4.8', 'Bizagi Modeler 4.3.0.008 for native diagnostics') } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'package.json') -Encoding utf8
    $manifest = Get-ChildItem -LiteralPath $output -File -Recurse | Sort-Object FullName | ForEach-Object {
        [ordered]@{ path = [IO.Path]::GetRelativePath($output, $_.FullName).Replace('\', '/'); sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
    }
    $manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'manifest.sha256.json') -Encoding utf8
    Compress-Archive -Path (Join-Path $output '*') -DestinationPath "$output.zip"
    Write-Output "Package directory: $output"
    Write-Output "Archive: $output.zip"
    Write-Output "SHA256: $((Get-FileHash -LiteralPath "$output.zip" -Algorithm SHA256).Hash)"
} finally { Pop-Location }
