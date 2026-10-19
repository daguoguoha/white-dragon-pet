$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$installer=Join-Path $root 'native\Install-Materials.ps1'
function Assert($value,$name){if(-not $value){throw "FAIL: $name"};Write-Host "PASS: $name"}
$testHome=Join-Path $root ('dist\native-tests-'+[Guid]::NewGuid().ToString('N'))
$target=Join-Path $testHome 'pets\codex-girl'
[void][IO.Directory]::CreateDirectory((Join-Path $target 'quota'))
[IO.File]::WriteAllText((Join-Path $target 'spritesheet.png'),'untouched original sheet')
[IO.File]::WriteAllText((Join-Path $target 'quota\keep.txt'),'untouched app data')
[IO.File]::WriteAllText((Join-Path $target 'pet.json'),'{"id":"codex-girl","displayName":"Codex girl","description":"original","spriteVersionNumber":2,"spritesheetPath":"spritesheet.png"}')
& $installer -Food fish -CodexDirectory $testHome
$manifest=Get-Content (Join-Path $target 'pet.json') -Raw -Encoding UTF8|ConvertFrom-Json
Assert ($manifest.id -eq 'codex-girl' -and $manifest.spritesheetPath -eq 'spritesheet-native.png') 'material install keeps the same pet identity and switches its sprite path'
Assert ((Get-Content (Join-Path $target 'spritesheet.png') -Raw) -eq 'untouched original sheet' -and (Get-Content (Join-Path $target 'quota\keep.txt') -Raw) -eq 'untouched app data') 'original atlas and standalone app data are untouched'
$backups=@(Get-ChildItem (Join-Path $testHome 'pet-backups') -Directory)
Assert ($backups.Count -eq 1 -and (Get-Content (Join-Path $backups[0].FullName 'pet.json') -Raw).Contains('original')) 'original manifest is backed up before the path changes'
& $installer -Food fish -CodexDirectory $testHome
Assert (@(Get-ChildItem (Join-Path $testHome 'pet-backups') -Directory).Count -eq 1) 'same material does not create a duplicate backup'
& $installer -Food cake -CodexDirectory $testHome
Assert (@(Get-ChildItem (Join-Path $testHome 'pets') -Directory).Count -eq 1) 'food switching does not create duplicate pets'
$cakeHash=(Get-FileHash (Join-Path $root 'native\packages\cake\spritesheet.png')).Hash
Assert ((Get-FileHash (Join-Path $target 'spritesheet-native.png')).Hash -eq $cakeHash) 'changing food publishes the selected native atlas'
$manifestPath=Join-Path $target 'pet.json';$before=[IO.File]::ReadAllText($manifestPath)
$lock=[IO.File]::Open($manifestPath,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
$failed=$false
try{try{& $installer -Food diamond -CodexDirectory $testHome}catch{$failed=$true}}finally{$lock.Dispose()}
Assert ($failed -and (Get-FileHash (Join-Path $target 'spritesheet-native.png')).Hash -eq $cakeHash -and [IO.File]::ReadAllText($manifestPath) -eq $before) 'manifest publication failure restores the previous material'
Assert (@(Get-ChildItem $target -Force -Filter '.native-materials-*').Count -eq 0) 'staging files are removed after failure'
Write-Host 'All native material install checks passed.'
