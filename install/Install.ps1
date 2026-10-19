[CmdletBinding()]
param([string]$CodexDirectory, [switch]$NoOpenFolder)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

function Assert-RegularDirectory([string]$Path) {
    if (Test-Path -LiteralPath $Path) {
        $item = Get-Item -LiteralPath $Path -Force
        if (-not $item.PSIsContainer -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw "Expected a regular directory: $Path"
        }
    }
}

$package = Join-Path $PSScriptRoot 'pet'
$manifestPath = Join-Path $package 'pet.json'
$sheetPath = Join-Path $package 'spritesheet.png'
$checks = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'package-checksums.json') -Raw | ConvertFrom-Json
foreach ($file in @('pet.json', 'spritesheet.png')) {
    $path = Join-Path $package $file
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing file: $file" }
    $expected = $checks.$file
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expected) {
        throw "File verification failed: $file. Please download the ZIP again."
    }
}
$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
if ($manifest.id -ne 'codex-girl' -or $manifest.spriteVersionNumber -ne 2 -or $manifest.spritesheetPath -ne 'spritesheet.png') {
    throw 'Unexpected pet manifest.'
}
$bytes = [IO.File]::ReadAllBytes($sheetPath)
if ($bytes.Length -lt 24 -or [BitConverter]::ToString($bytes[0..7]) -ne '89-50-4E-47-0D-0A-1A-0A' -or
    [BitConverter]::ToString($bytes[16..23]) -ne '00-00-06-00-00-00-08-F0') {
    throw 'Expected a 1536 x 2288 PNG sprite sheet.'
}

if ([string]::IsNullOrWhiteSpace($CodexDirectory)) {
    if (-not [string]::IsNullOrWhiteSpace($env:CODEX_HOME)) { $CodexDirectory = $env:CODEX_HOME }
    else { $CodexDirectory = Join-Path ([Environment]::GetFolderPath('UserProfile')) '.codex' }
}
if (-not [IO.Path]::IsPathRooted($CodexDirectory)) { throw 'CodexDirectory must be an absolute path.' }
$codexPath = [IO.Path]::GetFullPath($CodexDirectory)
$petsPath = Join-Path $codexPath 'pets'
$target = Join-Path $petsPath 'codex-girl'
foreach ($dir in @($codexPath, $petsPath, $target)) { Assert-RegularDirectory $dir }
[void][IO.Directory]::CreateDirectory($target)

$changed = $false
foreach ($file in @('pet.json', 'spritesheet.png')) {
    $destination = Join-Path $target $file
    if (Test-Path -LiteralPath $destination) {
        $item = Get-Item -LiteralPath $destination -Force
        if ($item.PSIsContainer -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw "Expected a regular file: $destination"
        }
    }
    if (-not (Test-Path -LiteralPath $destination -PathType Leaf) -or
        (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash.ToLowerInvariant() -ne $checks.$file) {
        $changed = $true
    }
}

if ($changed) {
    $backup = $null
    $existing = @('pet.json', 'spritesheet.png') | Where-Object { Test-Path -LiteralPath (Join-Path $target $_) -PathType Leaf }
    if (@($existing).Count -gt 0) {
        $backup = Join-Path $codexPath ('pet-backups\codex-girl-' + [DateTime]::Now.ToString('yyyyMMdd-HHmmss-fff') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
        Assert-RegularDirectory (Join-Path $codexPath 'pet-backups')
        [void][IO.Directory]::CreateDirectory($backup)
        foreach ($file in $existing) { Copy-Item -LiteralPath (Join-Path $target $file) -Destination (Join-Path $backup $file) }
    }
    try {
        # Write the sheet first, then publish the manifest for the pet picker.
        foreach ($file in @('spritesheet.png', 'pet.json')) {
            Copy-Item -LiteralPath (Join-Path $package $file) -Destination (Join-Path $target $file) -Force
        }
        foreach ($file in @('pet.json', 'spritesheet.png')) {
            if ((Get-FileHash -LiteralPath (Join-Path $target $file) -Algorithm SHA256).Hash.ToLowerInvariant() -ne $checks.$file) {
                throw "Installed file verification failed: $file"
            }
        }
    } catch {
        foreach ($file in @('pet.json', 'spritesheet.png')) {
            $destination = Join-Path $target $file
            if ($backup -and (Test-Path -LiteralPath (Join-Path $backup $file) -PathType Leaf)) {
                Copy-Item -LiteralPath (Join-Path $backup $file) -Destination $destination -Force
            } elseif ($existing -notcontains $file -and (Test-Path -LiteralPath $destination -PathType Leaf)) {
                Remove-Item -LiteralPath $destination
            }
        }
        throw
    }
    if ($backup) { Write-Host "Backup: $backup" }
}
Write-Host ''
Write-Host 'Codex娘 installed successfully.' -ForegroundColor Green
Write-Host "Location: $target"
Write-Host 'Open Settings > Pets > Refresh, then select Codex娘.'
Write-Host 'If it does not appear, restart the desktop app. Use /pet to show it.'
if (-not $NoOpenFolder) { Start-Process -FilePath 'explorer.exe' -ArgumentList ('"' + $target + '"') }
