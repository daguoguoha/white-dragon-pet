$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase
$root = Split-Path $PSScriptRoot -Parent
[void][Reflection.Assembly]::LoadFrom((Join-Path $root 'runtime\CodexGirlQuota.exe'))
function Assert($condition, $name) { if (-not $condition) { throw "FAIL: $name" }; Write-Host "PASS: $name" }
$testRoot = Join-Path $root ('dist\import-tests-' + [Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($testRoot)
$sheet = Join-Path $root 'assets\spritesheet.png'
$testCodexDirectory = Join-Path $testRoot 'profile with spaces'
$result = [CodexGirl.PetInstaller]::Import($sheet, $testCodexDirectory)
$target = Join-Path $testCodexDirectory 'pets\codex-girl'
$manifest = Join-Path $target 'pet.json'
$image = Join-Path $target 'spritesheet.png'
Assert ((Test-Path -LiteralPath $manifest) -and -not $result.AlreadyInstalled) 'first import installs a native pet'
Assert ((Get-FileHash $image).Hash -eq (Get-FileHash $sheet).Hash) 'import preserves exact sprite bytes'
$stamp = (Get-Item $manifest).LastWriteTimeUtc
$repeat = [CodexGirl.PetInstaller]::Import($sheet, $testCodexDirectory)
Assert ($repeat.AlreadyInstalled -and (Get-Item $manifest).LastWriteTimeUtc -eq $stamp -and -not (Test-Path (Join-Path $testCodexDirectory 'pet-backups'))) 'repeat import reuses the entry without rewriting or backing up'
[IO.File]::WriteAllText((Join-Path $target 'keep.txt'),'unrelated file')
[IO.File]::WriteAllText($manifest,'{"displayName":"Old pet"}')
[IO.File]::WriteAllText($image,'old image bytes')
$upgrade = [CodexGirl.PetInstaller]::Import($sheet, $testCodexDirectory)
Assert ((Get-Content (Join-Path $upgrade.BackupPath 'spritesheet.png') -Raw) -eq 'old image bytes') 'replacement backs up old files'
Assert ((Get-Content (Join-Path $target 'keep.txt') -Raw) -eq 'unrelated file') 'unrelated files are preserved'
$badSheet = Join-Path $testRoot 'broken.png'
$bytes = [IO.File]::ReadAllBytes($sheet)
[IO.File]::WriteAllBytes($badSheet,[byte[]]$bytes[0..23])
$badHome = Join-Path $testRoot 'bad-source-home'
$rejected = $false
try { [void][CodexGirl.PetInstaller]::Import($badSheet,$badHome) } catch { $rejected = $true }
Assert ($rejected -and -not (Test-Path $badHome)) 'corrupt image is rejected before any installation files are created'
[IO.File]::WriteAllText($manifest,'{"displayName":"Locked old pet"}')
[IO.File]::WriteAllText($image,'rollback image bytes')
$lock = [IO.File]::Open($manifest,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
$failed = $false
try { try { [void][CodexGirl.PetInstaller]::Import($sheet,$testCodexDirectory) } catch { $failed = $true } } finally { $lock.Dispose() }
Assert ($failed -and (Get-Content $image -Raw) -eq 'rollback image bytes' -and (Get-Content $manifest -Raw) -eq '{"displayName":"Locked old pet"}') 'failed manifest publication restores the prior image'
Assert (@(Get-ChildItem (Join-Path $testCodexDirectory 'pets') -Directory -Filter '.codex-girl-import-*' -Force).Count -eq 0) 'temporary import files are cleaned up'
$outside = Join-Path $testRoot 'outside'
[void][IO.Directory]::CreateDirectory($outside)
$linkedHome = Join-Path $testRoot 'linked-home'
[void][IO.Directory]::CreateDirectory((Join-Path $linkedHome 'pets'))
[void](New-Item -ItemType Junction -Path (Join-Path $linkedHome 'pets\codex-girl') -Target $outside)
$rejected = $false
try { [void][CodexGirl.PetInstaller]::Import($sheet,$linkedHome) } catch { $rejected = $true }
Assert ($rejected -and -not (Test-Path (Join-Path $outside 'pet.json'))) 'linked destination is rejected'

$uiData = Join-Path $testRoot 'ui-data'
[void][IO.Directory]::CreateDirectory($uiData)
$window = New-Object CodexGirl.QuotaPetWindow $sheet,$uiData,$testCodexDirectory
$type = $window.GetType()
$toggle = $type.GetMethod('SetQuotaVisible',[Reflection.BindingFlags]'Instance,NonPublic')
$initialTop = $window.Top
$initialHeight = $window.Height
$baseline = $initialTop + $initialHeight
[void]$toggle.Invoke($window,@($false))
Assert ($window.Width -eq 204 -and [Math]::Abs($window.Top + $window.Height - $baseline) -lt 1) 'hiding the quota card keeps the pet baseline stable'
$preferences = Get-Content (Join-Path $uiData 'settings.json') -Raw | ConvertFrom-Json
Assert ($preferences.showQuota -eq $false) 'hidden state is persisted'
$restored = New-Object CodexGirl.QuotaPetWindow $sheet,$uiData,$testCodexDirectory
Assert ($restored.Width -eq 204) 'reopening remembers the hidden card'
[void]$toggle.Invoke($window,@($true))
Assert ($window.Width -eq 470) 'quota card can be restored'
$window.Render((Join-Path $testRoot 'quota-visible.png'))
[void]$toggle.Invoke($window,@($false))
$window.Render((Join-Path $testRoot 'quota-hidden.png'))
Write-Host "All import and card visibility checks passed. Artifacts: $testRoot"
