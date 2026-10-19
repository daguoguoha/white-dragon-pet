$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Web.Extensions
$root = Split-Path $PSScriptRoot -Parent
Add-Type -Path (Join-Path $root 'quota\QuotaCore.cs') -ReferencedAssemblies @('System.dll','System.Core.dll',[System.Web.Script.Serialization.JavaScriptSerializer].Assembly.Location)
function Assert($condition, $name) { if (-not $condition) { throw "FAIL: $name" }; Write-Host "PASS: $name" }
$now = [DateTime]::UtcNow
$sample = [CodexGirl.QuotaData]::Parse('{"rateLimits":{"primary":{"usedPercent":25,"windowDurationMins":300,"resetsAt":1800000000},"secondary":null}}',$now)
Assert ($sample.Buckets[0].Primary.Remaining -eq 75) 'used percent is converted to remaining'
Assert ($sample.Buckets[0].Primary.Minutes -eq 300) 'actual window duration is preserved'
Assert ($sample.Buckets[0].Primary.ResetUtc.Kind -eq 'Utc') 'Unix timestamps are UTC'
Assert ($null -eq $sample.Buckets[0].Secondary.Remaining) 'missing window is unknown, never zero'
$multiple = [CodexGirl.QuotaData]::Parse('{"rateLimits":{"primary":{"usedPercent":99}},"rateLimitsByLimitId":{"other":{"primary":{"usedPercent":40}},"codex":{"primary":{"usedPercent":10}}}}',$now)
Assert ($multiple.Buckets.Count -eq 2 -and $multiple.Buckets[0].Id -eq 'codex' -and $multiple.Buckets[0].Primary.Remaining -eq 90) 'multi-bucket data takes priority and selects Codex first'
$invalid = [CodexGirl.QuotaData]::Parse('{"rateLimits":{"primary":{"usedPercent":null},"secondary":{"usedPercent":"NaN"}}}',$now)
Assert ($null -eq $invalid.Buckets[0].Primary.Remaining -and $null -eq $invalid.Buckets[0].Secondary.Remaining) 'null and NaN remain unknown'
$clamped = [CodexGirl.QuotaData]::Parse('{"rateLimits":{"primary":{"usedPercent":150},"secondary":{"usedPercent":-50}}}',$now)
Assert ($clamped.Buckets[0].Primary.Remaining -eq 0 -and $clamped.Buckets[0].Secondary.Remaining -eq 100) 'percentages are clamped to a valid range'
$empty = [CodexGirl.QuotaData]::Parse('{}',$now)
Assert ($empty.Buckets.Count -eq 0) 'missing account data does not fabricate a quota'
$elapsed = [CodexGirl.QuotaData]::Parse('{"rateLimits":{"primary":{"usedPercent":100,"resetsAt":1}}}',$now)
Assert ($elapsed.Buckets[0].Primary.Remaining -eq 0 -and $elapsed.Buckets[0].Primary.ResetText($now).Length -gt 0) 'elapsed reset does not fabricate replenished quota'
Write-Host 'All quota semantics checks passed.'
