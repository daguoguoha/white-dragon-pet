param([Parameter(Mandatory=$true)][string]$Server)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
[void][Reflection.Assembly]::LoadFrom((Join-Path $root 'runtime\CodexGirlQuota.exe'))
function Assert($condition, $name) { if (-not $condition) { throw "FAIL: $name" }; Write-Host "PASS: $name" }
$config = New-Object CodexGirl.ChatConfiguration
$config.Endpoint = "$Server/ok/v1"; $config.Model = 'test-model'; $config.Key = 'fake-test-key'
$history = New-Object 'System.Collections.Generic.List[System.Collections.Generic.Dictionary[string,string]]'
$token = [Threading.CancellationToken]::None
$reply = [CodexGirl.CompanionNetwork]::Chat($config,$history,'hello',$token).GetAwaiter().GetResult()
Assert ($reply -eq 'hello from fixture' -and $history.Count -eq 2) 'compatible chat endpoint receives a user message and returns a reply'
1..21 | ForEach-Object { [void][CodexGirl.CompanionNetwork]::Chat($config,$history,"message $_",$token).GetAwaiter().GetResult() }
Assert ($history.Count -eq 40 -and $history[0]['role'] -eq 'user' -and $history[39]['role'] -eq 'assistant') 'chat history remains bounded to 40 alternating messages'
$before = $history.Count
foreach ($path in @('fail','bad','redirect','large')) {
    $config.Endpoint = "$Server/$path/v1"; $rejected = $false
    try { [void][CodexGirl.CompanionNetwork]::Chat($config,$history,'test',$token).GetAwaiter().GetResult() } catch { $rejected = $true }
    Assert ($rejected -and $history.Count -eq $before) "$path response is rejected without corrupting history"
}
$config.Endpoint = "$Server/slow/v1"
$cancel = New-Object Threading.CancellationTokenSource
$cancel.CancelAfter(150)
$timer = [Diagnostics.Stopwatch]::StartNew(); $cancelled = $false
try { [void][CodexGirl.CompanionNetwork]::Chat($config,$history,'test',$cancel.Token).GetAwaiter().GetResult() } catch { $cancelled = $true }
Assert ($cancelled -and $timer.Elapsed.TotalSeconds -lt 3) 'closing a chat can cancel its network request promptly'
$cancel.Dispose()
Write-Host 'All offline network checks passed.'
