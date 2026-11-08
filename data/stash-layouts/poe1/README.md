# PoE1 stash layouts

Eight partially mapped pages ship: Currency General, Atlas and League; Fragment
General; Delve, Essence, Blight and Delirium (the latter four use page `general`).
Allflame is deferred: only one cell has a confirmed identity, but calibration
requires two. Scarab and Eldritch pages are excluded. Shared Currency overflow
slots and other free storage grids are not fixed item cells and are not mapped.

Only independently identified `high` cells from the owner's screenshot research
are included, and only when their exact English name appears in the Standard
poe.ninja response for their type. No uncertain identities were promoted. Missing
prices in a different league are omitted by the runtime. In particular, Standard
lists only some essence tiers. See [ATTRIBUTION.md](ATTRIBUTION.md) for evidence.
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
   from the allowed poe.ninja API, and update attribution and unmapped cells below. Run
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
loaded by the tests, with unique pages, non-overlapping rectangles and recorded API names checked. Owner Windows
checks must cover DPI, ultrawide geometry, click-through, calibration and one search per click.

## Unmapped cells

Coordinates below are approximate centers in the 2000x837 source, for owner
in-game verification. “Unconfirmed” includes guesses and research entries without
independent evidence; their proposed identities are deliberately not reproduced.
Each coordinate is one omitted fixed cell. Free storage/overflow slots are excluded
from counts.

### currency-general — 24 included, 21 omitted

- Unconfirmed identity: (236,161); (281,161); (46,259); (90,259); (134,259); (236,259); (281,259); (340,259); (385,259); (429,259); (46,312); (385,312); (429,312); (46,355); (90,355); (178,355); (385,355).
- Unknown identity: (134,355); (429,355); (473,355).
- Scroll Fragment — exact name absent from Standard Currency list: (46,161).

### currency-atlas — 13 included, 28 omitted

- Unconfirmed identity: (58,161); (107,161); (156,161); (58,205); (107,205); (156,205); (236,161); (281,161); (236,205); (281,205); (259,250); (362,161); (451,205); (107,250); (385,250); (428,250); (340,294); (385,294); (428,294).
- Scouting report; not listed by poe.ninja: (58,294); (58,338); (58,383); (107,294); (107,338); (107,383); (156,294); (156,338); (156,383).

### currency-league — 4 included, 20 omitted

- Unknown identity: (215,164); (215,210); (215,257); (259,164); (259,210); (259,257); (303,164); (303,210); (303,257); (362,164); (362,210); (411,164); (411,210); (107,257); (107,308); (107,354); (155,257); (155,308); (155,354); (132,400).

### fragment-general — 16 included, 44 omitted

- Unconfirmed identity: (91,172); (91,218); (136,172); (136,218); (383,172); (383,218); (429,172); (429,218); (108,460); (108,506); (156,460); (156,506); (236,460); (236,506); (283,460); (283,506); (364,460); (364,506); (411,460); (411,506); (46,556); (94,556); (141,556); (189,556); (236,556); (284,556); (331,556); (378,556); (426,556); (473,556); (332,172); (284,218); (332,218); (108,359); (260,359); (199,410); (260,410); (320,410); (411,410).
- Timeless Eternal Empire Splinter — exact name absent from Standard Fragment list: (164,267).
- Timeless Karui Splinter — exact name absent from Standard Fragment list: (212,267).
- Timeless Vaal Splinter — exact name absent from Standard Fragment list: (259,267).
- Timeless Templar Splinter — exact name absent from Standard Fragment list: (307,267).
- Timeless Maraketh Splinter — exact name absent from Standard Fragment list: (354,267).

### fragment-allflame — 0 included, 8 omitted

- Unconfirmed identity: (180,314); (233,314); (233,377); (286,314); (286,377); (338,314); (338,377).
- Confirmed Gilded; page needs two confirmed cells for calibration: (180,377).

### delve — 26 included, 3 omitted

- Prismatic Fossil — exact name absent from Standard Fossil list: (209,213).
- Lucent Fossil — exact name absent from Standard Fossil list: (363,213).
- Fractured Fossil — exact name absent from Standard Fossil list: (439,265).

### essence — 35 included, 73 omitted

