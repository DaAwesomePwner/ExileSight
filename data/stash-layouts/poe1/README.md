# PoE1 stash layouts

Eight partially mapped pages ship: Currency General, Atlas and League; Fragment
General; Delve, Essence, Blight and Delirium (the latter four use page `general`).
Allflame is deferred: only one cell has a confirmed identity, but calibration
requires two. Scarab and Eldritch pages are excluded. Shared Currency overflow
slots and other free storage grids are not fixed item cells and are not mapped.

Included cells have an independent GitHub icon reference in the evidence fixture,
or an explicit `noReferenceIcon` reason where an appropriate icon could not be
found. Initial `high` labels alone are not icon confirmation: the atlas Hunter and
Warlord mappings were corrected by comparison, and the unidentified silver spiked
orb at (451,161) is omitted. Four corrupted Essence cells were independently
confirmed against GitHub icons. Allflame remains deferred because only one cell
has a confirmed identity.

Exchange overviews omit low-volume items. Confirmed identities stay in the layout
even without a current API entry; the runtime draws no tag until a price is
available. An absent API ID is not an identification failure. See
[ATTRIBUTION.md](ATTRIBUTION.md) for screenshot and name evidence and
[IDENTIFICATION-RECHECK.md](IDENTIFICATION-RECHECK.md) for the icon audit.
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
   accepting it. This is a **dev-only** Python helper, not shipped app code. Install
   ImageMagick with `magick` on PATH; the helper reads the image dimensions itself.
   No images are committed.
4. After measuring/authoring, run `python3 tools/stash-layouts/normalize.py` from
   the repository root (standard library only). It aligns connected centres within
   3 reference pixels to one row/column centre, using the extrema midpoint rounded
   to the nearest half-pixel. Cells linked by widths **and** heights within 15% use
   common median dimensions, capped at the smallest aligned pitch to avoid overlap.
   Before rounding or capping dimensions, a median-relative guard requires every
   member's original width and height to be within 15% of its class median width
   and height, respectively. Transitive grouping accommodates measurement noise:
   a 36→40→43 height chain is accepted because all members are within 15% of 40.
   An outlier is refused with its page, cell, dimension and percentage deviation.
   The helper validates every page before writing any files; resolve rejected
   measurements from the measurement notes before rerunning.
   Distinct size classes (such as larger resonators) remain separate. Origins may
   use quarter-pixels to centre half-pixel dimensions exactly; the schema allows this.
   The helper refuses ambiguous chains spanning over 6 pixels, centre moves over
   3 pixels (Euclidean), overlaps, out-of-bounds boxes and non-idempotent results.
   Inspect these cases against the measurement notes rather than relaxing the limits.
   It changes geometry only and reports the largest centre move per page. Re-running
   is idempotent; `python3 tools/stash-layouts/normalize.py --check` checks without
   writing. Shipped-layout tests enforce centre spreads at most 0.5 pixels and
   identical dimensions within each size class. Geometry changes require recalibration.
5. Use a distinct tabId/page for every page. At least two distinct cell centers are
   needed for calibration. Runtime validation rejects missing provenance, duplicate
   cells/pages, unknown categories, nonfinite numbers and out-of-bounds geometry.
6. Update `tests/ExileSight.Features.Stashninja.Tests/fixtures/measured-names.json`.
   Compare every candidate against an appropriate independent GitHub icon and record
   its pinned repo/path in `referenceIcon`. If none exists, record a concrete
   `noReferenceIcon` reason. Drop mismatches; do not trust an earlier `high` mark alone.
   Record API IDs when available, but never remove a confirmed cell solely for lacking
   a price. Check exact spelling in Standard and the current challenge league
   (discover it through poe.ninja `poe1/api/data/index-state`).
7. Update attribution and the Unmapped list below. Tests derive expected pages and
   cells from the layout files and evidence fixture; layout edits need no C# test edit.
8. Run `dotnet build ExileSight.slnx -warnaserror` and
   `dotnet test tests/ExileSight.Features.Stashninja.Tests`.
9. Launch the built app; layout files are copied to `data/stash-layouts/poe1` beside it.
   Press Ctrl+Shift+N or long-press omni and choose **Price stash tab…**. Select the
   tab/page, then Load. Unsupported tabs show a clear no-layout message.
