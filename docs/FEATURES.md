# 1.0 feature specs (PoE1)

Each spec lists:
- **Trigger:** what the user does to start it
- **Actions:** the `GameAction`s it may use (see FAIR-PLAY.md)
- **Data:** what it reads
- **Reference:** the matching Exile-UI code. The repository is on GitHub at Lailloken/Exile-UI; a local checkout on the owner's machine is at `/Users/bgomez/Documents/GitHub/Exile-UI`. Read it for behaviour and edge cases; never copy AHK structure.
- **Done when:** the acceptance criteria

Port behaviour, not code. Where Exile-UI changes the game window, chains inputs, or polls the screen, follow FAIR-PLAY.md instead.

---

### `omni`: Omni-key + context menu
- **Trigger:** the omni-key (default CapsLock, which is blocked so the game never sees it) over an item.
- **Actions:** one `CopyItem` per press. Uses advanced copy (Ctrl+Alt+C) by default. Turn off **Use advanced copy** in Item rules › Omni key for plain copy.
- **Behaviour:**
  - Parse the copied text and route it to the first matching handler by priority.
  - A short press opens the default handler, usually item-info. A long press (over about 200 ms) opens the context menu.
  - The context menu opens links in the browser: poewiki, poedb, Craft of Exile, and the trade site as a search URL only.
  - If nothing was copied (no item under the cursor), do nothing. In 1.0 there are no screen checks to fall back on.
- **Reference:** `modules/omni-key.ahk` (`Omni_Context` around lines 268–340, context menu around lines 342–575).
- **Done when:** routing is unit-tested with fixtures, and one key press results in at most one `CopyItem`.

### `iteminfo`: Item card
- **Trigger:** the omni handler for gear, jewels and flasks; no separate item-info hotkey is registered.
- **Actions:** none beyond the omni copy, which uses advanced copy (Ctrl+Alt+C) by default. **Use advanced copy** switches it back to plain copy.
- **Behaviour:**
  - A click-through 300-unit card beside the cursor (or docked), showing:
    - verdict counts, short mod labels in game order, tier pills and roll bars with numeric values
    - affix item-level requirement
    - prefix and suffix pips, including open slots
    - base percentile for defences
  - Height is capped at 80% of the client: Compare drops first, then Slots/base, then trailing mods become “+N more mods”. No scrolling. Density and contrast follow Overlay settings.
  - Plain copies show an advanced-copy hint; unidentified items hide mods; uniques show average roll and omit unknown drop tiers.
  - Up to 5 highlight profiles (desired and undesired mods, global or per item class). Unique drop tiers remain unknown.
  - Tiers come from advanced-copy headers. Defence percentiles use quality additive with local increased defence and forward-validated integer reversal; ambiguous results remain unknown, deliberately diverging from Exile-UI.
  - Closes on mouse move beyond a threshold or on Esc.
- **Data:** the `poe1/mods`, `poe1/bases` and `poe1/stats` packs; no unique drop-tier data is loaded.
- **Reference:** `modules/item-checker.ahk` (parsing around lines 123–289, PoE1 mod matching around lines 1447–1460, drop tiers around 1819–1825), `data/global/item mods.json`, `item bases.json`, `item drop-tiers.json`.
- **Done when:** fixture tests cover every rarity and slot class, and the 🎮 gate shows tiers matching the game on 20 real items.

### `gearcmp`: Gear comparison
- **Trigger:** choose **Set as equipped** in the omni long-press menu. The card stays click-through. The slot is inferred from the item class; for rings the user picks left or right.
- **Actions:** none.
- **Behaviour:**
  - Store equipped items in the default profile or an explicitly confirmed character profile. Client.txt names are in-memory candidates until confirmed in the omni menu; profile filenames are hashed.
  - The item card for the same slot shows stat deltas: life, resistances, attributes, copied weapon DPS, and defences. Unknown stats are omitted; hybrid stats expand to components.
  - Preview and equip use the same replacement rule; bows retain quivers, and quivers pair only with bows.
- **Reference:** `modules/item-checker.ahk` (`Iteminfo_GearParse` around lines 2354–2613, comparison around lines 865–1100). Exile-UI's inventory-pixel button placement is **not** ported (D-006).
- **Done when:** delta maths is unit-tested and the stored gear survives a restart.

