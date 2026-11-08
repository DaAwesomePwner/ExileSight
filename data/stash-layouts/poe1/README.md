# PoE1 stash layouts

Eight partially mapped pages ship: Currency General, Atlas and League; Fragment
General; Delve, Essence, Blight and Delirium (the latter four use page `general`).
Allflame is deferred: only one cell has a confirmed identity, but calibration
requires two. Scarab and Eldritch pages are excluded. Shared Currency overflow
slots and other free storage grids are not fixed item cells and are not mapped.

Every cell marked `high` in the owner's identification table is included on the
shipped pages. Identity determines inclusion, not today's price availability.
Four additional corrupted Essence cells were independently confirmed against
GitHub reference icons and exact names in poe.ninja. Allflame remains deferred
because it has only one independently confirmed cell.

Exchange overviews omit low-volume items. Names absent from Standard or the current
challenge league (Allflame at measurement time) are retained when identity is high;
the runtime simply draws no tag until a price is available. This includes Scroll
Fragment, lower essence tiers, Legion splinters and some fossils. See
[ATTRIBUTION.md](ATTRIBUTION.md) for screenshot and name evidence and
[IDENTIFICATION-RECHECK.md](IDENTIFICATION-RECHECK.md) for uncertain-cell review.
No Exile-UI coordinates, item ordering or icons are used.

## Updating layouts after a patch

Game-specific geometry and names live in JSON; a patch does not require C# edits.

1. Take an owner-provided screenshot of the entire game **client area**, with the fixed
   tab and page visible. Record its pixel width/height, date and permission to use it.
   Redact names/chat before sharing. Do not include desktop borders in measurements.
2. Measure each cell's top-left x/y and width/height, in that screenshot's pixels.
   Folder offsets are added to cell x/y; do not apply an offset twice.
3. Author `<tab>-<page>.layout.json` against `layout.schema.json`. Use stable cell IDs,
   a supported price category and a poe.ninja ID or exact English item name. Record
   author, date and method exactly `measured from screenshot at WIDTHxHEIGHT` (lowercase x).
   Record screenshot source/permission in an adjacent attribution Markdown file.
   Optionally use `python3 tools/stash-layouts/snap.py screenshot.png CX CY WIDTH HEIGHT`
   from the repository root. It searches pixel intensity transitions within two pixels
   of each estimated frame edge; inspect the result against the screenshot before
   accepting it. ImageMagick is required; no images are committed.
4. Use a distinct tabId/page for every page. At least two distinct cell centers are
   needed for calibration. Runtime validation rejects missing provenance, duplicate
   cells/pages, unknown categories, nonfinite numbers and out-of-bounds geometry.
5. Update the name evidence fixture (`tests/ExileSight.Features.Stashninja.Tests/fixtures/measured-names.json`)
   with independent identity evidence. Record API IDs when available; do not remove
   high-confidence cells merely because a price is absent. For uncertain cells, require
   independent icon confirmation and exact spelling in Standard or the current challenge
   league (discover it through poe.ninja `poe1/api/data/index-state`). Update attribution
   and unmapped cells below. Run
   `dotnet build ExileSight.slnx -warnaserror` and
   `dotnet test tests/ExileSight.Features.Stashninja.Tests`. Build the app: these files are copied to `data/stash-layouts/poe1` beside the app.
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

The synthetic engine-test fixture is not shipped as a layout. Shipped layouts are also
loaded by the tests, with unique pages, non-overlapping rectangles and recorded identity evidence checked. Owner Windows
checks must cover DPI, ultrawide geometry, click-through, calibration and one search per click.

## Unmapped cells

Coordinates below are approximate centers in the 2000x837 source, for owner
in-game verification. “Unconfirmed” includes guesses and research entries without
independent evidence; their proposed identities are deliberately not reproduced.
Each coordinate is one omitted fixed cell. Free storage/overflow slots are excluded
from counts.

### currency-general — 25 included, 20 omitted

- Unconfirmed identity: (236,161); (281,161); (46,259); (90,259); (134,259); (236,259); (281,259); (340,259); (385,259); (429,259); (46,312); (385,312); (429,312); (46,355); (90,355); (178,355); (385,355).
- Unknown identity: (134,355); (429,355); (473,355).

### currency-atlas — 13 included, 28 omitted

- Unconfirmed identity: (58,161); (107,161); (156,161); (58,205); (107,205); (156,205); (236,161); (281,161); (236,205); (281,205); (259,250); (362,161); (451,205); (107,250); (385,250); (428,250); (340,294); (385,294); (428,294).
- Unconfirmed individual Scouting Report identities (also absent from the checked API lists): (58,294); (58,338); (58,383); (107,294); (107,338); (107,383); (156,294); (156,338); (156,383).

### currency-league — 4 included, 20 omitted

- Unknown identity: (215,164); (215,210); (215,257); (259,164); (259,210); (259,257); (303,164); (303,210); (303,257); (362,164); (362,210); (411,164); (411,210); (107,257); (107,308); (107,354); (155,257); (155,308); (155,354); (132,400).

### fragment-general — 21 included, 39 omitted

- Unconfirmed identity: (91,172); (91,218); (136,172); (136,218); (383,172); (383,218); (429,172); (429,218); (108,460); (108,506); (156,460); (156,506); (236,460); (236,506); (283,460); (283,506); (364,460); (364,506); (411,460); (411,506); (46,556); (94,556); (141,556); (189,556); (236,556); (284,556); (331,556); (378,556); (426,556); (473,556); (332,172); (284,218); (332,218); (108,359); (260,359); (199,410); (260,410); (320,410); (411,410).

### fragment-allflame — 0 included, 8 omitted

- Unconfirmed identity: (180,314); (233,314); (233,377); (286,314); (286,377); (338,314); (338,377).
- Confirmed Gilded; page needs two confirmed cells for calibration: (180,377).

### delve — 29 included, 0 omitted

None.
### essence — 104 included, 4 omitted

- No identifying ghost: (429,445); (429,483); (429,521); (429,559).

### blight — 16 included, 0 omitted

None.

### delirium — 14 included, 4 omitted

- No identifying ghost: (260,197); (260,251); (260,304); (260,358).