10. With the matching stash page visible, check automatic grid calibration on a fresh
    resolution, or click **Auto-calibrate** to retry. Verify tag alignment on all cells,
    including filled cells, at 1080p/1440p/4K, windowed and borderless, with folders
    and ultrawide black bars. Confirm closed stash and unrelated pages refuse;
    changing page, focus or resolution during capture/search must discard the result.
    Failed Auto-calibrate attempts must retain the last good calibration and loaded
    tags, showing only the failure status. Foreground loss and page changes still
    hide tags normally.
    Stable identified pages must never blink. On a real page change, tags can stay hidden through capture and identification.
    On refusal, choose any two distinct empty cells in the calibration selectors.
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

- Unidentified silver spiked orb; does not match Hunter or Warlord references: (451,161).
- Unconfirmed identity: (58,161); (107,161); (156,161); (58,205); (107,205); (156,205); (236,161); (281,161); (236,205); (281,205); (259,250); (451,205); (107,250); (385,250); (428,250); (340,294); (385,294); (428,294).
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

## Recognition region runbook

Automatic prices default ON and need no teaching or menu. Every 450 ms, compare
the shared header signature against the identification baseline; Currency/Fragment
also compare their sub-tab strip (y100–138) with tags visible. Their cells start
at y142 or below. Stable pages cause no full reads and no capture hides. Signature
change threshold is .04 normalized mean absolute distance. Missing/blocked samples
with no detected change need one second of consecutive Unknown before hiding.

Unidentified scenes use sparse fixed-transform border pre-checks, no scale or
translation search: eight 33×33 patches per layout, at most 69,696 pixels for all
eight pages. The profile scans up to a ±12px normal band (scaled flanks reduce the range at
larger client scales) for coherent borders, requiring
≥18/19 samples to have normalized valley evidence >12 and absolute contrast ≥6 luminance. Weight line evidence by clamp((mean flank luminance − 8)/20, 0, 1) to suppress dark speckle. Score ≥12.5 gates full capture/identification.
After the initial immediate check, unidentified scenes have a minimum 1800 ms
interval even if the header changes every tick. Header checks remain 450 ms. An
already-calibrated page normally needs one admission interval when reopening from
an unidentified scene. First use needs two identification ticks: budget ~3.6 s
(two intervals), or ~5.4 s for three, plus capture/scoring/price loading. Immediate
first checks can be faster; failures or cooldowns can extend the delay.

Local identification compares every layout within ±3 px / ±0.3% scale, with
refinement (≤559 evaluations per layout). Require normalized inner-band score ≥16,
≥65% supported cells (evidence >6; all cells for layouts smaller than eight) and runner-up margin ≥3. The locator requires
joint score ≥14, final aligned score ≥12 and wide confidence ≥2.5.
Near bands are 3 reference pixels inset (Blight 1.5; Delve X 2.5/Y 3.5);
far bands are 8 pixels inset (Blight/Essence 8.5; Delve X 8.5/Y 7).
These offsets come from local median strip profiles; tag geometry is unchanged.
Normalize each valley by 16 plus the mean of its two flanks, setting depths below 4 luminance to zero. Discount layouts with fewer than eight sampled cells by .6 + count/20 (four cells: .8), and subtract 1.5 points per 1% scale drift beyond 0.5% from the start on these small layouts. This limits broad-search overfitting of a few slots. Recheck continuous-band evidence at the recovered transform using the sparse profile; insufficient continuity returns zero evidence. Near/far pairs share
one ±2-reference-pixel offset during search. Trim the strongest quarter of cells;
the final sub-pixel joint fit has no per-cell offset freedom. Track the strongest
aligned candidate throughout search so refinement does not inherit a displaced
maximum from the tolerant score. Local boundary hits at the cell centroid require wider calibration; scale/translation compensation alone is not a boundary hit.
Currency button thirds in `subTabs.currency/general`, `/atlas`, `/league` are
measured rectangles with provenance. Compare mean luminance after mapping through
the recovered transform in the grid capture; require a ≥6 lead and agreement with the best currency grid, which must lead its siblings by ≥3. Evaluate buttons only when currency wins the grid comparison. No teaching or stored image is needed.
Rank qualifying candidates by full grid score plus one tenth of their sparse score. If needed, at most two qualifying uncalibrated candidates get the broad
±3% height / ±5% scale search (≤40,000 evaluations each). Automatic identification
accepts a wide-found candidate only at score ≥18, within 1% of Fit scale, and ≥3
above every other layout's original local score (even if that layout subsequently
received a wide search). The score floor preserves the weakest real score, 18.40,
while rejecting the admitted scarab false candidates, 16.45/16.41. The locator's
all-cell coverage rule for layouts smaller than eight still applies. Manual
Auto-calibrate retains the wider locator bounds.
Only one fully admitted wider-search batch is allowed per header baseline, and each layout has a
separate 30-second cooldown. Reservations are consumed before work, even on failure
or cancellation; watcher resets do not clear them. A fully admitted batch consumes
the baseline and requires a real header change to retry. If any top candidate is
cooling down, keep the baseline available for later attempts while retaining each
layout’s 30-second cooldown. Save a first-use calibration per geometry/resolution only after two consecutive identifications agree on the page and every cell position within 1 px. Keep the first result only as a transient start for confirmation. Offsets
outside the ±12 px sparse band need Ctrl+Shift+N → Auto-calibrate or manual calibration.

