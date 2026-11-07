# Authoring PoE1 stash layouts

This directory intentionally contains **no supported layouts**. No coordinates or data
from Exile-UI's `tabs.json` may be copied: its provenance is unproven (D-008).
Exile-UI modules/stash-ninja.ahk is a behaviour reference only. No bulk-exchange POST is used.

1. Take an owner-provided screenshot of the entire game **client area**, with the fixed
   tab and page visible. Record its pixel width/height, date and permission to use it.
   Redact names/chat before sharing. Do not include desktop borders in measurements.
2. Measure each cell's top-left x/y and width/height, in that screenshot's pixels.
   Folder offsets are added to cell x/y; do not apply an offset twice.
3. Author `<tab>-<page>.layout.json` against `layout.schema.json`. Use stable cell IDs,
   a supported price category and a poe.ninja ID or exact English item name. Record
   author, date and method exactly `measured from screenshot at WIDTHxHEIGHT` (lowercase x).
   Record screenshot source/permission in an adjacent attribution Markdown file.
4. Use a distinct tabId/page for every page. At least two distinct cell centers are
   needed for calibration. Runtime validation rejects missing provenance, duplicate
   cells/pages, unknown categories, nonfinite numbers and out-of-bounds geometry.
5. Build the app: these files are copied to `data/stash-layouts/poe1` beside the app.
   Press Ctrl+Shift+N or long-press omni and choose **Price stash tab…**. Select the
   tab/page, then Load. Unsupported tabs show a clear no-layout message.
6. Choose any two distinct cells in the calibration selectors, preferably empty ones.
   Click Calibrate, then those cell centers in order. Esc cancels and restores Highlight. Clicks pass through to the game: use empty
   cells and ensure no item is held. This observes physical clicks; it sends no input.
   Calibration fits uniform scale/translation, rejects inconsistent clicks, and is
   stored atomically under `%APPDATA%/ExileSight/poe1/stashninja/calibration.json`.
   Calibration uses a cached SHA-256 geometry fingerprint and a SHA-256 key per
   client resolution, bounded to 1000 entries and 1 MiB. Old unhashed stores are
   preserved as `.corrupt` files and require recalibration. Moving/resizing
   during calibration cancels it. Default placement scales by client height and
   compensates for black bars wider than 21:9.

Profiles are configurable through the feature setting `stashninja.profiles`, an array
of `{ "Name": "Valuable", "Minimum": 10, "High": 100, "LowColor": "#99DDFF",
"HighColor": "#FFD700" }`. Minimum controls highlighting; High controls tag color.
Search includes all priced item types at or above Minimum, escapes only regex metacharacters (spaces, apostrophes and # stay literal),
and is disabled if the complete quoted expression exceeds 250 characters. It does not
inspect stash contents or quantities. The highlight button spends its own physical-click
token on one PasteSearch; loading prices and calibration never send input.

Seven-point sparklines compound daily percentage changes from a baseline of 100;
missing points remain gaps. Missing prices are omitted and the priced-cell count is
shown. Stale/incomplete prices are labeled. Divine text is omitted without a valid rate.

The test fixture is explicitly synthetic and is not shipped as a layout. Owner Windows
checks must cover DPI, ultrawide geometry, click-through, calibration and one search per click.
