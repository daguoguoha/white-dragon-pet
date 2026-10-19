$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase, System.Xaml, System.Web.Extensions, System.Windows.Forms, System.Drawing, System.Security
$references = @('System.dll', 'System.Core.dll', 'System.Security.dll', 'System.Windows.Forms.dll', 'System.Drawing.dll', [System.Web.Script.Serialization.JavaScriptSerializer].Assembly.Location,
    [System.Windows.Window].Assembly.Location, [System.Windows.Media.ImageSource].Assembly.Location,
    [System.Windows.Threading.Dispatcher].Assembly.Location, [System.Xaml.XamlReader].Assembly.Location)
$outDir = Join-Path $root 'runtime'
[void][IO.Directory]::CreateDirectory($outDir)
$exe = Join-Path $outDir 'CodexGirlQuota.exe'
if (Test-Path -LiteralPath $exe) { Remove-Item -LiteralPath $exe }
$sources = @('QuotaCore.cs','QuotaPet.cs','Program.cs', 'AssemblyInfo.cs','PetInstaller.cs','PetImportWindow.cs', 'PetBehavior.cs', 'DesktopPet.cs', 'CompanionServices.cs', 'CompanionDialogs.cs', 'InteractionAnimations.cs', 'DesktopArtwork.cs') | ForEach-Object { Join-Path (Join-Path $root 'quota') $_ }
$compiler = New-Object System.CodeDom.Compiler.CompilerParameters
$compiler.GenerateExecutable = $true
$compiler.GenerateInMemory = $false
$compiler.OutputAssembly = $exe
$manifest = Join-Path $root 'quota\app.manifest'
$compiler.CompilerOptions = '/target:winexe /optimize+ /win32manifest:"' + $manifest + '"'
foreach ($reference in $references) { [void]$compiler.ReferencedAssemblies.Add($reference) }
[void]$compiler.EmbeddedResources.Add((Join-Path $root 'assets\spritesheet.png'))
[void]$compiler.EmbeddedResources.Add((Join-Path $root 'THIRD_PARTY_NOTICES.md'))
[void]$compiler.EmbeddedResources.Add((Join-Path $root 'assets\interactions-v2.png'))
$foodImages = @('fish','cake','lollipop','dango','diamond') | ForEach-Object { Join-Path $root ('assets\foods\' + $_ + '-v1.png') }
foreach ($image in $foodImages) { [void]$compiler.EmbeddedResources.Add($image) }
$desktopFiles = @(Get-ChildItem -LiteralPath (Join-Path $root 'assets\desktop') -File | Where-Object { $_.Extension -in @('.png','.json') } | Sort-Object Name)
foreach ($file in $desktopFiles) { [void]$compiler.EmbeddedResources.Add($file.FullName) }
Add-Type -Path $sources -CompilerParameters $compiler
$sourceHashes = [ordered]@{}
foreach ($source in $sources) { $sourceHashes['quota/' + [IO.Path]::GetFileName($source)] = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() }
$sourceHashes['assets/spritesheet.png'] = (Get-FileHash -LiteralPath (Join-Path $root 'assets\spritesheet.png') -Algorithm SHA256).Hash.ToLowerInvariant()
$sourceHashes['THIRD_PARTY_NOTICES.md'] = (Get-FileHash -LiteralPath (Join-Path $root 'THIRD_PARTY_NOTICES.md') -Algorithm SHA256).Hash.ToLowerInvariant()
$sourceHashes['assets/interactions-v2.png'] = (Get-FileHash -LiteralPath (Join-Path $root 'assets\interactions-v2.png') -Algorithm SHA256).Hash.ToLowerInvariant()
foreach ($image in $foodImages) { $sourceHashes['assets/foods/' + [IO.Path]::GetFileName($image)] = (Get-FileHash -LiteralPath $image -Algorithm SHA256).Hash.ToLowerInvariant() }
$sourceHashes['quota/app.manifest'] = (Get-FileHash -LiteralPath $manifest -Algorithm SHA256).Hash.ToLowerInvariant()
foreach ($file in $desktopFiles) { $sourceHashes['assets/desktop/' + $file.Name] = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
$buildInfo = [ordered]@{ executable_sha256 = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash.ToLowerInvariant(); sources = $sourceHashes }
[IO.File]::WriteAllText((Join-Path $outDir 'build-info.json'), ($buildInfo | ConvertTo-Json -Depth 4), (New-Object Text.UTF8Encoding($false)))
Write-Host "Built $exe"