Menu Load pins a manual selection until the header changes or the stash closes.
Status includes automatic score/margin. Remember/Forget references remain optional
and are not used for automatic identity. Keep the header above all cells after
patches, and keep each sub-tab rectangle clear of its tab's cells.

Tests include noise, gamma, filled cells, unpriced frames at two unique cell/probe locations per competing currency sibling,
±2px offsets at 1920×1080 and 3440×1440, ambiguous geometry, unrelated grids,
first-use offsets beyond 3px, backoff and pin transitions. Windows validation must
cover real thresholds, sparse GDI call overhead, stable-page flicker, close/reopen,
foreground/resolution changes, price-response races and manual controls. Only
a genuine change or qualifying gate causes full capture; tags may hide through
that transition's 32ms compositor delay, capture and off-thread scoring.
No screenshots or signatures may be committed; samples stay on the PC.

Set `EXILESIGHT_REAL_STASH_DIR` to the local corpus to run `RealImageTests`: all
198 first-use cases retain the original 150 checks and add all ±5 px shifts of
unsupported pages. Each runs the real sparse crops with no calibration, then the
production identifier with every admitted wide candidate explicitly permitted and
the normal currency button rule. The test records the exact number of executed
wide searches per image/shift, checks that every reservation ran, and reports the
minimum accepted score against the highest admitted wrong/null candidate. The
original calibration checks remain within 2 px at 2000 width / 3 px at 3440 width.
Each case also replays pending-transform identification and confirmation for up to
three ticks, retaining the first wide-search reservation across those ticks. All
labelled cases must confirm within that bound; unsupported cases must never confirm. With the
variable unset the test skips cleanly. No screenshots belong in this repository.
`GridTimingTests` logs Release timings (targets: 150 ms local all-eight, 600 ms wide,
excluding capture and JIT warm-up); shared CI runners do not fail on wall time.
It asserts candidate, scoring-pass and map-read counts deterministically. Candidate
caps count transforms, not scoring passes: at most two passes per candidate plus
16 final passes, each reading at most 24 cells × 12 probes × 5 offsets × 4 map values.
Two evidence maps additionally read at most nine luminances per captured pixel each,
after one luminance conversion pass. Final continuity verification rereads at most
8,712 pixels from the existing capture, with the same bounded sparse-patch loops.
Capture pixel caps and watch cadence are unchanged. Tests include 128 deterministic
dark correlated-noise scenes (clipping both before and after blur), black/mid-grey
frames at both widths, synthetic and real Atlas hover-glow rejection, and the
first-use confirmation guard. Fixtures choose band centers within measured navy valleys, offset by 0.5–1 reference
pixel from the scorer’s template on every side, and use
at least ±7 levels of luminance noise.

The new `currency/*` recognition rectangle keys change `RecognitionRegion.Fingerprint`,
invalidating earlier taught references. Automatic recognition needs no teaching;
users who use optional taught references can remember those pages again.


### Structural capture and retention (S1-R6 round 2)

The pixel-panel guard is removed. Review demonstrated both false negatives at
3440/150% with panels shifted down one pixel (General on src11: 17.35; world:
18.42) and false positives on clean 2000-px Atlas/General at ≥125%. The old
positive post-guard score gaps are not a safety guarantee at production scales.

After hiding tags, drain WPF rendering then call IScreenSampler.WaitForComposition
once per capture batch, before header/sub-tab/grid samples. Platform.Windows uses
CsWin32 DwmFlush (display-only, not input). A failure or return under 16 ms incurs
a bounded 32 ms fallback on the worker. Estimated hidden time is render drain +
flush + fallback if needed + capture: roughly 21–78 ms at 60 Hz excluding dispatcher
backlog, with estimated capture 5–30 ms. Native flush/driver stalls can be longer.
Tags return before identification scoring (historically ~100 ms). Real Windows
composition timings remain to be measured; no game input or new capture region
is introduced.

