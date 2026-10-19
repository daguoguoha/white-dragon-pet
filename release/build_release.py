"""Build a self-contained Windows pet installer with only the Python standard library."""
import hashlib
import json
from pathlib import Path
import re
import struct
import zipfile

ROOT = Path(__file__).resolve().parents[1]
VERSION = (ROOT / 'release/version.txt').read_text().strip()
if not re.fullmatch(r'\d+\.\d+\.\d+', VERSION):
    raise ValueError('Expected a numeric semver version')
TAG = 'v' + VERSION
OUT = ROOT / 'dist'
OUT.mkdir(exist_ok=True)
sheet = (ROOT / 'assets/spritesheet.png').read_bytes()
if sheet[:8] != b'\x89PNG\r\n\x1a\n' or struct.unpack('>II', sheet[16:24]) != (1536, 2288):
    raise ValueError('Expected a 1536 x 2288 transparent PNG')
manifest = (json.dumps({
    'id': 'codex-girl',
    'displayName': 'Codex娘',
    'description': '白发紫瞳的 Q 版白龙少女，含九种动画和十六向视线。',
    'spriteVersionNumber': 2,
    'spritesheetPath': 'spritesheet.png',
}, ensure_ascii=False, indent=2) + '\n').encode('utf8')
payload = {'pet/pet.json': manifest, 'pet/spritesheet.png': sheet, 'THIRD_PARTY_NOTICES.md': (ROOT / 'THIRD_PARTY_NOTICES.md').read_bytes()}
for name in ('Install.cmd', 'Install.ps1', 'Start-Quota.cmd', 'README.md'):
    source = ROOT / 'install' / name
    text = source.read_text(encoding='utf-8-sig')
    # Windows PowerShell 5.1 needs a BOM for Chinese text; CMD needs CRLF.
    data = text.replace('\r\n', '\n').replace('\n', '\r\n').encode('utf8')
    payload[name] = (b'\xef\xbb\xbf' + data) if name.endswith('.ps1') else data
for name in ('CodexGirlQuota.exe', 'CodexGirlQuota.exe.config'):
    payload['quota/' + name] = (ROOT / 'runtime' / name).read_bytes()
if not payload['quota/CodexGirlQuota.exe'].startswith(b'MZ'):
    raise ValueError('Build the Windows application with release/Build-Windows.ps1 first')
build_info = json.loads((ROOT / 'runtime/build-info.json').read_text(encoding='utf-8-sig'))
if hashlib.sha256(payload['quota/CodexGirlQuota.exe']).hexdigest() != build_info['executable_sha256']:
    raise ValueError('Runtime executable does not match build-info.json')
for source, expected in build_info['sources'].items():
    if hashlib.sha256((ROOT / source).read_bytes()).hexdigest() != expected:
        raise ValueError(f'Rebuild the Windows runtime after changing {source}')
checks = {name: hashlib.sha256(data).hexdigest() for name, data in payload.items() if name not in ('Install.cmd', 'Install.ps1', 'README.md')}
payload['package-checksums.json'] = (json.dumps(checks, indent=2) + '\n').encode()
archive = OUT / f'Codex-Girl-{TAG}-Windows.zip'
with zipfile.ZipFile(archive, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=9) as z:
    for name, data in sorted(payload.items()):
        info = zipfile.ZipInfo(f'Codex-Girl-{TAG}/{name}', date_time=(2026, 10, 19, 0, 0, 0))
        info.create_system = 3  # Identical ZIP headers on Windows and Linux.
        info.compress_type = zipfile.ZIP_DEFLATED
        info.external_attr = 0o100644 << 16
        z.writestr(info, data)
with zipfile.ZipFile(archive) as z:
    assert z.testzip() is None
