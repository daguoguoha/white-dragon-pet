"""Package the pure-material variants, without a companion executable."""
from pathlib import Path
import hashlib
import json
import zipfile

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'native'
OUT = ROOT / 'dist'
FOODS = ('fish', 'cake', 'lollipop', 'dango', 'diamond')
checks = json.loads((SOURCE / 'packages/checksums.json').read_text(encoding='utf-8-sig'))
for food in FOODS:
    name = food + '/spritesheet.png'
    assert hashlib.sha256((SOURCE / 'packages' / name).read_bytes()).hexdigest() == checks[name]
paths = ['README.md', 'preview.html', 'Install-Materials.ps1', 'packages/checksums.json']
paths += [f'Use-{food}.cmd' for food in FOODS]
paths += [f'packages/{food}/{name}' for food in FOODS for name in ('pet.json', 'spritesheet.png')]
archive = OUT / 'Codex-Girl-Native-Materials-v1.zip'
with zipfile.ZipFile(archive, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=9) as package:
    for name in sorted(paths):
        data = (SOURCE / name).read_bytes()
        if not name.endswith('.png'):
            content = data.decode('utf-8-sig').replace('\r\n', '\n')
            if name.endswith(('.ps1', '.cmd')):
                content = content.replace('\n', '\r\n')
            data = content.encode('utf8')
            if name.endswith('.ps1'):
                data = b'\xef\xbb\xbf' + data
        info = zipfile.ZipInfo('Codex-Girl-Native-Materials-v1/' + name, date_time=(2026, 10, 19, 0, 0, 0))
        info.create_system = 3
        info.compress_type = zipfile.ZIP_DEFLATED
        info.external_attr = 0o100644 << 16
        package.writestr(info, data)
with zipfile.ZipFile(archive) as package:
    assert package.testzip() is None
digest = hashlib.sha256(archive.read_bytes()).hexdigest()
(OUT / 'Native-Materials-SHA256SUMS.txt').write_text(f'{digest}  {archive.name}\n', encoding='ascii')
print(json.dumps({'archive': str(archive), 'sha256': digest, 'bytes': archive.stat().st_size}))
