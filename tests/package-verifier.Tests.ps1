#Requires -Version 7.2
<#
.SYNOPSIS
Exercise package integrity policy with tiny filesystem fixtures, not runtime mocks.
.DESCRIPTION
These tests never accredit the MCP/native engine or a real release. Fixtures remain
under ignored .local/test-results for diagnosis; no operator files are removed.
#>
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$verify = Join-Path $repo 'scripts/verify-package.ps1'
$root = Join-Path $repo ('.local/test-results/package-policy/' + [Guid]::NewGuid().ToString('N'))
$commit = '1111111111111111111111111111111111111111'
$results = [Collections.Generic.List[object]]::new()

function Write-Manifest([string]$Directory) {
    $rows = @(Get-ChildItem -LiteralPath $Directory -File -Recurse | Where-Object Name -ne 'manifest.sha256.json' | ForEach-Object {
        @{ path = [IO.Path]::GetRelativePath($Directory, $_.FullName).Replace('\', '/'); sha256 = (Get-FileHash -LiteralPath $_.FullName).Hash.ToLowerInvariant() }
    })
    ConvertTo-Json -InputObject $rows | Set-Content -LiteralPath (Join-Path $Directory 'manifest.sha256.json')
}

function New-Fixture([string]$Name) {
    $directory = Join-Path $root $Name
    New-Item -ItemType Directory -Path (Join-Path $directory 'worker') | Out-Null
    foreach ($file in 'McpBizagi.Server.dll', 'McpBizagi.Server.deps.json', 'McpBizagi.Server.runtimeconfig.json',
        'worker/McpBizagi.Worker.exe', 'worker/McpBizagi.Worker.exe.config', 'worker/McpBizagi.BizagiAdapter.dll',
        'LICENSE', 'ATTRIBUTION.md', 'THIRD-PARTY-NOTICES.md', 'dependencies.json') {
        [IO.File]::WriteAllText((Join-Path $directory $file), 'Policy fixture only; never executed: ' + $file)
    }
    @{ version = '0.6.0-alpha.1'; commit = $commit; dirtyCheckout = $false; architecture = 'win-x64'; frameworkDependent = $true } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $directory 'package.json')
    Write-Manifest $directory
    return $directory
}

function Check([string]$Name, [scriptblock]$Edit, [bool]$ShouldPass = $false, [switch]$AllowDirty) {
    $directory = New-Fixture $Name
    & $Edit $directory
    $passed = $false; $diagnostic = $null
    try {
        $receipt = & $verify -PackageDirectory $directory -ExpectedCommit $commit -AllowDirtyCheckout:$AllowDirty | ConvertFrom-Json
        $passed = $receipt.verified -eq $true -and $receipt.operationalAcceptance -eq 'not_performed_by_manifest_verifier'
    } catch { $diagnostic = $_.Exception.Message }
    if ($passed -ne $ShouldPass) { throw "Unexpected verifier result for ${Name}: $diagnostic" }
    $results.Add(@{ name = $Name; expectedAccepted = $ShouldPass; accepted = $passed; diagnostic = $diagnostic })
}

Check 'clean' { param($d) } $true
Check 'changed-bytes' { param($d) Add-Content -LiteralPath (Join-Path $d 'LICENSE') -Value 'changed' }
Check 'extra-file' { param($d) Set-Content -LiteralPath (Join-Path $d 'extra.txt') -Value 'unexpected' }
Check 'missing-file' { param($d) Remove-Item -LiteralPath (Join-Path $d 'LICENSE') }
Check 'missing-required-even-with-valid-manifest' { param($d) Remove-Item -LiteralPath (Join-Path $d 'LICENSE'); Write-Manifest $d }
Check 'duplicate-case-spelling' { param($d)
    $path = Join-Path $d 'manifest.sha256.json'; $rows = @(Get-Content -LiteralPath $path -Raw | ConvertFrom-Json -AsHashtable)
    $rows += @{ path = $rows[0].path.ToUpperInvariant(); sha256 = $rows[0].sha256 }
    ConvertTo-Json -InputObject $rows | Set-Content -LiteralPath $path
}
Check 'traversal' { param($d)
    $path = Join-Path $d 'manifest.sha256.json'; $rows = @(Get-Content -LiteralPath $path -Raw | ConvertFrom-Json -AsHashtable)
    $rows[0].path = '../outside.txt'; ConvertTo-Json -InputObject $rows | Set-Content -LiteralPath $path
}
Check 'manifest-self-reference' { param($d)
    $path = Join-Path $d 'manifest.sha256.json'; $rows = @(Get-Content -LiteralPath $path -Raw | ConvertFrom-Json -AsHashtable)
    $rows[0].path = 'manifest.sha256.json'; ConvertTo-Json -InputObject $rows | Set-Content -LiteralPath $path
}
Check 'invalid-digest' { param($d)
    $path = Join-Path $d 'manifest.sha256.json'; $rows = @(Get-Content -LiteralPath $path -Raw | ConvertFrom-Json -AsHashtable)
    $rows[0].sha256 = 'invalid'; ConvertTo-Json -InputObject $rows | Set-Content -LiteralPath $path
}
Check 'wrong-source-commit' { param($d)
    $path = Join-Path $d 'package.json'; $json = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json -AsHashtable
    $json.commit = '2222222222222222222222222222222222222222'; $json | ConvertTo-Json | Set-Content -LiteralPath $path; Write-Manifest $d
}
$dirty = { param($d)
    $path = Join-Path $d 'package.json'; $json = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json -AsHashtable
    $json.dirtyCheckout = $true; $json | ConvertTo-Json | Set-Content -LiteralPath $path; Write-Manifest $d
}
Check 'dirty-source' $dirty
Check 'explicit-local-dirty-source' $dirty $true -AllowDirty
foreach ($forbiddenName in 'operator.bpm', 'definitions.bca', 'private.pdb', 'Aspose.Diagram.dll', 'BizagiModeler.exe') {
    Check ('forbidden-' + $forbiddenName) { param($d) Set-Content -LiteralPath (Join-Path $d $forbiddenName) -Value 'policy fixture'; Write-Manifest $d }
}
if ($IsWindows) {
    # Junctions require no symlink privilege. The target is another fresh fixture
    # directory, and the verifier must reject the link without trusting its contents.
    Check 'directory-junction' { param($d)
        $target = Join-Path $root 'junction-target'; New-Item -ItemType Directory -Path $target | Out-Null
        New-Item -ItemType Junction -Path (Join-Path $d 'linked') -Target $target | Out-Null
    }
}
$results | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $root 'results.json')
Write-Output "PACKAGE_POLICY_TESTS_PASS cases=$($results.Count) evidence=$root (not native acceptance)"
