"""Inspect packaged atlases without changing image data."""
import hashlib
import json
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
BASE = Image.open(ROOT / 'assets/spritesheet.png').convert('RGBA')
FOODS = ('fish', 'cake', 'lollipop', 'dango', 'diamond')
COUNTS = (6, 8, 8, 4, 5, 8, 6, 6, 6)


def cell(image, column, row):
    return image.crop((column * 192, row * 208, (column + 1) * 192, (row + 1) * 208))


def main():
    report = {}
    first = None
    digests = set()
    for food in FOODS:
        path = ROOT / 'native/packages' / food / 'spritesheet.png'
        image = Image.open(path).convert('RGBA')
        assert image.size == (1536, 2288)
        assert path.stat().st_size < 20 * 1024 * 1024
        for row in range(11):
            for column in range(8):
                frame = cell(image, column, row)
                if row in (0, 1, 2, 4, 8, 9, 10) and (row, column) != (0, 6):
                    assert frame.tobytes() == cell(BASE, column, row).tobytes(), (food, row, column, 'unchanged frame')
                if row < 9 and column >= COUNTS[row] and (row, column) != (0, 6):
                    assert frame.getchannel('A').getextrema() == (0, 0), (food, row, column, 'unused cell')
        assert cell(image, 6, 0).tobytes() == cell(BASE, 0, 0).tobytes(), 'v2 neutral cell'
        if first is not None:
            for row in range(11):
                if row != 7:
                    assert image.crop((0, row * 208, 1536, (row + 1) * 208)).tobytes() == first.crop((0, row * 208, 1536, (row + 1) * 208)).tobytes()
        else:
            first = image
        assert len({cell(image, column, 7).tobytes() for column in range(6)}) == 4
        digest = hashlib.sha256(path.read_bytes()).hexdigest()
        digests.add(digest)
        report[food] = {'sha256': digest, 'bytes': path.stat().st_size, 'valid': True}
    assert len(digests) == 5
    (ROOT / 'native/validation.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf8', newline='\n')
    print('PASS: five v2 atlases; correct unused/neutral cells; original motion, idle, jump, reading and 16 looks preserved; food variants differ only in working row')


if __name__ == '__main__':
    main()
