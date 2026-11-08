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
- **Trigger:** a hotkey (or long omni press) while a supported fixed stash tab is open. The user selects the tab/page, or it is inferred by matching the copied item's name or base type to a layout cell's itemName or ninjaId. Hotkey opens the last loaded page in this app session, initially Currency General; unmatched items use that same fallback. In 1.1, D-104 permits automatic tab/page recognition from user-taught local pixel references to switch displayed price tags.
- **Actions:** `CopyItem` from the omni press. `PasteSearch` for the optional generated regex, on its own click.
- **Behaviour:**
  - Price tags drawn over the tab's fixed grid cells, sized from the client height.
  - Chaos and divine conversion, threshold color profiles, and a 7-day sparkline.
- **Screen reads (1.1, D-104):** declare "stash page recognition and grid calibration (game client area, display-only)" in the manifest. Use GDI `BitBlt` only within the game client rectangle while the game is foreground and the stash overlay is open. No DXGI capture, DirectX/Vulkan hooks or Windows.Graphics.Capture. Results only change overlay display; they never send input or obtain an `IntentToken`.
- **References (1.1):** the user teaches each page locally, with automatic capture after calibration or **Remember this page**. Store references only on the user’s machine; ship and commit no game images. Exclusive fullscreen may not capture; recommend Windowed Fullscreen. D-104 now also includes display-only grid auto-calibration.
- **Calibration:** **Auto-calibrate** searches slot-border contrast within ±3% of client height in translation and ±5% of the starting scale (saved transform or Geometry.Fit). It runs once per uncalibrated page load when foreground; the grid score checks whether the stash looks open. Tags hide for a 32 ms compositor delay plus tiled capture, then return while the bounded search runs off-thread. Require mean contrast ≥8/255, ≥75% supported border probes and a score ≥2.5 standard deviations above the coarse distribution. Save only a still-current result and teach the page as after manual calibration. On refusal, suggest manual calibration: choose two empty reference cells far apart diagonally and click their centers to fit uniform scale and translation per geometry/client resolution. Use empty cells with no item held: these observed clicks reach the game. Esc cancels and restores Highlight.
- **Data:** ExileSight-measured layouts with recorded screenshot provenance under `data/stash-layouts/poe1` (eight measured pages; no Exile-UI geometry), and poe.ninja `poe1/api/economy/exchange/current/overview?league=…&type=…`, cached for 1 h or more.
- **Reference:** `modules/stash-ninja.ahk` (grid scaling around lines 59–65, poe.ninja around 440). Its bulk-exchange POST is **not** ported (D-003).

- **Recognition behaviour:** data-authored shared header and per-tab sub-tab rectangles, fitted by client height independently of calibration; 48×6 mean-colour signatures with per-channel normalization. Stage one compares tabs (margin against other tabs only); stage two distinguishes sibling pages using the complete sub-tab row, and requires valid sub-tab references for every shipped page of that tab. An incomplete set returns Unknown and shows a teaching reminder only after the header matches that tab. Local per-resolution page references support Remember/Forget and automatic teaching after calibration. Auto-detect defaults on with taught references; foreground/menu-only 250 ms polling pauses during calibration. Two matching ticks switch tags, three unknown ticks hide them. Ambiguous matches are unknown; header/menu overlap or failed capture shows a notice without advancing the hide counter; overlapping tags from a different tab are hidden before sub-tab capture on the next tick. Overlap with the matched tab’s own tags blocks capture with a notice, retaining tags without hide/show cycling. Stable matches preserve status messages. Automatic teaching respects an explicit auto-detect off choice; Remember enables it. Version-2 references store both signatures and fingerprint both rectangles. Corrupt references show a notice and are ignored. No screenshots are stored or transmitted.

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
