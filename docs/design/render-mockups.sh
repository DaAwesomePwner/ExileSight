#!/usr/bin/env bash
# Re-render docs/design/mockups/png/*.png from the HTML mockups with headless Chrome.
# Usage: docs/design/render-mockups.sh [name ...]   (no names = all mockups)
# Works with the bash 3.2 that ships with macOS.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
chrome="${CHROME:-/Applications/Google Chrome.app/Contents/MacOS/Google Chrome}"
[[ -x "$chrome" ]] || chrome="$(command -v google-chrome || command -v chromium || true)"
[[ -n "$chrome" ]] || { echo "Set CHROME to a Chrome or Chromium binary" >&2; exit 1; }

all="item-card item-card-states map-card trade ambient-hud app-home app-item-rules app-overlay-settings tokens"

# Frame size per mockup, in CSS px at 1x (matches the design canvas).
size_of() {
  case "$1" in
    item-card) echo 1440,810 ;;
    item-card-states) echo 1360,700 ;;
    map-card) echo 760,700 ;;
    trade) echo 1020,680 ;;
    ambient-hud) echo 1300,720 ;;
    app-home|app-item-rules) echo 1280,820 ;;
    app-overlay-settings) echo 1280,1000 ;;
    tokens) echo 1280,940 ;;
    *) return 1 ;;
  esac
}

[[ $# -gt 0 ]] || set -- $all
profile="$(mktemp -d)"; trap 'rm -rf "$profile"' EXIT
for name in "$@"; do
  size="$(size_of "$name")" || { echo "Unknown mockup: $name" >&2; exit 1; }
  "$chrome" --headless=new --disable-gpu --hide-scrollbars --force-device-scale-factor=1 \
    --user-data-dir="$profile" --virtual-time-budget=6000 --window-size="$size" \
    --screenshot="$here/mockups/png/$name.png" "file://$here/mockups/$name.html" >/dev/null 2>&1
  echo "rendered $name"
done