### `guide`: Act-tracker guide
- **Trigger:** passive, driven by `AreaEntered` and `CharacterLevelUp` log events. Ctrl+Shift+F10 toggles the panel; Ctrl+Shift+F11 goes back and Ctrl+Shift+F12 moves forward manually.
- **Actions:** none.
- **Behaviour:**
  - A small panel showing the current step and the next one, advanced by zone changes.
  - Per-act and total timers with CSV export.
  - Supports importing a custom guide from JSON.
- **Data:** the `poe1/guide` pack: Exile-UI's `data/english/[leveltracker] default guide.json` at `67d1a3637e97b791072129f0cf890e22f385b237` (Exile-UI and exile-leveling MIT notices), joined with RePoE `world_areas.json` and `gems.json` at `d0bc438357d849bb93ad22f3db0b12938aaf6da1` (GGG game data). Quest rewards only; generated in CI, never committed.
- **Reference:** `modules/leveling tracker.ahk` (`Leveltracker_Progress` around line 3160, timer around lines 3580–3697).
- **Done when:** a recorded Acts 1–3 log replays through the guide in a unit test, and the 🎮 gate passes.

### `mapinfo`: Map-info panel
- **Trigger:** the omni handler for maps and logbooks. Optional `mapinfo:hold` shows the last copied map while held; it has no default binding because timers owns Tab.
- **Actions:** none beyond the omni copy.
- **Behaviour:**
  - Map mods are shortened and ranked by the user's danger rating (4 levels, set per mod with a click).
  - Quantity, rarity and pack size are highlighted.
- **Data:** the `poe1/mapmods` pack, converted from Exile-UI `data/english/map-info.txt` at `67d1a3637e97b791072129f0cf890e22f385b237` (MIT patterns/labels, GGG game text); stable IDs, no user ratings; generated in CI, never committed.
- **Reference:** `modules/map-info.ahk`.
- **Done when:** every map fixture parses into known mod IDs and ratings persist.

### `maptracker`: Map tracker + log viewer
- **Trigger:** passive, driven by log events (area generated with seed and level, area entered, deaths, NPC dialogue for mechanics).
- **Actions:** `SendChatCommand("/kills")`, once per Ctrl+Shift+K press and never automatically. Ctrl+Shift+M opens the viewer.
- **Behaviour:**
  - Records each run with map name, tier, duration (time spent in hideout excluded), deaths, mechanics, map mods (if the map was copied before the run) and notes.
  - A log viewer with filters, totals and CSV export.
  - Storage: one JSON-lines file per league.
  - The league picker uses the shared cached poe.ninja index-state request (at least 1 h between requests, D-071), with the current softcore challenge league first. Offline or stale results show the saved league plus Standard/Hardcore with a note. The saved league stays selected; names are de-duplicated case-insensitively, preserving poe.ninja spelling. Type SSF/Ruthless names manually; poe.ninja does not list them.
- **Reference:** `modules/map tracker.ahk`, `modules/client log.ahk` (deaths around line 534). Current league choices come from poe.ninja, replacing Exile-UI's hard-coded list (around lines 63–64).
- **Done when:** a recorded mapping log replays into the expected runs in a unit test.

### `events`: Map-event notifications
- **Trigger:** passive log events: infamous mercenary, discoverable hideout, and similar. Ctrl+Shift+E opens settings.
- **Actions:** none.
- **Behaviour:** a short toast overlay, which can be turned off per event.
- **Data:** the `poe1/events` pack, from `data/english/mercenaries.json` and the patterns in `client log.ahk` around lines 578–592.
- **Reference:** `modules/qol tools.ahk` around lines 706–770.

