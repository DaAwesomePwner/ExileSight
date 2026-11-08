#!/usr/bin/env python3
"""Regularize measured JSON geometry; no screenshots or runtime dependencies."""
import argparse
import copy
import json
import math
from pathlib import Path
from statistics import median


def components(items, adjacent):
    """Connected measurement neighbours, with deterministic input ordering."""
    remaining = list(items)
    while remaining:
        group = [remaining.pop(0)]
        for item in group:
            neighbours = [other for other in remaining if adjacent(item, other)]
            group.extend(neighbours)
            remaining = [other for other in remaining if other not in neighbours]
        yield group


def half(value):
    return math.floor(value * 2 + 0.5) / 2


def normalize(layout):
    result = copy.deepcopy(layout)
    cells = result['cells']
    origins = [layout['folderOffsets'].get(c.get('folder'), {'x': 0, 'y': 0})
               for c in cells]
    centers = [[c[a] + c[s] / 2 + o[a] for a, s in [('x', 'w'), ('y', 'h')]]
               for c, o in zip(cells, origins)]
    before = copy.deepcopy(centers)
    for axis in range(2):
        for group in components(range(len(cells)),
                                lambda i, j: abs(before[i][axis] - before[j][axis]) <= 3):
            values = [before[i][axis] for i in group]
            # Refuse chains that might bridge genuinely different rows/columns.
            if max(values) - min(values) > 6:
                raise ValueError('Ambiguous row/column spanning more than 6 px')
            target = half((min(values) + max(values)) / 2)
            for i in group:
                centers[i][axis] = target
    for group in components(range(len(cells)), lambda i, j: all(
            max(cells[i][s], cells[j][s]) <= min(cells[i][s], cells[j][s]) * 1.15
            for s in ('w', 'h'))):
        sizes = [half(median(cells[i][s] for i in group)) for s in ('w', 'h')]
        # Frame measurements can exceed the pitch (notably Essence). Cap the
        # common box at the smallest pitch on an aligned row/column, not per cell.
        for axis in range(2):
            pitches = [abs(centers[i][axis] - centers[j][axis])
                       for i in group for j in group if i < j
                       and centers[i][1 - axis] == centers[j][1 - axis]
                       and centers[i][axis] != centers[j][axis]]
            if pitches:
                sizes[axis] = min(sizes[axis], min(pitches))
        for i in group:
            for axis, (a, s) in enumerate([('x', 'w'), ('y', 'h')]):
                cells[i][s] = sizes[axis]
                cells[i][a] = centers[i][axis] - sizes[axis] / 2 - origins[i][a]
    moves = [math.dist(a, b) for a, b in zip(before, centers)]
    if max(moves) > 3:
        raise ValueError(f'Centre movement {max(moves):.3f} exceeds 3 px')
    for i, a in enumerate(cells):
        x, y = centers[i]
        if (x - a['w'] / 2 < 0 or y - a['h'] / 2 < 0
                or x + a['w'] / 2 > layout['referenceResolution']['width']
                or y + a['h'] / 2 > layout['referenceResolution']['height']):
            raise ValueError(f"Outside screenshot: {a['id']}")
        for j, b in enumerate(cells[:i]):
            if (abs(x - centers[j][0]) < (a['w'] + b['w']) / 2
                    and abs(y - centers[j][1]) < (a['h'] + b['h']) / 2):
                raise ValueError(f"Overlapping cells: {a['id']}, {b['id']}")
    return result, max(moves)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('directory', nargs='?', type=Path,
                        default=Path(__file__).resolve().parents[2] / 'data/stash-layouts/poe1')
    parser.add_argument('--check', action='store_true', help='Fail if normalization would change geometry')
    args = parser.parse_args()
    pending = []
    for path in sorted(args.directory.glob('*.layout.json')):
        original = json.loads(path.read_text())
        normalized, move = normalize(original)
        if normalize(normalized)[0] != normalized:
            raise ValueError(f'{path.name}: normalization is not idempotent; inspect size classes')
        print(f'{path.name}: largest centre move {move:.3f} px')
        if original != normalized:
            pending.append((path, normalized))
    if not list(args.directory.glob('*.layout.json')):
        parser.error('No layout files found')
    if args.check and pending:
        parser.exit(1, 'Layouts need normalization\n')
    for path, normalized in pending:
        path.write_text(json.dumps(normalized, indent=2, ensure_ascii=False) + '\n')


if __name__ == '__main__':
    main()
