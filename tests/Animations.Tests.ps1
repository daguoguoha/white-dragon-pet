$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase, System.Web.Extensions
[void][Reflection.Assembly]::LoadFrom((Join-Path $root 'runtime\CodexGirlQuota.exe'))
function Assert($condition, $name) { if (-not $condition) { throw "FAIL: $name" }; Write-Host "PASS: $name" }
$version = (Get-Content (Join-Path $root 'release\version.txt') -Raw).Trim()
$testDir = Join-Path $root ("dist\animation-check-v" + $version)
[void][IO.Directory]::CreateDirectory($testDir)
$sheet = Join-Path $root 'assets\spritesheet.png'
$bank = [CodexGirl.InteractionAnimations]::Load($sheet)
$allFrames = @{}
foreach ($state in [CodexGirl.InteractionAnimations]::States) {
    $frames = $bank[$state]; $hashes = @(); $encoded = @()
    for ($index=0; $index -lt $frames.Count; $index++) {
        $frame = $frames[$index]
        Assert ($frame.PixelWidth -eq 192 -and $frame.PixelHeight -eq 208) "$state frame $index matches the render cell"
        $encoder = New-Object Windows.Media.Imaging.PngBitmapEncoder
        $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($frame))
        $path = Join-Path $testDir ("$state-$index.png")
        $stream = [IO.File]::Create($path); try { $encoder.Save($stream) } finally { $stream.Dispose() }
        $hashes += (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
        $encoded += 'data:image/png;base64,' + [Convert]::ToBase64String([IO.File]::ReadAllBytes($path))
        $pixels = New-Object byte[] (192*208*4); $frame.CopyPixels($pixels,192*4,0)
        Assert ($pixels[3] -eq 0 -and $pixels[(208*192-1)*4+3] -eq 0) "$state frame $index keeps transparent corners"
    }
    Assert (@($hashes | Select-Object -Unique).Count -eq 4) "$state has four different animation frames"
    $allFrames[$state] = $encoded
}
$now = [DateTime]::UtcNow
$brain = New-Object CodexGirl.PetBehavior $now
[void]$brain.Stroke($now); Assert ($brain.Expression -eq 'stroke') 'stroking selects its dedicated animation'
[void]$brain.Feed('cake',$now); Assert ($brain.Expression -eq 'eat-cake') 'feeding cake selects its own fork and cake animation'
$brain.Praise($now); Assert ($brain.Expression -eq 'happy') 'praise selects the happy expression'
$brain.Tease($now); Assert ($brain.Expression -eq 'angry') 'teasing selects a pout'
$brain.Tick($now.AddSeconds(91),$false); Assert ($brain.Sleeping -and $brain.Expression -eq 'sleep') 'inactivity selects closed-eye sleeping animation'