- Unconfirmed identity: (467,445); (467,483); (467,521); (467,559).
- No identifying ghost: (429,445); (429,483); (429,521); (429,559).
- Shrieking Essence of Greed — exact name absent from Standard Essence list: (92,139).
- Screaming Essence of Greed — exact name absent from Standard Essence list: (130,139).
- Wailing Essence of Greed — exact name absent from Standard Essence list: (168,139).
- Weeping Essence of Greed — exact name absent from Standard Essence list: (206,139).
- Muttering Essence of Greed — exact name absent from Standard Essence list: (244,139).
- Whispering Essence of Greed — exact name absent from Standard Essence list: (282,139).
- Shrieking Essence of Contempt — exact name absent from Standard Essence list: (92,177).
- Wailing Essence of Contempt — exact name absent from Standard Essence list: (168,177).
- Weeping Essence of Contempt — exact name absent from Standard Essence list: (206,177).
- Muttering Essence of Contempt — exact name absent from Standard Essence list: (244,177).
- Whispering Essence of Contempt — exact name absent from Standard Essence list: (282,177).
- Shrieking Essence of Hatred — exact name absent from Standard Essence list: (92,216).
- Screaming Essence of Hatred — exact name absent from Standard Essence list: (130,216).
- Wailing Essence of Hatred — exact name absent from Standard Essence list: (168,216).
- Weeping Essence of Hatred — exact name absent from Standard Essence list: (206,216).
- Muttering Essence of Hatred — exact name absent from Standard Essence list: (244,216).
- Whispering Essence of Hatred — exact name absent from Standard Essence list: (282,216).
- Screaming Essence of Woe — exact name absent from Standard Essence list: (130,254).
- Wailing Essence of Woe — exact name absent from Standard Essence list: (168,254).
- Weeping Essence of Woe — exact name absent from Standard Essence list: (206,254).
- Muttering Essence of Woe — exact name absent from Standard Essence list: (244,254).
- Whispering Essence of Woe — exact name absent from Standard Essence list: (282,254).
- Shrieking Essence of Fear — exact name absent from Standard Essence list: (92,292).
- Screaming Essence of Fear — exact name absent from Standard Essence list: (130,292).
- Weeping Essence of Fear — exact name absent from Standard Essence list: (206,292).
- Muttering Essence of Fear — exact name absent from Standard Essence list: (244,292).
- Shrieking Essence of Anger — exact name absent from Standard Essence list: (92,330).
- Screaming Essence of Anger — exact name absent from Standard Essence list: (130,330).
- Wailing Essence of Anger — exact name absent from Standard Essence list: (168,330).
- Weeping Essence of Anger — exact name absent from Standard Essence list: (206,330).
- Muttering Essence of Anger — exact name absent from Standard Essence list: (244,330).
- Shrieking Essence of Torment — exact name absent from Standard Essence list: (92,368).
- Screaming Essence of Torment — exact name absent from Standard Essence list: (130,368).
- Wailing Essence of Torment — exact name absent from Standard Essence list: (168,368).
- Weeping Essence of Torment — exact name absent from Standard Essence list: (206,368).
- Muttering Essence of Torment — exact name absent from Standard Essence list: (244,368).
- Screaming Essence of Sorrow — exact name absent from Standard Essence list: (130,407).
- Wailing Essence of Sorrow — exact name absent from Standard Essence list: (168,407).
- Muttering Essence of Sorrow — exact name absent from Standard Essence list: (244,407).
- Wailing Essence of Rage — exact name absent from Standard Essence list: (168,445).
- Weeping Essence of Rage — exact name absent from Standard Essence list: (206,445).
- Shrieking Essence of Suffering — exact name absent from Standard Essence list: (92,483).
- Screaming Essence of Suffering — exact name absent from Standard Essence list: (130,483).
- Wailing Essence of Suffering — exact name absent from Standard Essence list: (168,483).
- Weeping Essence of Suffering — exact name absent from Standard Essence list: (206,483).
- Screaming Essence of Wrath — exact name absent from Standard Essence list: (130,521).
- Wailing Essence of Wrath — exact name absent from Standard Essence list: (168,521).
- Shrieking Essence of Doubt — exact name absent from Standard Essence list: (92,559).
- Screaming Essence of Doubt — exact name absent from Standard Essence list: (130,559).
- Wailing Essence of Doubt — exact name absent from Standard Essence list: (168,559).
- Weeping Essence of Doubt — exact name absent from Standard Essence list: (206,559).
- Wailing Essence of Loathing — exact name absent from Standard Essence list: (353,139).
- Screaming Essence of Loathing — exact name absent from Standard Essence list: (391,139).
- Wailing Essence of Zeal — exact name absent from Standard Essence list: (353,177).
- Screaming Essence of Zeal — exact name absent from Standard Essence list: (391,177).
- Wailing Essence of Anguish — exact name absent from Standard Essence list: (353,216).
- Screaming Essence of Anguish — exact name absent from Standard Essence list: (391,216).
- Shrieking Essence of Anguish — exact name absent from Standard Essence list: (429,216).
- Wailing Essence of Spite — exact name absent from Standard Essence list: (353,254).
- Screaming Essence of Spite — exact name absent from Standard Essence list: (391,254).
- Screaming Essence of Scorn — exact name absent from Standard Essence list: (391,292).
- Screaming Essence of Misery — exact name absent from Standard Essence list: (391,368).
- Shrieking Essence of Misery — exact name absent from Standard Essence list: (429,368).
- Screaming Essence of Dread — exact name absent from Standard Essence list: (391,407).
- Shrieking Essence of Dread — exact name absent from Standard Essence list: (429,407).

### blight — 16 included, 0 omitted

None.

### delirium — 13 included, 5 omitted

- No identifying ghost: (260,197); (260,251); (260,304); (260,358).
- Skittering Delirium Orb — exact name absent from Standard DeliriumOrb list: (402,197).
