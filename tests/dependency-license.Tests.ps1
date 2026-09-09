#Requires -Version 7.2
# Filesystem policy tests only, not native engine or package operational acceptance.
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
. (Join-Path $repo 'scripts/dependency-license.ps1')
$root = Join-Path $repo ('.local/test-results/license-policy/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root -Force | Out-Null
$sha = 'IlKyVvu9DCcc/xDAW2yw9N1i3xMzaogrL7oa4dE8/IFH3pt0loQo4O2TUKKeAvPgAKgNz45YSyD2/8qdTj8l6Q=='
$count = 0
foreach ($case in 'valid', 'hash', 'license', 'text', 'escape', 'mutable-source', 'duplicate', 'missing-version') {
    $directory = Join-Path $root $case
    Copy-Item -LiteralPath (Join-Path $repo 'licenses') -Destination $directory -Recurse
    $manifest = Join-Path $directory 'reviewed-sources.json'
    $entries = @(Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json -AsHashtable)
    if ($case -eq 'escape') { $entries[0].file = '../LICENSE.txt' }
    if ($case -eq 'mutable-source') { $entries[0].source = $entries[0].source -replace '/[0-9a-f]{40}/', '/master/' }
    if ($case -eq 'duplicate') { $entries = @($entries[0], $entries[0]) }
    ConvertTo-Json -InputObject $entries | Set-Content -LiteralPath $manifest
    if ($case -eq 'text') { Add-Content -LiteralPath (Join-Path $directory 'Msagl-1.2.1/LICENSE.txt') 'Altered fixture.' }
    $failed = $false; $result = $null
    try {
        $result = Get-ReviewedDependencyLicense -LicenseRoot $directory -Package $(if ($case -eq 'missing-version') { 'Msagl/1.2.2' } else { 'Msagl/1.2.1' }) `
            -NugetSha512 $(if ($case -eq 'hash') { 'changed' } else { $sha }) -DeclaredLicense $(if ($case -eq 'license') { 'Apache-2.0' } else { 'MIT' })
    } catch { $failed = $true }
    if ($case -eq 'valid') {
        if ($failed -or -not $result.text.Contains('Microsoft Corporation')) { throw 'Reviewed license did not resolve.' }
    } elseif ($case -eq 'missing-version') {
        if ($failed -or $null -ne $result) { throw 'Reviewed license was reused for another version.' }
    } elseif (-not $failed) { throw "Unsafe license policy result: $case" }
    $count++
}
# Validate every checked-in text, including entries that share identical
# upstream license bytes. Package-specific hashes must remain independent.
$reviewed=@(Get-Content (Join-Path $repo 'licenses/reviewed-sources.json') -Raw|ConvertFrom-Json)
foreach($entry in $reviewed){
    $actual=Get-ReviewedDependencyLicense -LicenseRoot (Join-Path $repo 'licenses') -Package $entry.package -NugetSha512 $entry.nugetSha512 -DeclaredLicense $entry.license
    if(-not $actual -or [string]::IsNullOrWhiteSpace($actual.text) -or $actual.source -cne $entry.source){throw 'Reviewed cache entry did not resolve exactly.'}
}
Write-Output "DEPENDENCY_LICENSE_POLICY_PASS cases=$count reviewedEntries=$($reviewed.Count)"
