$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase, System.Web.Extensions
[void][Reflection.Assembly]::LoadFrom((Join-Path $root 'runtime\CodexGirlQuota.exe'))
function Assert($condition, $name) { if (-not $condition) { throw "FAIL: $name" }; Write-Host "PASS: $name" }
$testDir = Join-Path $root ('dist\smoothness-' + [Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($testDir)
$window = New-Object CodexGirl.QuotaPetWindow (Join-Path $root 'assets\spritesheet.png'),$testDir,$testDir
$type = $window.GetType(); $flags = [Reflection.BindingFlags]'Instance,NonPublic'
function Call($name,$arguments) { $type.GetMethod($name,$flags).Invoke($window,$arguments) }
function Read($name) { $type.GetField($name,$flags).GetValue($window) }
function Put($name,$value) { $type.GetField($name,$flags).SetValue($window,$value) }
try {
    Assert ((Read 'targetFrameRate') -eq 60) 'new and legacy settings default to 60 FPS'
    $brain = Read 'brain'; $image = Read 'pet'
    $brain.React('sleep',[DateTime]::UtcNow,100)
    [void](Call 'RenderPoseAt' @([double]0))
    $first = $image.Source
    [void](Call 'RenderPoseAt' @([double]0.479))
    Assert ([Object]::ReferenceEquals($image.Source,$first)) 'sleep pose keeps its authored hold time'
    $brain.React('eat-cake',[DateTime]::UtcNow,100)
    [void](Call 'RenderPoseAt' @([double]0.480))
    $cake = (Read 'interactionActions')['eat-cake']
    Assert ([Object]::ReferenceEquals($image.Source,$cake[0])) 'waking into feeding switches immediately without waiting for the old 480ms timer'
    [void](Call 'RenderPoseAt' @([double]0.699))
    Assert ([Object]::ReferenceEquals($image.Source,$cake[0])) '60 FPS rendering does not speed up eating'
    [void](Call 'RenderPoseAt' @([double]0.700))
    Assert ([Object]::ReferenceEquals($image.Source,$cake[1])) 'the next source frame appears on its exact deadline'
    [void](Call 'RenderPoseAt' @([double]1.149))
    Assert ([Object]::ReferenceEquals($image.Source,$cake[3])) 'a delayed callback catches up to elapsed time instead of slowing the animation'
    [void](Call 'RenderPoseAt' @([double]1000.480))
    Assert ([Object]::ReferenceEquals($image.Source,$cake[1])) 'long stalls skip complete cycles without replaying thousands of frames'
    Put 'busy' $true
    $window.Show()
    Assert (Read 'renderingAttached') 'showing subscribes to display rendering'
    [void](Call 'StopRendering' @())
    foreach ($rate in @(60,30)) {
        [void](Call 'SetFrameRate' @([int]$rate)); Put 'lastComposition' ([TimeSpan]::MinValue)
        $accepted = 0; $duplicatesIgnored = $true
        for ($i=0; $i -lt 120; $i++) {
            $before = Read 'lastRenderAt'
            $frame = [Activator]::CreateInstance([Windows.Media.RenderingEventArgs],[Reflection.BindingFlags]'Instance,Public,NonPublic',$null,@([TimeSpan]::FromTicks([long]($i * 10000000.0 / 120))),$null)
            [void](Call 'RenderDesktop' @($null,$frame))
            if ((Read 'lastRenderAt') -ne $before) { $accepted++ }
            $after = Read 'lastRenderAt'
            [void](Call 'RenderDesktop' @($null,$frame))
            if ((Read 'lastRenderAt') -ne $after) { $duplicatesIgnored = $false }
        }
        Assert ([Math]::Abs($accepted-$rate) -le 1) "$rate FPS pacing stays stable under a 120Hz compositor ($accepted updates)"
        Assert $duplicatesIgnored "$rate FPS ignores duplicate compositor timestamps"
    }
    [void](Call 'SetFrameRate' @([int]30)); [void](Call 'SaveDesktopPrefs' @())
    $other = New-Object CodexGirl.QuotaPetWindow (Join-Path $root 'assets\spritesheet.png'),$testDir,$testDir
    Assert ($type.GetField('targetFrameRate',$flags).GetValue($other) -eq 30) 'power saving choice survives reopening'
    $other.Close()
    [void](Call 'SetFrameRate' @([int]60))
    $window.Hide(); Assert (-not (Read 'renderingAttached')) 'hiding stops per-frame work'
    $window.Show(); Assert (Read 'renderingAttached') 'restoring resumes rendering'
    # Measure actual callbacks with the real WPF dispatcher and window, without model or quota requests.
    $script:sampleTimes = New-Object 'System.Collections.Generic.List[double]'
    $script:lastSample = -1
    $observer = [EventHandler]{
        $stamp = Read 'lastRenderAt'
        if ($stamp -ge 0 -and $stamp -ne $script:lastSample) { $script:sampleTimes.Add($stamp); $script:lastSample = $stamp }
    }
    [Windows.Media.CompositionTarget]::add_Rendering($observer)
    $pump = New-Object Windows.Threading.DispatcherFrame
    $timer = New-Object Windows.Threading.DispatcherTimer
    $timer.Interval = [TimeSpan]::FromSeconds(4)
    $timer.Add_Tick({ $pump.Continue = $false; $timer.Stop() })
    try { $timer.Start(); [Windows.Threading.Dispatcher]::PushFrame($pump) }
    finally { $timer.Stop(); [Windows.Media.CompositionTarget]::remove_Rendering($observer) }
    Assert ($script:sampleTimes.Count -gt 20) 'the visible pet continues rendering on the real dispatcher'
    $span = $script:sampleTimes[$script:sampleTimes.Count-1] - $script:sampleTimes[0]
    $fps = ($script:sampleTimes.Count-1) / $span
    $report = [ordered]@{ target_fps=60; measured_update_fps=[Math]::Round($fps,2); duration_seconds=[Math]::Round($span,2); updates=$script:sampleTimes.Count; source_frames_unchanged=$true }
    $report | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $testDir 'render-report.json') -Encoding UTF8
    Write-Host ($report | ConvertTo-Json -Compress)
} finally {
    $window.Close()
}
Assert (-not (Read 'renderingAttached')) 'closing removes the static rendering subscription'
Write-Host "All smoothness checks passed. Artifacts: $testDir"
