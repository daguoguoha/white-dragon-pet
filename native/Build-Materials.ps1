$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase
[void][Reflection.Assembly]::LoadFrom((Join-Path $root 'runtime\CodexGirlQuota.exe'))
$basePath = Join-Path $root 'assets\spritesheet.png'
$base = New-Object Windows.Media.Imaging.BitmapImage
$base.BeginInit(); $base.CacheOption = [Windows.Media.Imaging.BitmapCacheOption]::OnLoad; $base.UriSource = New-Object Uri $basePath; $base.EndInit(); $base.Freeze()
$format = [Windows.Media.PixelFormats]::Bgra32
$converted = New-Object Windows.Media.Imaging.FormatConvertedBitmap $base,$format,$null,0
$stride = 1536*4; $pixels = New-Object byte[] ($stride*2288); $converted.CopyPixels($pixels,$stride,0)
$bank = [CodexGirl.InteractionAnimations]::Load($basePath)
$out = Join-Path $root 'native\packages'
[void][IO.Directory]::CreateDirectory($out)
$mapping = @{
    3 = @{ state='stroke'; sequence=@(0,1,2,3) }
    5 = @{ state='angry'; sequence=@(0,1,2,3,0,1,2,3) }
    6 = @{ state='sleep'; sequence=@(0,1,2,3,2,1) }
    7 = @{ state='FOOD'; sequence=@(0,1,2,2,3,0) }
}
function Write-Cell($atlas,[int]$column,[int]$row,$frame) {
    $rgba = New-Object Windows.Media.Imaging.FormatConvertedBitmap $frame,$format,$null,0
    $cell = New-Object byte[] (192*208*4); $rgba.CopyPixels($cell,192*4,0)
    $atlas.WritePixels((New-Object Windows.Int32Rect ($column*192),($row*208),192,208),$cell,192*4,0)
}
$checks = [ordered]@{}
foreach($food in @('fish','cake','lollipop','dango','diamond')) {
    $atlas = New-Object Windows.Media.Imaging.WriteableBitmap 1536,2288,96,96,$format,$null
    $atlas.WritePixels((New-Object Windows.Int32Rect 0,0,1536,2288),$pixels,$stride,0)
    # V2 idle[6] is the pointer-neutral pose; preserve the original neutral appearance.
    $neutral = New-Object Windows.Media.Imaging.CroppedBitmap $base,(New-Object Windows.Int32Rect 0,0,192,208)
    Write-Cell $atlas 6 0 $neutral
    foreach($row in $mapping.Keys) {
        $rule=$mapping[$row]; $state=if($rule.state -eq 'FOOD'){'eat-'+$food}else{$rule.state}
        for($column=0;$column -lt $rule.sequence.Count;$column++){Write-Cell $atlas $column $row $bank[$state][$rule.sequence[$column]]}
    }
    $folder = Join-Path $out $food; [void][IO.Directory]::CreateDirectory($folder)
    $path=Join-Path $folder 'spritesheet.png'
    $encoder=New-Object Windows.Media.Imaging.PngBitmapEncoder;$encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($atlas))
    $stream=[IO.File]::Create($path);try{$encoder.Save($stream)}finally{$stream.Dispose()}
    $manifest=[ordered]@{id='codex-girl';displayName='Codex娘';description=('原生素材版：摸头、闭眼休息、工作时吃东西（'+$food+'），保留移动、跳跃、读书和十六向视线。');spriteVersionNumber=2;spritesheetPath='spritesheet.png'}
    [IO.File]::WriteAllText((Join-Path $folder 'pet.json'),($manifest|ConvertTo-Json),(New-Object Text.UTF8Encoding($false)))
    $checks[$food+'/spritesheet.png']=(Get-FileHash -LiteralPath $path).Hash.ToLowerInvariant()
}
[IO.File]::WriteAllText((Join-Path $out 'checksums.json'),($checks|ConvertTo-Json),(New-Object Text.UTF8Encoding($false)))
Write-Host "Built five native v2 material variants: $out"
