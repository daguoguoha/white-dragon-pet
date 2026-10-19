$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase, System.Xaml, System.Web.Extensions
$references = @('System.dll', 'System.Core.dll', [System.Web.Script.Serialization.JavaScriptSerializer].Assembly.Location,
    [System.Windows.Window].Assembly.Location, [System.Windows.Media.ImageSource].Assembly.Location,
    [System.Windows.Threading.Dispatcher].Assembly.Location, [System.Xaml.XamlReader].Assembly.Location)
$outDir = Join-Path $root 'runtime'
[void][IO.Directory]::CreateDirectory($outDir)
$exe = Join-Path $outDir 'CodexGirlQuota.exe'
if (Test-Path -LiteralPath $exe) { Remove-Item -LiteralPath $exe }
$sources = @('QuotaCore.cs','QuotaPet.cs','Program.cs') | ForEach-Object { Join-Path (Join-Path $root 'quota') $_ }
Add-Type -Path $sources -ReferencedAssemblies $references -OutputAssembly $exe -OutputType WindowsApplication
$sourceHashes = [ordered]@{}
foreach ($source in $sources) { $sourceHashes['quota/' + [IO.Path]::GetFileName($source)] = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() }
$buildInfo = [ordered]@{ executable_sha256 = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash.ToLowerInvariant(); sources = $sourceHashes }
[IO.File]::WriteAllText((Join-Path $outDir 'build-info.json'), ($buildInfo | ConvertTo-Json -Depth 4), (New-Object Text.UTF8Encoding($false)))
Write-Host "Built $exe"
