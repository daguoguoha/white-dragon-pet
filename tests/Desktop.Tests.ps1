$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase, System.Windows.Forms, System.Drawing
[void][Reflection.Assembly]::LoadFrom((Join-Path $root 'runtime\CodexGirlQuota.exe'))
function Assert($condition, $name) { if (-not $condition) { throw "FAIL: $name" }; Write-Host "PASS: $name" }
$now = [DateTime]::UtcNow
$brain = New-Object CodexGirl.PetBehavior $now
Assert ($brain.Feed('fish',$now) -and $brain.Mood -eq 70) 'first food improves mood'
Assert ($brain.Feed('fish',$now) -and -not $brain.Feed('fish',$now) -and $brain.Mood -eq 79) 'third repeated food is disliked'
Assert ($brain.Feed('cake',$now) -and $brain.Mood -eq 89) 'changing food resets repetition'
1..20 | ForEach-Object { $brain.Praise($now) }
Assert ($brain.Mood -eq 100) 'mood is capped at 100'
1..100 | ForEach-Object { $brain.Tease($now) }
Assert ($brain.Mood -eq 0) 'mood cannot become negative'
Assert ($brain.Stroke($now) -and -not $brain.Stroke($now.AddSeconds(1)) -and $brain.Mood -eq 5) 'stroking has a six second cooldown'
$brain.Tick($now.AddSeconds(91),$false)
Assert ($brain.Sleeping -and $brain.Expression -eq 'sleep') 'inactivity enters sleep'
$brain.Poke($now.AddSeconds(92))
Assert (-not $brain.Sleeping -and $brain.Expression -eq 'surprise') 'interaction wakes the pet'
$brain.Tick($now.AddSeconds(96),$false)
Assert ($brain.Expression -eq 'idle') 'temporary reactions return to idle'
$brain.Tick($now.AddSeconds(200),$true)
Assert (-not $brain.Sleeping) 'modal interactions pause sleep'

$testDir = Join-Path $root ('dist\desktop-tests-' + [Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($testDir)
$config = New-Object CodexGirl.ChatConfiguration
$config.Endpoint = 'http://127.0.0.1:8123/v1'; $config.Model = 'local-test'; $config.Key = 'fixture-key-never-a-real-secret'
$config.Save($testDir)
$saved = [IO.File]::ReadAllText((Join-Path $testDir 'chat.json'))
$restored = [CodexGirl.ChatConfiguration]::Load($testDir)
Assert (-not $saved.Contains($config.Key) -and $restored.Key -eq $config.Key) 'API key round trips through DPAPI without plaintext on disk'
foreach ($url in @('http://example.com/v1','https://user:password@example.com/v1','https://example.com/v1?x=1')) {
    $rejected = $false; try { [void][CodexGirl.ChatConfiguration]::ValidateEndpoint($url) } catch { $rejected = $true }
    Assert $rejected 'unsafe or ambiguous service address is rejected'
}
$window = New-Object CodexGirl.QuotaPetWindow (Join-Path $root 'assets\spritesheet.png'),$testDir,$testDir
$type = $window.GetType(); $flags = [Reflection.BindingFlags]'Instance,NonPublic'
function Invoke-Private($name,$arguments) { $type.GetMethod($name,$flags).Invoke($window,$arguments) }
function Read-Private($name) { $type.GetField($name,$flags).GetValue($window) }
[void](Invoke-Private 'SetMovement' @('follow'))
$other = New-Object CodexGirl.QuotaPetWindow (Join-Path $root 'assets\spritesheet.png'),$testDir,$testDir
Assert ($type.GetField('movementMode',$flags).GetValue($other) -eq 'follow') 'movement choice survives reopening'
[void](Invoke-Private 'SetMovement' @('still'))
[void](Invoke-Private 'Say' @('Feed / stroke / follow / quota',([string]'♡')))
Assert ((Read-Private 'floating').Count -eq 2 -and (Read-Private 'bubble').Visibility -eq 'Visible') 'interaction creates a visible bubble and particles'
$atlas = Read-Private 'actions'
Assert ($atlas.Count -eq 11 -and $atlas[1].Count -eq 8 -and $atlas[6].Count -eq 6) 'all existing animation states are loaded'
(Read-Private 'brain').React('game',$now,12)
[void](Invoke-Private 'AnimateDesktop' @())
Assert ((Read-Private 'renderedAction') -eq 'game') 'interaction selects the gameplay animation'
$window.Render((Join-Path $testDir 'interaction.png'))

# Exercise the real HWND, tray and motion timer without making a quota request.
$type.GetField('busy',$flags).SetValue($window,$true)
$window.Left = -10000; $window.Top = -10000; $window.Show()
Assert ($null -ne (Read-Private 'tray')) 'a recovery tray icon is created on launch'
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class DesktopTestNative {
    [DllImport("user32.dll", EntryPoint="GetWindowLongW")]
    public static extern int GetWindowLong(IntPtr hwnd, int index);
}
'@
$handle = (New-Object System.Windows.Interop.WindowInteropHelper $window).Handle
[void](Invoke-Private 'SetPassthrough' @($true))
Assert (([DesktopTestNative]::GetWindowLong($handle,-20) -band 0x20) -ne 0) 'click-through sets the actual native window flag'
[void](Invoke-Private 'SetPassthrough' @($false))
Assert (([DesktopTestNative]::GetWindowLong($handle,-20) -band 0x20) -eq 0) 'click-through removes the actual native window flag'
$window.Hide()
Assert (-not $window.IsVisible) 'pet can be hidden without quitting'
[void](Invoke-Private 'Recover' @())
Assert ($window.IsVisible -and $window.Left -ge [Windows.SystemParameters]::VirtualScreenLeft) 'tray recovery restores the window on screen'
[void](Invoke-Private 'SetMovement' @('wander'))
$before = $window.Left
$destination = New-Object Windows.Point ($before - 100),$window.Top
$type.GetField('target',$flags).SetValue($window,$destination)
(Read-Private 'brain').React('idle',[DateTime]::UtcNow,1)
[void](Invoke-Private 'MovePet' @([DateTime]::UtcNow,[double]0.1))
Assert ($window.Left -lt $before -and $before - $window.Left -le 11) 'walking advances smoothly at a bounded speed (including device-pixel rounding)'
$window.Close()
Assert (-not (Read-Private 'motion').IsEnabled) 'closing stops movement and disposes the tray'
$other.Close()
Write-Host "All desktop checks passed. Artifacts: $testDir"