checksum = hashlib.sha256(archive.read_bytes()).hexdigest()
standalone = OUT / f'Codex-Girl-{TAG}.exe'
standalone.write_bytes(payload['quota/CodexGirlQuota.exe'])
exe_checksum = hashlib.sha256(standalone.read_bytes()).hexdigest()
(OUT / 'SHA256SUMS.txt').write_text(f'{checksum}  {archive.name}\n{exe_checksum}  {standalone.name}\n', encoding='ascii')
(OUT / 'release-notes.md').write_text(f'''# Codex娘 {TAG}

提升 **桌宠清晰度**：直接使用之前生成的九组高清动作原画，避免原生 192×208 中间缩图；基础、互动和食物动画缓存统一为 384×416，显示尺寸与动作对齐不变。保留透明背景，清理原始绿底轮廓；EXE 内嵌 Per-Monitor V2 DPI 声明以适配高分屏缩放。原生 Pet 精灵图保持不变。

优化 **桌宠流畅度**：移动、呼吸和粒子改为跟随 WPF 绘制，默认目标 60 FPS，右键「动画流畅度」可切换 30 FPS 省电模式。互动在下一次绘制切换；动画按经过时间追赶，隐藏到托盘后暂停逐帧工作。实际帧率受设备和负载影响，保留每动作 4–8 帧原画及原有播放节奏。

五种食物各自的专属动画继续保留，每种四帧，共 20 帧：小鱼干双手咬嚼、蛋糕用叉子吃、棒棒糖含糖回味、三色团子串签、钻石抱着吸收光芒。原生 v2 精灵图不变，独立 EXE 内置补充图集。

独立桌宠互动参考 [GenhuiC-sudo/11](https://github.com/GenhuiC-sudo/11) 的代码逻辑，保留其 MIT 许可声明。

直接下载并打开 **{standalone.name}** 即可，素材已内置，Windows 需要 .NET Framework 4.8。

- 按住头部小幅来回轻抚；拖动身体移动，点击身体戳一戳。
- 右键 **互动** 可喂食、夸夸、逗逗、跳跃、玩游戏、读书；连续三次相同食物会嫌腻。
- 右键 **活动模式** 选择原地陪伴、自由散步、跟随鼠标。闲置 90 秒会打瞌睡，互动会唤醒；贴边有托腮或倒挂效果。
- 带对话气泡、爱心 / 星星 / z 粒子、心情值和动作淡入；移动、呼吸和粒子默认目标 60 FPS，支持 30 FPS 省电模式，六组互动使用专属四帧序列；走路沿用原有左右移动帧。呼吸、行走起伏和拖拽抖动参考大肥鱼的动画公式。
- 托盘支持隐藏、恢复位置、解除鼠标穿透和退出。开机启动需手动开启，默认关闭。
- 右键 **和她聊天 / AI 设置** 配置兼容 OpenAI 的接口根地址、模型和密钥。发送聊天才请求模型，可能产生所配服务的 API 费用；最近 40 条消息仅保存在窗口内存中。密钥用 Windows 当前账户加密保存。
- 天气需手动输入城市并查询，使用 wttr.in，不读取定位。支持 CPU / 内存状态和占用提醒；GPU 温度暂不可用。
- 保留每分钟刷新的 Codex CLI 账户额度、卡片隐藏，以及 **导入到 Codex Pet**。双击宠物切换额度卡片，喂食在右键菜单中。

这是独立 EXE 的功能。导入原生 Codex Pet 的仍是动画素材；新交互不进入原生窗口。角色保留 Codex娘的外观，未直接使用参考项目立绘。五种喂食会切换各自的道具和动作，连续三次同款仍会嫌腻；托盘负责窗口显示和隐藏，无角色专属动作。

额度通过官方 App Server 的 `account/rateLimits/read` 读取；不保存令牌，不发起模型请求。可选的 AI 聊天使用你单独配置的接口和密钥，与 Codex 登录及额度读取分开。

ZIP **{archive.name}** 完整解压后也可运行 **Install.cmd**。升级前退出旧程序。卸载前若曾启用开机启动，请先在菜单关闭。

`SHA256SUMS.txt` 提供 EXE 和 ZIP 的校验值。
''', encoding='utf8')
print(json.dumps({'tag': TAG, 'archive': str(archive), 'bytes': archive.stat().st_size, 'sha256': checksum, 'standalone': str(standalone), 'exe_sha256': exe_checksum}, ensure_ascii=False))
