[CmdletBinding()]
param([ValidateSet('fish','cake','lollipop','dango','diamond')][string]$Food='fish',[string]$CodexDirectory)
$ErrorActionPreference='Stop'
function Regular([string]$Path,[bool]$Directory){
    if(Test-Path -LiteralPath $Path){$item=Get-Item -LiteralPath $Path -Force;if(($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -or ($item.PSIsContainer -ne $Directory)){throw "Expected a regular path: $Path"}}
}
if([string]::IsNullOrWhiteSpace($CodexDirectory)){$CodexDirectory=$env:CODEX_HOME}
if([string]::IsNullOrWhiteSpace($CodexDirectory)){$CodexDirectory=Join-Path ([Environment]::GetFolderPath('UserProfile')) '.codex'}
if(-not [IO.Path]::IsPathRooted($CodexDirectory)){throw 'CodexDirectory must be absolute'}
$codexPath=[IO.Path]::GetFullPath($CodexDirectory)
$packages=Join-Path $PSScriptRoot 'packages'
$source=Join-Path (Join-Path $packages $Food) 'spritesheet.png'
$checks=Get-Content -LiteralPath (Join-Path $packages 'checksums.json') -Raw | ConvertFrom-Json
$expected=$checks.($Food+'/spritesheet.png')
if((Get-FileHash -LiteralPath $source).Hash.ToLowerInvariant() -ne $expected){throw 'Native material checksum mismatch'}
Add-Type -AssemblyName PresentationCore,WindowsBase
$imageStream=[IO.File]::OpenRead($source)
try{$decoder=[Windows.Media.Imaging.BitmapDecoder]::Create($imageStream,[Windows.Media.Imaging.BitmapCreateOptions]::PreservePixelFormat,[Windows.Media.Imaging.BitmapCacheOption]::OnLoad);if($decoder.Frames[0].PixelWidth -ne 1536 -or $decoder.Frames[0].PixelHeight -ne 2288){throw 'Expected v2 native pet sheet'}}finally{$imageStream.Dispose()}
$pets=Join-Path $codexPath 'pets';$target=Join-Path $pets 'codex-girl'
foreach($directory in @($codexPath,$pets,$target)){Regular $directory $true}
$manifestPath=Join-Path $target 'pet.json';$imagePath=Join-Path $target 'spritesheet-native.png'
Regular $manifestPath $false;Regular $imagePath $false
$manifest=if(Test-Path -LiteralPath $manifestPath){Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8|ConvertFrom-Json}else{[pscustomobject]@{id='codex-girl';displayName='Codex娘';description='';spriteVersionNumber=2;spritesheetPath=''}}
if($manifest.spritesheetPath -eq 'spritesheet-native.png' -and (Test-Path -LiteralPath $imagePath) -and (Get-FileHash -LiteralPath $imagePath).Hash.ToLowerInvariant() -eq $expected){Write-Host 'These native materials are already installed.';return}
$backupRoot=Join-Path $codexPath 'pet-backups';Regular $backupRoot $true
$backup=Join-Path $backupRoot ('native-materials-'+[Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($backup)
$hadManifest=Test-Path -LiteralPath $manifestPath;$hadImage=Test-Path -LiteralPath $imagePath
if($hadManifest){Copy-Item -LiteralPath $manifestPath -Destination (Join-Path $backup 'pet.json')}
if($hadImage){Copy-Item -LiteralPath $imagePath -Destination (Join-Path $backup 'spritesheet-native.png')}
$manifest.spritesheetPath='spritesheet-native.png';$manifest.spriteVersionNumber=2
$manifest.description='原生素材版：摸头、闭眼休息、工作时吃东西；动作仍由 Codex 固定状态触发。'
[void][IO.Directory]::CreateDirectory($target)
$stage=Join-Path $target ('.native-materials-'+[Guid]::NewGuid().ToString('N'))
try{
    Copy-Item -LiteralPath $source -Destination ($stage+'.png')
    [IO.File]::WriteAllText(($stage+'.json'),($manifest|ConvertTo-Json -Depth 8),(New-Object Text.UTF8Encoding($false)))
    if($hadImage){[IO.File]::Replace(($stage+'.png'),$imagePath,[NullString]::Value)}else{[IO.File]::Move(($stage+'.png'),$imagePath)}
    if($hadManifest){[IO.File]::Replace(($stage+'.json'),$manifestPath,[NullString]::Value)}else{[IO.File]::Move(($stage+'.json'),$manifestPath)}
}catch{
    if($hadImage){Copy-Item -LiteralPath (Join-Path $backup 'spritesheet-native.png') -Destination $imagePath -Force}elseif(Test-Path -LiteralPath $imagePath){Remove-Item -LiteralPath $imagePath}
    # The manifest is published atomically and last; if publication fails the old manifest is intact.
    throw
}finally{foreach($path in @(($stage+'.png'),($stage+'.json'))){if(Test-Path -LiteralPath $path){Remove-Item -LiteralPath $path}}}
Write-Host "Installed native materials: $Food"
Write-Host "Backup: $backup"
Write-Host '在 Codex 设置 > 宠物中刷新并重新选择 Codex娘。五种食物版本只替换同一个条目的素材。'