### `stashninja`: Stash-ninja price tags
- **Trigger:** press Ctrl+Shift+P on a supported stash page to show price tags and the summary for this visit. Rebind it on the Hotkeys page. Press again to hide them. Closing the stash, losing foreground or an unknown page ends the visit. A press outside a recognized stash shows nothing and stops. No watcher ticks, captures or stash price refreshes run before the press. **Show stash prices automatically** is an opt-in saved as `stashninja.automaticPrices`. Missing settings now mean off; an explicitly saved true keeps automatic watching. In automatic mode the hotkey pauses watching until another press or foreground change.
- **Menu:** Ctrl+Shift+N remains available for threshold profiles, Highlight, Auto-calibrate, two-click calibration and manual page selection. A manual Load pins its page until the header baseline changes or the stash closes; automatic mode then resumes. Status includes the automatic page, score and runner-up margin. The list follows the automatically identified page. Failed Auto-calibrate attempts retain the last good calibration and the loaded page’s tags; only the status changes. Foreground loss and page changes still hide tags normally.
- **Actions:** optional `PasteSearch` only on its own physical Highlight click; omni CopyItem remains user-triggered. Screen results never send input or obtain an IntentToken.
- **Screen reads (D-104):** foreground game-client GDI BitBlt only, declared as "automatic stash watch, page recognition and grid calibration (foreground game client, display-only)". No DXGI, DirectX/Vulkan hooks or Windows.Graphics.Capture.
- **Watch:** 450 ms ticks with one stable tick or a two-unstable-tick cap before checking. Closed moving-world scenes hide after two failures within four ticks (~1.8 s plus processing). Every newly adopted automatic header/sub-tab baseline or hover acknowledgement is provisional: the next available tick after check completion captures again, even if signatures are unchanged. A matching pass confirms it when there has been no failed confirmation; a failure keeps confirmation pending and a second consecutive failure hides tags. A stale normal or hover pass followed by two fresh failures therefore clears within two follow-up ticks plus processing (typically 2–3 ticks, ~0.9–1.35 s). Four-second failure windows use completion timestamps, including exceptions/menu overlap; passes and longer gaps reset counts, but an unconfirmed baseline never becomes trusted merely by waiting. With no failed confirmation, a new no-leader currency hover costs one independent confirmation (one extra short blink). Without a prior failed confirmation, a matching normal or hover pass confirms the signature, then matching ticks do no capture or scoring. Moving away, even back to the trusted baseline, resumes debounce. The scheduling bound after a stale keep is 0.9 s plus two check durations; including initial detection, it is 1.8 s plus three check durations (~2.4 s estimated). These bounds assume fresh consecutive failures within the four-second window; OS/driver stalls have no hard wall-clock bound.
- **Confirmation recovery:** After any failed confirmation, the provisional baseline or hover acknowledgement needs two consecutive matching passing confirmations from separate composed captures before trust; another failure resets that streak. Failure-free pages still need only one extra check. Two stale passing captures with no intervening failure can still establish wrong trust: this is an accepted residual limit of composition-based freshness and needs in-game validation.
- **No-stash gate:** only header checks plus sparse border pre-checks while unidentified. Score each layout once at saved calibration or Fit, with no transform search: up to eight 33×33 patches per layout (at most 69,696 pixels across eight layouts). Each fixed border profile scans up to ±12 px normal displacement (scaled flanks reduce this range at larger client scales) and requires ≥18/19 samples along a line to have normalized valley evidence >12 and absolute contrast ≥6 luminance. Weight line evidence by clamp((mean flank luminance − 8)/20, 0, 1) to suppress dark speckle. A score ≥12.5 permits full identification. After the initial immediate check, unidentified scenes have a fixed minimum 1800 ms interval. Header motion never shortens it. An already-calibrated page reopening from an unidentified scene normally waits up to one 1.8 s admission interval; a change from an identified page is noticed on the 450 ms header/sub-tab tick. Capture, scoring and price loading add latency.
- **Identification:** compare all eight layouts with local ±3 px / ±0.3% scale search and refinement (≤559 evaluations per layout). Require normalized inner-band score ≥16, ≥65% supported cells (cell evidence >6; all cells for layouts smaller than eight) and runner-up margin ≥3. Score near/far bands jointly against their two flanks, trim the strongest quarter of cells, and finish with a sub-pixel joint fit without per-cell offsets. Valleys below 4 luminance depth contribute zero; normalize by 16 plus the mean flank luminance. Layouts with fewer than eight sampled cells receive a score multiplier of .6 + count/20 (four cells: .8), plus a 1.5-point penalty per 1% scale drift beyond 0.5% from the start on these small layouts. The locator also verifies continuous-band evidence at the recovered transform using the sparse profile, and requires joint score ≥14, aligned score ≥12 and wide-search confidence ≥2.5. Only when currency wins the grid comparison, its best sibling must lead the other currency grids by ≥3 and agree with the selected button, whose luminance must exceed the other two by ≥6; button rectangles come from recognition.json, are read from the grid capture and follow the recovered transform. No teaching is needed. Candidate counts measure transforms: each invokes up to two scoring passes, plus at most 16 final passes per layout; deterministic tests bound passes and map reads separately. The final continuity check rereads at most 8,712 pixels from the existing capture. If identification fails, rank qualifying candidates by grid score plus one tenth of their sparse score; at most two qualifying uncalibrated candidates get the bounded wider search (≤40,000 evaluations each). An automatic wide-found identity must additionally score ≥18, stay within 1% of Fit scale, and beat every other layout’s original local score by ≥3. Reserve at most one fully admitted wider-search batch per header baseline, with a separate 30-second cooldown per layout; failures/cancellation consume the reservation and watcher resets do not clear it. A fully admitted batch consumes the baseline and requires a real header change to retry. If any top candidate is cooling down, leave the baseline available so it can be tried later; all admitted layouts still incur their 30-second cooldown.
- **Calibration:** the sparse ±12 px normal band is a first-use limit; if the offset is larger, use Ctrl+Shift+N → Auto-calibrate or manual calibration. Save a first-use transform per layout/resolution only after two consecutive identification ticks agree on the page and every cell position within 1 px. Budget two admission intervals (~3.6 s) for first use, or three (~5.4 s) if the fit needs another identification tick, plus capture/scoring/price loading. Immediate initial or changed-baseline checks can be faster; failed samples and wide-search cooldowns can extend the delay. The first result is a transient search start; missing evidence, page/resolution changes or disable/foreground loss reset confirmation. Entering confirmation invalidates the previous page’s load and Highlight once while retaining the pending fit. Manual Auto-calibrate retains the broad search. Two-click fallback: choose empty cells far apart diagonally and click their centers with no item held; clicks reach the game, Esc cancels.
- **Capture:** hide tags, drain WPF rendering through ContextIdle, then invoke IScreenSampler.WaitForComposition once before all header/sub-tab/grid samples (never per tile). Platform.Windows calls CsWin32 DwmFlush from the capture worker. Failure or return in under 16 ms adds a bounded 32 ms fallback delay. This is display-only synchronization, not input. Hidden time is dispatcher render drain + DwmFlush + optional fallback + capture; at 60 Hz estimate ~21–78 ms excluding dispatcher backlog (capture estimate 5–30 ms). The caller waits at most 100 ms for the composition worker, then uses the 32 ms fallback on timeout. At most one outstanding worker is retained per watcher, so repeated timeouts cannot accumulate blocked workers. Reusing an older outstanding flush also requires the fallback. The composition wait budget is 132 ms plus capture (estimated 137–162 ms with 5–30 ms capture), excluding dispatcher/scheduler backlog; tags return before scoring. A hung DwmFlush no longer holds the caller indefinitely. Restore tags before scoring, historically ~100 ms; switch a confident Q immediately after scoring. The scale-sensitive TagCaptureGuard is removed. Fresh independent confirmation, rather than pixel-panel classification, bounds a stale normal identification or hover keep. Production-scale painted-frame regressions use 1.0, 1.25, 1.5 and 2.0 with vertical offsets −1/0/+1 px. Strict partial-occlusion/+25-hover decisions are unchanged: two failures may hide tags; the next passing identification restores them.
- **Privacy and references:** no screenshots are saved or transmitted, and no game images ship. Optional Remember/Forget controls and local references remain compatible, but automatic identification does not consult them or require teaching.
- **Data:** eight measured pages under `data/stash-layouts/poe1`; poe.ninja prices cached for at least one hour, chaos/divine conversion, threshold colors and seven-day sparklines. Tags represent item types, not quantities or ownership.
- **Reference:** Exile-UI `modules/stash-ninja.ahk` (height scaling and prices); its bulk-exchange POST is not ported (D-003).