$window = New-Object CodexGirl.QuotaPetWindow $sheet,$testDir,$testDir
$flags = [Reflection.BindingFlags]'Instance,NonPublic'; $type = $window.GetType()
$petBrain = $type.GetField('brain',$flags).GetValue($window)
$petImage = $type.GetField('pet',$flags).GetValue($window)
$animate = $type.GetMethod('AnimateDesktop',$flags)
$toggle = $type.GetMethod('SetQuotaVisible',$flags)
[void]$toggle.Invoke($window,@($false))
$foodMenu = @($window.ContextMenu.Items[0].Items | Where-Object { $_.Header -eq '喂食' })[0]
$foodStates = [CodexGirl.InteractionAnimations]::FoodStates
$activeBank = $type.GetField('interactionActions',$flags).GetValue($window)
for ($food=0; $food -lt 5; $food++) {
    $item = $foodMenu.Items[$food]
    $click = New-Object Windows.RoutedEventArgs ([Windows.Controls.MenuItem]::ClickEvent),$item
    $item.RaiseEvent($click)
    Assert ($petBrain.Expression -eq $foodStates[$food]) ("menu food " + $food + ' selects its matching food animation')
    [void]$animate.Invoke($window,@())
    Assert ([Object]::ReferenceEquals($petImage.Source,$activeBank[$foodStates[$food]][0])) ("menu food " + $food + ' displays the matching prop rather than a generic treat')
}
$firstFrames = @($foodStates | ForEach-Object { $allFrames[$_][0] })
Assert (@($firstFrames | Select-Object -Unique).Count -eq 5) 'all five foods use visibly distinct image sequences'
$repeatBrain = New-Object CodexGirl.PetBehavior ([DateTime]::UtcNow)
Assert ($repeatBrain.Feed('小鱼干',$now) -and $repeatBrain.Feed('小鱼干',$now) -and -not $repeatBrain.Feed('小鱼干',$now) -and $repeatBrain.Expression -eq 'bored') 'third identical food still produces the bored reaction'
Assert ($repeatBrain.Feed('蛋糕',$now) -and $repeatBrain.Expression -eq 'eat-cake') 'changing food resets boredom and selects the new prop'
foreach ($state in [CodexGirl.InteractionAnimations]::States) {
    $petBrain.React($state,[DateTime]::UtcNow,10)
    [void]$animate.Invoke($window,@())
    $first = $petImage.Source
    [void]$animate.Invoke($window,@())
    Assert (-not [Object]::ReferenceEquals($first,$petImage.Source)) "$state advances actual displayed frames"
    # A fixed delay normally handles crossfade; finish it explicitly for deterministic preview rendering.
    $petImage.BeginAnimation([Windows.UIElement]::OpacityProperty,$null); $petImage.Opacity = 1
    $previous = $type.GetField('previousPose',$flags).GetValue($window)
    $previous.BeginAnimation([Windows.UIElement]::OpacityProperty,$null); $previous.Opacity = 0
    $window.Render((Join-Path $testDir ("window-$state.png")))
}
$window.Close()

$json = ConvertTo-Json -InputObject $allFrames -Compress -Depth 5
$html = @'
<!doctype html><meta charset="utf-8"><title>Codex娘 · 动画预览</title>
<style>body{font:16px system-ui;background:#f6f1fc;color:#51435e;margin:32px}h1{font-size:23px}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(220px,240px));gap:18px}.card{background:#fff;border-radius:18px;padding:18px;text-align:center}.card img{display:block;width:192px;height:208px;margin:auto}p{color:#877691}button{padding:8px 16px;margin-bottom:20px}</style>
<h1>Codex娘 · 五种食物和互动动画</h1><p>程序实际使用的逐帧素材。小鱼干、蛋糕、棒棒糖、团子、钻石各有四帧动作；钻石以吸收光芒表现。</p><button id="toggle">暂停 / 播放</button><div class="grid" id="grid"></div>
<script>const frames=FRAMES_DATA;const labels={'eat-fish':'小鱼干 · 双手咬嚼','eat-cake':'蛋糕 · 叉子送入口','eat-lollipop':'棒棒糖 · 含糖回味','eat-dango':'团子 · 三色串签','eat-diamond':'钻石 · 吸收光芒',stroke:'摸头 · 闭眼歪头',happy:'开心 · 笑眼',angry:'生气 · 抱臂鼓脸',bored:'嫌腻 · 托脸侧看',sleep:'休眠 · 闭眼呼吸'};let paused=false;document.getElementById('toggle').onclick=()=>paused=!paused;for(const state of Object.keys(labels)){if(!frames[state])continue;const card=document.createElement('div');card.className='card';const img=document.createElement('img');img.src=frames[state][0];const name=document.createElement('div');name.textContent=labels[state];card.append(img,name);document.getElementById('grid').append(card);let index=0;setInterval(()=>{if(!paused)img.src=frames[state][(++index)%4]},state==='sleep'?480:state.startsWith('eat-')?220:200)}</script>
'@
$html = $html.Replace('FRAMES_DATA',$json)
$preview = Join-Path $root 'previews\interactions.html'
[IO.File]::WriteAllText($preview,$html,(New-Object Text.UTF8Encoding($false)))
$foodFrames = @{}; foreach($state in $foodStates){$foodFrames[$state]=$allFrames[$state]}
$foodJson = ConvertTo-Json -InputObject $foodFrames -Compress -Depth 5
$foodPreview = $html.Replace($json,$foodJson).Replace('五种食物和互动动画','五种食物动画')
[IO.File]::WriteAllText((Join-Path $root 'previews\foods.html'),$foodPreview,(New-Object Text.UTF8Encoding($false)))
Write-Host "All interaction animation checks passed. Preview: $preview"