A new automatic header/sub-tab baseline is provisional. On the next available
450 ms tick after completion, capture again after composition. A matching pass
confirms it. A failed confirmation remains pending, and a second consecutive
failure hides tags. A single stale normal pass followed by fresh failed checks
therefore clears within two follow-up ticks plus processing (typically 2–3 ticks,
0.9–1.35 s). A slow gap over four seconds resets the failure count, not the need
for confirmation. Moving-world closure remains bounded to four ticks (1.8 s)
plus processing. Completion times, rather than starts, govern all failure windows.

No-leader currency hover signatures are acknowledged without adopting the normal
baseline. Matching acknowledged signatures cause no more captures or scoring;
moving away, including returning to the old baseline, restarts normal debounce.
Normal identification gates (16 score, 3 margin, button agreement) are unchanged.
Production-scale regression cases reuse ZReviewGuardTests and painted.txt from the
owner's local review folder. Clean pages are checked at 1.0/1.25/1.5/2.0 scale;
painted panels use those scales and vertical offsets −1/0/+1 px. Images stay local.

Strict partial-occlusion policy remains:

| Half-grid case | 2000 px | 3440 px |
| --- | --- | --- |
| Fragment | Hide after 2 | Keep |
| Currency General | Keep | Keep |
| Delve | Hide after 2 | Hide after 2 |
| Blight | Hide after 2 | Hide after 2 |
| Currency Atlas | Hide after 2 | Keep |
| Delirium | Keep | Hide after 2 |
| Essence | Hide after 2 | Hide after 2 |
| Currency League | Keep | Hide after 2 |
| Atlas +25 League hover | Hide after 2 | Hide after 2 |

Header-only occlusion keeps all eight pages at both widths; total-grid occlusion
hides after two failures. Passes reset the count; the next passing identification
restores hidden tags. Real hover-glow brightness needs in-game validation.

#### Round 2 verification and remaining D/F conflict

The native wait follows the [Microsoft DwmFlush contract](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/nf-dwmapi-dwmflush):
it waits for queued updates from this application, not the entire desktop session.
The application render drain precedes this wait. Windows timing is not measured here.

All 64 clean page/width/scale combinations identify correctly without the pixel
guard. The production matrix covers 2,112 painted frames: 118 transient keeps,
98 bounded normal-identification keeps, and 20 unbounded no-leader hover keeps.
Fresh unpainted captures reject P in all these cases, but the acknowledged-hover
policy never requests that capture while the signature stays unchanged.

| Width | Display scale | Unbounded painted hover cases |
| --- | --- | --- |
| 2000 | 1.25 | Atlas on src11, dy=-1 |
| 2000 | 1.5 | Atlas on src11, dy=-1; General on world, dy=0/+1 |
| 2000 | 2.0 | Atlas on src11, dy=-1; General on world, dy=0/+1 |
| 3440 | 1.5 | Atlas on src11, dy=0; General on src11 and world, dy=-1/0/+1 |
| 3440 | 2.0 | Atlas on src11, dy=-1; General on src8, dy=-1/0; General on src11, dy=-1; General on world, dy=-1/0 |

The legacy-size painted regression also exposes Atlas-on-src11 as an unbounded
hover keep. Its raw score gaps are -3.14 at 2000 and -1.55 at 3440; no single-frame
painted-score safety margin is claimed after removing the guard.

Direction D requires no more checks while an acknowledged hover signature stays
unchanged, whereas F requires every painted-frame keep to receive fresh checks
and hide within the bound. The corpus demonstrates that a painted frame can
produce precisely that acknowledged hover. Both requirements cannot hold as
written. A proposed resolution is one independent confirmation for each new hover
acknowledgement, then no more checks until its signature moves. That exception
needs an explicit decision; it has not been silently introduced.

Build, 109 regular feature tests, 129 architecture tests, 25 transparency tests
and 48 Platform.Windows tests pass (21 Windows-only tests skipped off Windows).
The full opt-in run executes all four image tests with no skips: the original
198-case replay and approved strict-occlusion cases pass; the legacy and production
painted-frame temporal acceptance tests fail on the hover cases above. No images
are committed. This round remains incomplete and is not ready to merge.