### `vaalasync`: Shop repricing + sales log
- **Trigger:** a short omni press over an item with a `~b/o` or `~price` note opens the price buttons. For an unpriced item, choose **Set listing price…** in the omni hold menu.
- **Actions:** `CopyItem` (omni), then one action per price-button click: `SetListingAmount(amount)` in default async mode, or `SetPriceNote(text)` in note mode. The user opens the game dialog themselves.
- **Behaviour:**
  - Async mode targets the amount field in **Set Item Price**, whose current value is pre-selected. Each click sends Ctrl+A, Ctrl+V of the amount only, with no Enter or clicks. The player chooses the currency and clicks **LIST ITEM**. Buttons show amount + currency; a display-only reminder appears when the suggested currency differs.
  - Async amounts are positive whole numbers only, because the game's Set Item Price amount box accepts only whole numbers (confirmed in game). Suggestions round to whole numbers (midpoints away from zero, minimum 1); duplicate adjustment amounts are disabled. Custom amounts must already be whole numbers.
  - Set `features.sections.vaalasync.listingMode` to `"note"` for legacy note mode, applied after restart (`"async"` is the default). Each click pastes the full note with Ctrl+A, Ctrl+V, Enter into the note box the user opened; decimals remain allowed.
  - Buttons offer ±1, ±10% and custom prices from the current note. Unpriced items enable only a custom amount, seeded from a poe.ninja estimate when known; the estimate never becomes an invented current price. Display and paste use invariant amounts, without thousands separators and with a dot for decimals.
  - After a send or uncertain outcome, buttons disarm until the item is read again with omni. Only explicit pre-input gate refusals leave buttons armed; double clicks are suppressed.
  - Each listing and price change is saved in local history (500 listings × 50 prices, at most 8 MB). Corrupt files are quarantined and buyer names are never stored. "Sold" is marked by hand; trade whisper matches are display-only evidence and never mark a sale.
  - A sales and purchase log viewer converts prices with poe.ninja rates.
