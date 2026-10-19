$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase, System.Web.Extensions
[void][Reflection.Assembly]::LoadFrom((Join-Path $root 'runtime\CodexGirlQuota.exe'))
function Assert($condition, $name) { if (-not $condition) { throw "FAIL: $name" }; Write-Host "PASS: $name" }
Add-Type -ReferencedAssemblies @([Windows.Media.ImageSource].Assembly.Location,[Windows.Threading.Dispatcher].Assembly.Location) -TypeDefinition @'
using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;
public static class ArtworkPixels {
    public static double[] Inspect(BitmapSource image) {
        var source = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
        int w = source.PixelWidth, h = source.PixelHeight, left=w, top=h, right=-1, bottom=-1, green=0;
        byte[] p=new byte[w*h*4]; source.CopyPixels(p,w*4,0);
        for(int y=0;y<h;y++) for(int x=0;x<w;x++) {
            int i=(y*w+x)*4; if(p[i+3]<64) continue;
            left=Math.Min(left,x); top=Math.Min(top,y); right=Math.Max(right,x); bottom=Math.Max(bottom,y);
            if(p[i+1]>Math.Max(p[i],p[i+2])+20) green++;
        }
        double scale=w/192.0;
        return new double[]{left/scale,top/scale,(right+1)/scale,(bottom+1)/scale,green};
    }
}
'@
$testDir = Join-Path $root 'dist\clarity-v1.3.4'
[void][IO.Directory]::CreateDirectory($testDir)
$sheet = Join-Path $root 'assets\spritesheet.png'
$window = New-Object CodexGirl.QuotaPetWindow $sheet,$testDir,$testDir
$flags = [Reflection.BindingFlags]'Instance,NonPublic'; $type=$window.GetType()
$actions=$type.GetField('actions',$flags).GetValue($window)
$native = New-Object Windows.Media.Imaging.BitmapImage ([Uri]$sheet)
$checked=0
try {
    for($row=0;$row -lt 9;$row++) {
        for($column=0;$column -lt $actions[$row].Count;$column++) {
            $frame=$actions[$row][$column]
            Assert ($frame.PixelWidth -eq 384 -and $frame.PixelHeight -eq 416 -and $frame.Width -eq 192 -and $frame.Height -eq 208 -and $frame.IsFrozen) "base $row/$column keeps 2x detail without enlarging the character"
            $old = New-Object Windows.Media.Imaging.CroppedBitmap $native,([Windows.Int32Rect]::new($column*192,$row*208,192,208))
            $oldBox=[ArtworkPixels]::Inspect($old); $newBox=[ArtworkPixels]::Inspect($frame)
            if($newBox[4] -gt 0) {
                $encoder=New-Object Windows.Media.Imaging.PngBitmapEncoder; $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($frame))
                $stream=[IO.File]::Create((Join-Path $testDir 'green-debug.png')); try {$encoder.Save($stream)} finally {$stream.Dispose()}
            }
            Assert ($newBox[4] -eq 0) "base $row/$column has no green backing fringe ($($newBox[4]) pixels)"
            $delta=0; for($i=0;$i -lt 4;$i++) { $delta=[Math]::Max($delta,[Math]::Abs($oldBox[$i]-$newBox[$i])) }
            Assert ($delta -le 2) "base $row/$column preserves the original pose registration ($delta logical pixels)"
            $checked++
        }
    }
    function Save-Png($source,$name) {
        $encoder=New-Object Windows.Media.Imaging.PngBitmapEncoder
        $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($source))
        $stream=[IO.File]::Create((Join-Path $testDir $name)); try { $encoder.Save($stream) } finally { $stream.Dispose() }
    }
    $oldIdle = New-Object Windows.Media.Imaging.CroppedBitmap $native,([Windows.Int32Rect]::new(0,0,192,208))
    Save-Png ([CodexGirl.DesktopArtwork]::Render($oldIdle,[Windows.Rect]::new(0,0,192,208))) 'idle-before.png'
    Save-Png $actions[0][0] 'idle-after.png'
    $foods=$type.GetField('interactionActions',$flags).GetValue($window)
    Save-Png $foods['eat-cake'][0] 'cake-after.png'
    $sizes=@($type.GetField('pet',$flags).GetValue($window).Width,$type.GetField('pet',$flags).GetValue($window).Height)
    Assert ($sizes[0] -eq 192 -and $sizes[1] -eq 208) 'visible pet size is unchanged'
    [ordered]@{base_frames=$checked;frame_pixels=@(384,416);logical_size=@(192,208);native_sheet_sha256=(Get-FileHash -LiteralPath $sheet).Hash.ToLowerInvariant()} | ConvertTo-Json | Set-Content (Join-Path $testDir 'clarity-report.json') -Encoding UTF8
} finally { $window.Close() }
Write-Host "All clarity checks passed. Artifacts: $testDir"
