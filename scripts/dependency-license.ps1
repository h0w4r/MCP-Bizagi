# Resolve explicitly reviewed missing license texts without trusting a mutable upstream branch.
function Get-ReviewedDependencyLicense {
    param([string]$LicenseRoot, [string]$Package, [string]$NugetSha512, [string]$DeclaredLicense)
    $root = [IO.Path]::GetFullPath($LicenseRoot)
    $manifest = Join-Path $root 'reviewed-sources.json'
    if (-not (Test-Path -LiteralPath $manifest)) { return $null }
    $entries = @(Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json -AsHashtable | Where-Object package -CEQ $Package)
    if ($entries.Count -eq 0) { return $null }
    if ($entries.Count -ne 1) { throw "Ambiguous reviewed dependency license: $Package" }
    $entry = $entries[0]
    if ($entry.nugetSha512 -cne $NugetSha512 -or $entry.license -cne $DeclaredLicense -or
        $entry.sha256 -cnotmatch '^[0-9a-f]{64}$' -or
        $entry.source -cnotmatch '^https://raw\.githubusercontent\.com/[\w.-]+/[\w.-]+/[0-9a-f]{40}/[^?#]+$') {
        throw "Reviewed license identity or immutable provenance mismatch: $Package"
    }
    $relative = [string]$entry.file
    if (-not $relative -or [IO.Path]::IsPathRooted($relative) -or $relative.Contains(':') -or $relative.Contains('\') -or
        @($relative.Split('/') | Where-Object { $_ -in '', '.', '..' }).Count) { throw 'Unconfined reviewed license path.' }
    $file = [IO.Path]::GetFullPath((Join-Path $root $relative))
    if (-not $file.StartsWith($root.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Unconfined reviewed license path.'
    }
    # Check each path component: a checked-in text must not resolve through a local link.
    $current = $file
    while ($current.Length -ge $root.Length) {
        if ((Get-Item -LiteralPath $current).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Linked reviewed license path.' }
        $current = Split-Path -Parent $current
    }
    if ((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant() -cne $entry.sha256) {
        throw "Reviewed license text hash mismatch: $Package"
    }
    return @{ text = [IO.File]::ReadAllText($file); source = [string]$entry.source; review = $entry }
}
