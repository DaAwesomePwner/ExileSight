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
- **Actions:** `CopyItem`.
- **Behaviour:**
  - Parse the copied text and route it to the first matching handler by priority.
  - A short press opens the default handler, usually item-info. A long press (over about 200 ms) opens the context menu.
  - The context menu opens links in the browser: poewiki, poedb, Craft of Exile, and the trade site as a search URL only.
  - If nothing was copied (no item under the cursor), do nothing. In 1.0 there are no screen checks to fall back on.
- **Reference:** `modules/omni-key.ahk` (`Omni_Context` around lines 268–340, context menu around lines 342–575).
- **Done when:** routing is unit-tested with fixtures, and one key press results in at most one `CopyItem`.

### `iteminfo`: Item-info tooltip
- **Trigger:** the omni handler for gear, jewels and flasks; no separate item-info hotkey is registered.
- **Actions:** none beyond the omni copy.
- **Behaviour:**
  - A compact tooltip near the cursor showing:
    - each mod's tier and roll position within its range, color-coded
    - affix item-level requirement
    - prefix and suffix counts
    - base percentile for defences
  - Up to 5 highlight profiles (desired and undesired mods, global or per item class). Unique drop tiers remain unknown.
  - Tiers come from advanced-copy headers. Defence percentiles use quality additive with local increased defence and forward-validated integer reversal; ambiguous results remain unknown, deliberately diverging from Exile-UI.
  - Closes on mouse move beyond a threshold or on Esc.
- **Data:** the `poe1/mods`, `poe1/bases` and `poe1/stats` packs; no unique drop-tier data is loaded.
- **Reference:** `modules/item-checker.ahk` (parsing around lines 123–289, PoE1 mod matching around lines 1447–1460, drop tiers around 1819–1825), `data/global/item mods.json`, `item bases.json`, `item drop-tiers.json`.
- **Done when:** fixture tests cover every rarity and slot class, and the 🎮 gate shows tiers matching the game on 20 real items.

### `gearcmp`: Gear comparison
- **Trigger:** choose **Set as equipped** in the omni long-press menu. The tooltip stays click-through. The slot is inferred from the item class; for rings the user picks left or right.
- **Actions:** none.
- **Behaviour:**
  - Store equipped items in the default profile or an explicitly confirmed character profile. Client.txt names are in-memory candidates until confirmed in the omni menu; profile filenames are hashed.
  - The item-info tooltip for the same slot shows stat deltas: life, resistances, attributes, copied weapon DPS, and defences. Unknown stats are omitted; hybrid stats expand to components.
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
- **Trigger:** open a supported stash page while the game is foreground; prices appear without a menu, page selection or teaching. **Show stash prices automatically** defaults ON and persists as `stashninja.automaticPrices` through the feature context.
- **Menu:** Ctrl+Shift+N remains available for threshold profiles, Highlight, Auto-calibrate, two-click calibration and manual page selection. A manual Load pins its page until the header baseline changes or the stash closes; automatic mode then resumes. Status includes the automatic page, score and runner-up margin. The list follows the automatically identified page. Failed Auto-calibrate attempts retain the last good calibration and the loaded page’s tags; only the status changes. Foreground loss and page changes still hide tags normally.
- **Actions:** optional `PasteSearch` only on its own physical Highlight click; omni CopyItem remains user-triggered. Screen results never send input or obtain an IntentToken.
- **Screen reads (D-104):** foreground game-client GDI BitBlt only, declared as "automatic stash watch, page recognition and grid calibration (foreground game client, display-only)". No DXGI, DirectX/Vulkan hooks or Windows.Graphics.Capture.
- **Watch:** 450 ms dispatcher ticks, paused while disabled, unfocused, minimized or calibrating. Compare the header and (for Currency/Fragment) sub-tab strip against the identification baseline, never against the preceding tick. Changes exceeding mean normalized distance .04 trigger identification; stable pages never trigger periodic full reads or tag hiding. Unchanged-baseline missing/blocked samples require one second of consecutive Unknown before hiding. A changed-baseline Unknown hides and invalidates once immediately.
- **No-stash gate:** only header checks plus sparse border pre-checks while unidentified. Score each layout once at saved calibration or Fit, with no transform search: up to eight 33×33 patches per layout (at most 69,696 pixels across eight layouts). Each fixed border profile tolerates ±12 px normal displacement and requires ≥17/19 positive contrast samples along a line. A score ≥12.5 permits full identification. After the initial immediate check, unidentified scenes have a fixed minimum 1800 ms interval. Header motion never shortens it; opening a supported page waits at most 1.8 s for the gate, plus capture, scoring and price loading.
- **Identification:** compare all eight layouts with local ±3 px / ±0.3% scale search and refinement (≤559 evaluations per layout). Require coherent border score ≥25/255, ≥75% supported search probes and runner-up margin ≥3. Independently average neighboring tangent samples to reject isolated noise. If identification fails, run the original ±3% height / ±5% scale GridLocator search for at most two uncalibrated candidates whose local or pre-check score reaches 12.5 (≤40,000 evaluations each); require its original confidence rules too. Reserve at most one fully admitted wider-search batch per header baseline, with a separate 30-second cooldown per layout; failures/cancellation consume the reservation and watcher resets do not clear it. A fully admitted batch consumes the baseline and requires a real header change to retry. If any top candidate is cooling down, leave the baseline available so it can be tried later; all admitted layouts still incur their 30-second cooldown.
- **Calibration:** the sparse ±12 px normal band is a first-use limit; if the offset is larger, use Ctrl+Shift+N → Auto-calibrate or manual calibration. Save a successful first-use transform per layout/resolution. Manual Auto-calibrate retains the broad search. Two-click fallback: choose empty cells far apart diagonally and click their centers with no item held; clicks reach the game, Esc cancels.
- **Capture:** full reads occur only after a baseline change or qualifying sparse pre-check. Tags hide for a 32 ms compositor delay and the capture/search transition, not periodically. Sub-tab baselines are captured at identification; Currency/Fragment sub-tab tick reads keep tags visible. Header processing is about 1 ms and synthetic sparse pre-check processing about 2 ms (GDI excluded); actual Windows capture overhead and thresholds require in-game validation. Windowed Fullscreen is recommended.
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
