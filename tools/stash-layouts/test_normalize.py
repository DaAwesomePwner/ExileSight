"""Regression tests for the dev-only normalizer; run with python3 directly."""
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

from normalize import normalize


def measured(values, dimension='h'):
    cells = []
    for i, value in enumerate(values):
        w, h = (value, 40) if dimension == 'w' else (40, value)
        cells.append(dict(id=f'cell-{i}', x=100 + 100 * i - w / 2,
                          y=100 - h / 2, w=w, h=h, itemName='Évidence'))
    return dict(tabId='synthetic', page='general', cells=cells, folderOffsets={},
                referenceResolution=dict(width=1000, height=1000))


class MedianGuardTests(unittest.TestCase):
    def test_noisy_chain_and_exact_boundary_are_accepted(self):
        for dimension in ('w', 'h'):
            for values in ([36, 40, 43], [34, 38, 40, 40, 46]):
                with self.subTest(dimension=dimension, values=values):
                    result, move = normalize(measured(values, dimension))
                    self.assertEqual({40}, {c[dimension] for c in result['cells']})
                    self.assertEqual(0, move)
                    self.assertEqual(result, normalize(result)[0])

    def test_chained_outliers_on_either_side_are_rejected(self):
        for dimension in ('w', 'h'):
            for values, cell in (([30, 34, 38, 40, 40], 'cell-0'),
                                 ([40, 40, 44, 50], 'cell-3')):
                with self.subTest(dimension=dimension, values=values):
                    with self.assertRaisesRegex(ValueError,
                            rf'synthetic/general: {cell} {dimension}=.*deviates .*%.*median'):
                        normalize(measured(values, dimension))

    def test_rejection_writes_nothing_and_success_preserves_utf8(self):
        with tempfile.TemporaryDirectory(dir=Path(__file__).parent) as directory:
            good = Path(directory) / 'a.layout.json'
            bad = Path(directory) / 'z.layout.json'
            good.write_text(json.dumps(measured([36, 40, 43]), ensure_ascii=False),
                            encoding='utf-8')
            bad.write_text(json.dumps(measured([30, 34, 38, 40, 40])), encoding='utf-8')
            original = {p: p.read_bytes() for p in (good, bad)}
            command = [sys.executable, str(Path(__file__).with_name('normalize.py')), directory]
            rejected = subprocess.run(command, capture_output=True, text=True, encoding='utf-8')
            self.assertNotEqual(0, rejected.returncode)
            self.assertIn('synthetic/general: cell-0', rejected.stderr)
            self.assertEqual(original, {p: p.read_bytes() for p in original})
            bad.unlink()
            accepted = subprocess.run(command, capture_output=True, text=True, encoding='utf-8')
            self.assertEqual(0, accepted.returncode, accepted.stderr)
            self.assertIn('Évidence', good.read_text(encoding='utf-8'))
            normalized = good.read_bytes()
            checked = subprocess.run(command + ['--check'], capture_output=True)
            self.assertEqual(0, checked.returncode, checked.stderr)
            self.assertEqual(normalized, good.read_bytes())


if __name__ == '__main__':
    unittest.main()
