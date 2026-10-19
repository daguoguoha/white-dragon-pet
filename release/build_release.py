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
payload = {'pet/pet.json': manifest, 'pet/spritesheet.png': sheet}
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
(OUT / 'SHA256SUMS.txt').write_text(f'{checksum}  {archive.name}\n', encoding='ascii')
(OUT / 'release-notes.md').write_text(f'''# Codex娘 {TAG}

Windows 一键安装包，新增 **Quota 额度悬浮版**。下载 **{archive.name}**，完整解压后双击 **Install.cmd**。

安装后自动启动额度宠物，之后可通过解压目录或安装目录中的 **Start-Quota.cmd** 打开，也可直接运行 **quota/CodexGirlQuota.exe**。

宠物旁会显示额度窗口的剩余百分比和重置倒计时，每分钟刷新一次。支持多个额度桶；网络失败会标记旧数据，缺失值显示暂不可用。可拖动、招手、调大小和切换置顶。

- 无需管理员权限、Python、Git；额度读取需要已登录的 Codex CLI 和网络，安装器本身可离线运行，支持 `CODEX_HOME`。
- Windows 程序使用系统的 .NET Framework 4.8，保留解压目录即可直接运行。
- 通过官方 App Server 的 `account/rateLimits/read` 读取 CLI 当前登录账户；不发送模型请求，不读取或复制令牌文件，不记录账户信息。
- 此悬浮版独立于原生宠物窗口；原生宠物没有实时文字插槽。它不跟随原生宠物的聊天状态，也不会自动隐藏原生宠物。若同时显示两只，可在原生宠物菜单中隐藏其中一只。
- 文件校验、安装后复核、升级前备份，重复安装相同版本不会重复备份。
- v2 透明精灵图，九种动画状态和十六个视线方向。
- 面向支持 v2 自定义宠物的 Windows 桌面端，本地安装不会自动同步到网页账户。

需要纯原生动画时，在 **设置 → 宠物 → 刷新** 中选择 **Codex娘** 并输入 `/pet`。额度版不会接管当前宠物选择。

`SHA256SUMS.txt` 提供安装包的 SHA-256 校验值。\n''', encoding='utf8')
print(json.dumps({'tag': TAG, 'archive': str(archive), 'bytes': archive.stat().st_size, 'sha256': checksum}, ensure_ascii=False))
