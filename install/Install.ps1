[CmdletBinding()]
param([string]$CodexDirectory)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

function Assert-RegularDirectory([string]$Path) {
    if (Test-Path -LiteralPath $Path) {
        $item = Get-Item -LiteralPath $Path -Force
        if (-not $item.PSIsContainer -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw "Expected a regular directory: $Path" }
    }
}

$files = [ordered]@{
    'pet/spritesheet.png' = 'spritesheet.png'
    'quota/CodexGirlQuota.exe' = 'quota/CodexGirlQuota.exe'
    'quota/CodexGirlQuota.exe.config' = 'quota/CodexGirlQuota.exe.config'
    'Start-Quota.cmd' = 'Start-Quota.cmd'
    'pet/pet.json' = 'pet.json'
}
$checks = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'package-checksums.json') -Raw | ConvertFrom-Json
foreach ($file in $files.Keys) {
    $source = Join-Path $PSScriptRoot $file
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw "Missing file: $file" }
    if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() -ne $checks.$file) { throw "Verification failed: $file" }
}
$manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'pet/pet.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if ($manifest.id -ne 'codex-girl' -or $manifest.spriteVersionNumber -ne 2 -or $manifest.spritesheetPath -ne 'spritesheet.png') { throw 'Unexpected pet manifest.' }
$png = [IO.File]::ReadAllBytes((Join-Path $PSScriptRoot 'pet/spritesheet.png'))
if ($png.Length -lt 24 -or [BitConverter]::ToString($png[0..7]) -ne '89-50-4E-47-0D-0A-1A-0A' -or [BitConverter]::ToString($png[16..23]) -ne '00-00-06-00-00-00-08-F0') { throw 'Expected a 1536 x 2288 PNG.' }

if ([string]::IsNullOrWhiteSpace($CodexDirectory)) {
    $CodexDirectory = $env:CODEX_HOME
    if ([string]::IsNullOrWhiteSpace($CodexDirectory)) {
        $profilePath = [Environment]::GetFolderPath('UserProfile')
        if ([string]::IsNullOrWhiteSpace($profilePath)) { $profilePath = $env:USERPROFILE }
        $CodexDirectory = Join-Path $profilePath '.codex'
    }
}
if (-not [IO.Path]::IsPathRooted($CodexDirectory)) { throw 'CodexDirectory must be absolute.' }
$codexPath = [IO.Path]::GetFullPath($CodexDirectory)
$pets = Join-Path $codexPath 'pets'
$target = Join-Path $pets 'codex-girl'
foreach ($dir in @($codexPath, $pets, $target, (Join-Path $target 'quota'))) { Assert-RegularDirectory $dir }
[void][IO.Directory]::CreateDirectory($target)
$changed = $false
$existing = @()
foreach ($file in $files.Keys) {
    $destination = Join-Path $target $files[$file]
    if (Test-Path -LiteralPath $destination) {
        $item = Get-Item -LiteralPath $destination -Force
        if ($item.PSIsContainer -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw "Expected a regular file: $destination" }
        $existing += $file
        if ((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash.ToLowerInvariant() -ne $checks.$file) { $changed = $true }
    } else { $changed = $true }
}
if ($changed) {
    $backup = $null
    if ($existing.Count -gt 0) {
        Assert-RegularDirectory (Join-Path $codexPath 'pet-backups')
        $backup = Join-Path $codexPath ('pet-backups/codex-girl-' + [Guid]::NewGuid().ToString('N'))
        foreach ($file in $existing) {
            $saved = Join-Path $backup $files[$file]
            [void][IO.Directory]::CreateDirectory((Split-Path $saved -Parent))
            Copy-Item -LiteralPath (Join-Path $target $files[$file]) -Destination $saved
        }
    }
    try {
        foreach ($file in $files.Keys) {
            $destination = Join-Path $target $files[$file]
            [void][IO.Directory]::CreateDirectory((Split-Path $destination -Parent))
            Copy-Item -LiteralPath (Join-Path $PSScriptRoot $file) -Destination $destination -Force
            if ((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash.ToLowerInvariant() -ne $checks.$file) { throw "Installed file verification failed: $file" }
        }
    } catch {
        foreach ($file in $files.Keys) {
            $destination = Join-Path $target $files[$file]
            if ($existing -contains $file) { Copy-Item -LiteralPath (Join-Path $backup $files[$file]) -Destination $destination -Force }
            elseif (Test-Path -LiteralPath $destination -PathType Leaf) { Remove-Item -LiteralPath $destination }
        }
        throw
    }
    if ($backup) { Write-Host "Backup: $backup" }
}
Write-Host "Installed: $target" -ForegroundColor Green
Write-Host 'Use Start-Quota.cmd to open the quota companion.'