- **Reference:** `modules/exchange.ahk` (async trade around lines 760–860). Exile-UI's automatic right-click is **not** ported (FAIR-PLAY.md).

### `macros`: Chat macros + chat wheel
- **Trigger:**
  - Fast-travel key (default F5): a short press goes to `/hideout`; a long press opens a radial wheel of destinations.
  - Macro key (default F6): a short press sends command 0; a long press opens an 8-slot wheel.
- **Actions:** `SendChatCommand` once per press or per wheel selection.
- **Reference:** `modules/macros.ahk`, radial menu in `modules/GUI.ahk` around line 578.

### `searchstr`: Search-strings menu
- **Trigger:** a hotkey (default F9) opens a menu.
  1. Pick the vendor or UI by hand (1.0 has no screen detection).
  2. Pick a saved string. The mouse wheel cycles through multi-line strings.
- **Actions:** `PasteSearch(text)` once per selection.
- **Behaviour:** user-editable string lists per vendor, plus built-in PoE1 defaults.
- **Reference:** `modules/search-strings.ahk`.

### `timers`: Timers
- **Behaviour:** named countdowns and a stopwatch that persist across restarts, shown in a small panel while Tab is held, with an optional alert when a countdown ends.
- **Actions:** none.
- **Reference:** `modules/qol tools.ahk` around lines 88–380.

### `lab` — imported daily Labyrinth
- **Trigger:** Ctrl+Shift+L toggles the panel; import/open actions require a click.
- **Actions:** none; passive display and Client.txt room tracking only.
- **Behaviour:** select difficulty, import a compass JSON, inspect the coordinate map and contents, Izaro weapon/phases/traps and tracking state. Adjacent rooms resolve repeated names. Non-adjacent or ambiguous lab room names retain the last position with an uncertainty warning. An area absent from the layout ends tracking. First room in file order is the entrance. Dates differing from the current UTC day are marked outdated (assumed reset 00:00 UTC).
- **Data:** latest user import per difficulty in `poe1/lab/{difficulty}.json`; most recently imported difficulty in `poe1/lab/latest.txt`; 1 MB cap; invalid imports preserve the previous map. Hand-authored trial locations/tips in `data/lab/info.txt`; update procedure in `data/lab/README.md`. No HTTP hosts.
- **Reference:** [poelab](https://www.poelab.com/) opens in the user's browser only; [LabCompass format](https://github.com/yznpku/LabCompass/blob/master/common/resources/map.schema.json) consulted for field semantics only. No upstream code or data included.
- **Done when:** file/paste imports display the map; duplicate-name tracking, invalid imports, storage and UTC boundary tests pass; Windows import interaction is checked. Optional shortest-path hint is deferred.
