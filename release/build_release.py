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
checks = {Path(name).name: hashlib.sha256(data).hexdigest() for name, data in payload.items()}
payload['package-checksums.json'] = (json.dumps(checks, indent=2) + '\n').encode()
for name in ('Install.cmd', 'Install.ps1', 'README.md'):
    text = (ROOT / 'install' / name).read_text(encoding='utf-8-sig')
    # Windows PowerShell 5.1 needs a BOM for Chinese text; CMD needs CRLF.
    data = text.replace('\r\n', '\n').replace('\n', '\r\n').encode('utf8')
    payload[name] = (b'\xef\xbb\xbf' + data) if name == 'Install.ps1' else data
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

Windows 一键安装包。下载 **{archive.name}**，完整解压后双击 **Install.cmd**。

安装完成后，在桌面端 **设置 → 宠物 → 刷新** 中选择 **Codex娘**；必要时重启桌面端，输入 `/pet` 显示。

- 无需管理员权限、Python、Git 或联网；支持 `CODEX_HOME`。
- 文件校验、安装后复核、升级前备份，重复安装相同版本不会重复备份。
- v2 透明精灵图，九种动画状态和十六个视线方向。
- 面向支持 v2 自定义宠物的 Windows 桌面端，本地安装不会自动同步到网页账户。

`SHA256SUMS.txt` 提供安装包的 SHA-256 校验值。\n''', encoding='utf8')
print(json.dumps({'tag': TAG, 'archive': str(archive), 'bytes': archive.stat().st_size, 'sha256': checksum}, ensure_ascii=False))
