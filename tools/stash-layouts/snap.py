#!/usr/bin/env python3
"""Measure slot-frame edges near a manually identified centre (requires ImageMagick).

Usage: snap.py screenshot.png cx cy width height
The width/height are visual estimates, not a game-layout database. Inspect results
against the screenshot before accepting them. Output uses half-open pixel bounds.
"""
import json
import shutil
import subprocess
import sys


def measure(pixels, cx, cy, width, height, image_width=2000):
    def intensity(x, y):
        return pixels[y * image_width + x]

    def edge(expected, vertical, start, end):
        # A frame is a long straight intensity transition. Average along its
        # middle to exclude rounded corners; search only near the estimated edge.
        def score(pos):
            if vertical:
                return sum(abs(intensity(pos, p) - intensity(pos - 1, p)) for p in range(start, end))
            return sum(abs(intensity(p, pos) - intensity(p, pos - 1)) for p in range(start, end))
        return max(range(round(expected) - 2, round(expected) + 3), key=score)

    left = edge(cx - width / 2, True, round(cy - height / 2 + 7), round(cy + height / 2 - 7))
    right = edge(cx + width / 2, True, round(cy - height / 2 + 7), round(cy + height / 2 - 7))
    top = edge(cy - height / 2, False, left + 7, right - 7)
    bottom = edge(cy + height / 2, False, left + 7, right - 7)
    return dict(x=left, y=top, w=right-left, h=bottom-top)


def read_pixels(path):
    return subprocess.check_output([shutil.which('magick') or '/opt/homebrew/bin/magick',
                                   str(path), '-colorspace', 'Gray', '-depth', '8', 'gray:-'])


if __name__ == '__main__':
    print(json.dumps(measure(read_pixels(sys.argv[1]), *map(float, sys.argv[2:]))))
