[CmdletBinding()]
param([string]$PreviewPath, [string]$FixturePath, [switch]$ReadQuota, [switch]$PreviewFailure)
$ErrorActionPreference = 'Stop'
try {
    Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase, System.Xaml, System.Web.Extensions
    $references = @('System.dll', 'System.Core.dll', [System.Web.Script.Serialization.JavaScriptSerializer].Assembly.Location,
        [System.Windows.Window].Assembly.Location,
        [System.Windows.Media.ImageSource].Assembly.Location,
        [System.Windows.Threading.Dispatcher].Assembly.Location,
        [System.Xaml.XamlReader].Assembly.Location)
    Add-Type -Path @((Join-Path $PSScriptRoot 'QuotaCore.cs'), (Join-Path $PSScriptRoot 'QuotaPet.cs')) -ReferencedAssemblies $references
    $root = Split-Path $PSScriptRoot -Parent
    $sheet = Join-Path $root 'spritesheet.png'
    if (-not (Test-Path -LiteralPath $sheet)) { $sheet = Join-Path $root 'pet\spritesheet.png' }
    if (-not (Test-Path -LiteralPath $sheet)) { $sheet = Join-Path $root 'assets\spritesheet.png' }
    $dataDir = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'CodexGirlQuota'
    $codexDir = $env:CODEX_HOME
    if ([string]::IsNullOrWhiteSpace($codexDir) -and (Test-Path -LiteralPath (Join-Path $root 'pet.json'))) {
        $codexDir = Split-Path (Split-Path $root -Parent) -Parent
    }
    if ([string]::IsNullOrWhiteSpace($codexDir) -and -not $PreviewPath) {
        $profileDir = [Environment]::GetFolderPath('UserProfile')
        if ([string]::IsNullOrWhiteSpace($profileDir)) { $profileDir = $env:USERPROFILE }
        if ([string]::IsNullOrWhiteSpace($profileDir)) { throw 'Cannot locate your Windows profile.' }
        $codexDir = Join-Path $profileDir '.codex'
    }
    if ($PreviewPath) {
        if (-not $FixturePath) { throw 'Preview requires an explicit fixture JSON file.' }
        [CodexGirl.QuotaPet]::Preview($sheet, $PreviewPath, (Get-Content -LiteralPath $FixturePath -Raw -Encoding UTF8), $PreviewFailure.IsPresent)
    } elseif ($ReadQuota) {
        $client = New-Object CodexGirl.QuotaClient ([CodexGirl.QuotaClient]::FindCodex()), $root, $codexDir
        try { $client.Read() | ConvertTo-Json -Depth 8 } finally { $client.Dispose() }
    } else { [CodexGirl.QuotaPet]::Run($sheet, $dataDir, $codexDir) }
} catch {
    if ($PreviewPath -or $ReadQuota) { throw }
    Add-Type -AssemblyName PresentationFramework
    [void][System.Windows.MessageBox]::Show(('启动失败，请确认安装包已完整解压。' + [Environment]::NewLine + $_.Exception.Message), 'Codex娘 Quota')
    exit 1
}
