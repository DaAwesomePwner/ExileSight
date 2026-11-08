#!/usr/bin/env bash
# Re-render docs/design/mockups/png/*.png from the HTML mockups with headless Chrome.
# Usage: docs/design/render-mockups.sh [name ...]   (no names = all mockups)
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
chrome="${CHROME:-/Applications/Google Chrome.app/Contents/MacOS/Google Chrome}"
[[ -x "$chrome" ]] || chrome="$(command -v google-chrome || command -v chromium || true)"
[[ -n "$chrome" ]] || { echo "Set CHROME to a Chrome or Chromium binary" >&2; exit 1; }
# Frame size per mockup, in CSS px at 1x (matches the design canvas).
declare -A size=(
  [item-card]=1440,810 [item-card-states]=1360,700 [map-card]=760,700
  [trade]=1020,680 [ambient-hud]=1300,720 [app-home]=1280,820
  [app-item-rules]=1280,820 [app-overlay-settings]=1280,1000 [tokens]=1280,940
)
names=("$@"); [[ ${#names[@]} -gt 0 ]] || names=("${!size[@]}")
profile="$(mktemp -d)"; trap 'rm -rf "$profile"' EXIT
for name in "${names[@]}"; do
  [[ -n "${size[$name]:-}" ]] || { echo "Unknown mockup: $name" >&2; exit 1; }
  "$chrome" --headless=new --disable-gpu --hide-scrollbars --force-device-scale-factor=1 \
    --user-data-dir="$profile" --virtual-time-budget=6000 --window-size="${size[$name]}" \
    --screenshot="$here/mockups/png/$name.png" "file://$here/mockups/$name.html" >/dev/null 2>&1
  echo "rendered $name"
done
