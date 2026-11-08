# UI spec (1.1 redesign)

Status: approved by the owner on 2026-11-08 (D-104). Tasks: [Phase 7](../ROADMAP.md#phase-7-ui-redesign-11).
Mockups and how to compare against them: [README.md](README.md).

The redesign changes how ExileSight looks, not what it is allowed to do. [FAIR-PLAY.md](../FAIR-PLAY.md)
is unchanged: no new `GameAction` kinds, no screen reading, nothing sent without a key press or click.
The only input change is that the omni key sends advanced copy by default (§3.1, D-108), which is the
existing `CopyItem` action with its existing `Advanced` flag.

## 1. Principles

1. **Don't repeat the game.** The game's item tooltip already prints every mod line. Overlays add only
   what the game hides: tiers, roll positions, affix slots, base percentiles, deltas, prices, danger.
   Never reprint full mod text on an in-game surface.
2. **Row N = line N.** Item-card mod rows follow the game tooltip's mod order, so the eye jumps sideways
   from a game line to its row instead of reading twice. (Map cards are the exception: they sort by danger.)
3. **Verdict first.** The top of every card answers one question at a glance ("worth a second look?",
   "safe to run?"). Detail comes after.
4. **Colour + shape + number.** Colour never carries meaning alone. Tier pills print the tier, bars print
   the %, deltas carry ▲/▼, danger carries pips. Use blue/orange for good/bad, not red/green.
5. **Unknown stays unknown.** A value we can't compute is shown as unknown (dashed) or left out. Never
   guess, never fill in a default that looks like data.
6. **Instant and quiet.** Overlays show and hide with no animation and no effects, take no focus, and
   stay click-through unless they have buttons.

## 2. Placement of item and map cards (D-106)

1.0 never reads the screen (D-006), so the game tooltip's rectangle is unknown. Only the cursor and the
game client rect are known. Two modes, set on the Overlay page (§7):

- **Beside the cursor (default).** Cursor right of the client's horizontal centre → card opens to the
  **right** of the cursor (the game draws its tooltip to the left). Cursor left of centre → card opens to
  the **left**. Card top aligns with the cursor; the existing 12-unit offset applies; the card is clamped
  inside the client and work area as `PopupPlacement.Place` does today.
- **Docked.** A fixed position stored as fractions of client width and height. The user sets it in the
  app by dragging a sample card on a preview rectangle with the game client's aspect ratio (overlays are
  hidden while the app has focus). Survives resolution changes.

Dismissal is unchanged (mouse moved beyond the scaled threshold, or Esc). Whether the "opposite side" rule
really avoids the game tooltip is a 🎮 gate check at 16:9, 21:9 and 16:10.

## 3. Item card

Mockups: [item-card](mockups/png/item-card.png), [item-card-states](mockups/png/item-card-states.png).

Width 300 at 1080p, scaled by `OverlayRules.Scale`. The card is click-through, so it **never scrolls**.
Sections top to bottom; a section with nothing known is dropped, not shown empty.

| Section | Content | Source |
|---|---|---|
| Name line | Base type (unique: unique name in Unique colour, base below), rarity + class, `ilvl N` | `ParsedItem` |
| Verdict band | `◆ N wanted` (teal) and `✕ N unwanted` (orange); omitted when both are 0 or no profile exists. Uniques show **Average roll** (mean roll position of mods that have a range) instead | `ModInfo.Highlight`; average is new, derived |
| Slots + base | `P` and `S` pips: filled = used, teal outline = open. Max per item: rare 3/3, magic 1/1, rare jewels 2/2; if the count or max is unknown, draw dashed pips and no "open" text. Right side: defence base percentile bar, e.g. `ES base 94th` | `ItemInfoResult.Prefixes/Suffixes`, `DefenceInfo` |
| Mod rows | One row per mod, in game order. Implicits first, separated by a hairline. See row spec below | `ModInfo` |
| Compare | One block per slot GearCmp compares, headed `vs <slot label>` (rings: left and right blocks; one-handers: each candidate slot GearCmp returns). Two-column stat deltas with ▲ (blue) / ▼ (orange) and the absolute delta. Weapons show the DPS lines GearCmp computes: Physical, Elemental and Chaos DPS (no total). A slot with nothing equipped shows "nothing equipped". Stats GearCmp can't compute are omitted. The character line and notices GearCmp emits today move to one Muted line | GearCmp |

**Height cap.** If the card would be taller than 80% of client height, drop the Compare section first,
then the Slots + base row; mod rows are never dropped. If rows alone still don't fit, show as many as fit
and a final Muted row `+N more mods`.

**Mod row** (24 high): columns 12 · 12 · * · 36 · 40 · 30 · 20, gap 5. The tier column is 36 so every
pill label fits with padding, including `ench` at the §8 label size (owner decision, 2026-11-08); all pills
share that one width (`TierPillWidth`) with the label centred.

1. Slot letter: `P` prefix, `S` suffix, `I` implicit, `?` unknown (Faint text).
2. Highlight mark: `◆` wanted (teal), `✕` unwanted (orange), else empty. An unwanted row's label is Secondary.
3. Short label (§9), single line, ellipsis if it doesn't fit. Crafted mods: label in Crafted colour.
4. Tier pill (36 wide, label centred): `T1`…`Tn` coloured by tier tone; `imp` outlined for implicits; `cr` Crafted; `U` Unique;
   `ench` outlined for enchants; `T?` when unknown (dashed `TooltipUnknown` border, Muted text).
5. Roll bar 40×6, fill width = roll position, coloured by roll tone; dashed empty track when unknown.
6. Roll %, right-aligned; `—` when unknown.
7. Required ilvl for that tier, right-aligned; blank for implicits, crafted and unique mods; `—` when unknown.

**Hybrid mods** (one mod, several game lines): one row, 40 high, a bracket left of the label and
`N game lines` under it, so row-to-line matching still holds. **Fixed unique lines** (no range): a dim row
with `·` in the pill column and `fixed` in place of the bar.

**Tones** reuse the existing calculator thresholds and `InfoColor` enum unchanged:
tier 1 = Best, tiers 2–3 = High, 4+ = Medium; roll 100% = Best, 75–99 = High, 25–74 = Medium, <25 = Low.

### 3.1 Advanced copy (D-108)

Tiers, roll ranges and tier ilvl exist only in advanced-copy text. Today the omni key sends a plain
`CopyItem()` (`OmniPress.cs`), so every omni card would be in the plain-copy state. Phase 7 makes the omni
key send `CopyItem(Advanced: true)` by default, with an **Use advanced copy** setting (Item rules page,
"Omni key" group) to switch back to plain. It is still one `CopyItem` action per press.

**States** (all in [item-card-states](mockups/png/item-card-states.png)):
- *Plain copy* (advanced copy turned off, or text without advanced headers): dashed band "Tiers and rolls
  need advanced copy. Turn it back on in Item rules › Omni key.", dashed pips and pills, empty tracks.
  Highlights still apply.
- *Unidentified*: name line plus base percentile only, with "Unidentified · mods hidden".
- *Unique*: no tiers, no slots; unique drop tier is unknown and is **not shown**.

## 4. Map card

Mockup: [map-card](mockups/png/map-card.png). Width 300. Interactive (not click-through), as today, so
it may scroll inside its height cap (80% of client height).

Ratings have three states, and the model must keep them apart: **unrated** (never clicked), **rated 0–3**
(the user's explicit choice), and **unknown** (the mod has no ID in the map data). Today `RatingStore.Get`
returns 0 for unrated IDs and `MapInfoModel` forces unknown mods to rank 3; P7-T6 changes both.

- Name line: map name; right side `T16 · rare · corrupted` (only parts that are known).
- Three stat tiles: Quantity, Rarity, Pack size, mono 20. A tile's number turns teal at or above the
  user's threshold (map card settings, defaults 70 / 40 / 25); otherwise primary text. Unknown: `?`.
- Danger band: counts of mods **rated** 3 ("deadly") and 2 ("risky") "for your ratings"; red-tinted when
  any 3s. Unknown and unrated mods are not counted. When no mod on this map has an explicit rating, show
  the dashed hint "Rate each mod once by clicking it. Every later map sorts itself." instead.
- Mod rows, in this order: rated 3, then unknown, then rated 2, rated 1, unrated, then rated 0; game order
  within each group. Columns: pips (34) · area (78) · short text. Rated pips are filled in the danger
  colour; unrated pips are dashed outlines; unknown mods show an orange `?` with "Not in map data · check in
  game" and aren't clickable. Rank 3 rows get a red tint.
- Only mods **rated 0** fold, into one row "+N more · rated 0 · click to show". Unrated mods always show.
- Clicking a row cycles its rating (unrated → 1 → 2 → 3 → 0 → 1 …); the existing store and limits stay.
- Footer hint: "Click a mod to cycle 0–3 · rating saved for every map". Existing notices go above it.

## 5. Trade

Mockup: [trade](mockups/png/trade.png).

**Shop pricer** (vaalasync), width 330. Name line plus `armed` (teal) / `disarmed` (muted). Two tiles:
**Listed now** (current amount + currency) and **Listed for** (time since the listing was first recorded;
omit if history has no timestamp). Four 56-high amount buttons: −10%, −1, +1, +10%, each showing the
**whole-number amount** large and the adjustment small. Duplicate amounts stay disabled (existing rule).
Then **Custom amount…** and one line: "All amounts in <currency>. A click pastes the number into the
amount box you opened. You pick the currency and press List." After a click: header `Pasted N`, all
buttons disabled, "Omni-key the item again to re-arm." Note mode keeps the same layout with the note text
in the buttons.

**Stash price tags** (stashninja; still disabled until stash layouts are measured): a tag in each priced
cell's **bottom-right** corner, mono 10, 14 high, solid background so it reads over item art. Text is the
unit price: chaos with no suffix below 1 divine, `1.4d` at or above. Colours keep the existing
`ThresholdProfile`: `HighColor` at or above its `High` value, otherwise `LowColor`; `Minimum` keeps driving
the search highlight. These are user-chosen colours, the one place a view may use non-token colours; new
profiles default to `TooltipBest` and `TooltipHigh`. Tag text is black or white, whichever contrasts more
with the background. Prices are **per unit**: stack counts are unknown, so there is no tab total. A summary
card **outside** the tab rectangle lists the three highest unit prices in the tab with a 7-day sparkline
and the price age.

## 6. Ambient and on-demand surfaces

Mockup: [ambient-hud](mockups/png/ambient-hud.png). All click-through unless noted.

- **Act guide**, width 340: `Act N · step i of n`, act and total timers right-aligned, a 3-high progress
  bar, the current page's lines in 15/600, then `NEXT` + the next page's first line in Muted. Guide notices
  (timing uncertain, guide complete, store notices) get one Muted line; league-start and bandit settings
  leave the overlay (they are already in the guide's settings). Needs a structured guide snapshot (P7-T9).
- **Timers** (Tab held), width 300: name, a 4-high bar, mono time. Under one minute turns orange; that is
  the only colour change. Stopwatch has no bar.
- **Event toast**, existing `DefaultOverlayLayout.EventsToast` position and existing queue behaviour (one
  toast for 5 s, up to four pending): a 4-wide teal strip, the event's `DisplayName` in bold, then a Muted
  line with its category. No key hints: bindings can change, and muting is done in event settings.
- **Omni long-press menu**, width 260, interactive: item name header, in-app actions first ("Set as
  equipped ›", "Set listing price…"), a hairline, then browser links marked `↗`. Number-key selection is
  backlog: omit the key badges until it exists.
- **Chat wheel**, centred on the cursor: 8 pill slots on a 200 circle, centre shows the key and
  "release"; the slot under the pointer direction is highlighted teal.
- **Search strings**, width 320, interactive: vendor chips (selected = teal), then the string list with
  the string itself in mono Faint on the right; multi-line entries show `wheel ↕ i/n`.

## 7. Desktop app

Mockups: [app-home](mockups/png/app-home.png), [app-item-rules](mockups/png/app-item-rules.png),
[app-overlay-settings](mockups/png/app-overlay-settings.png).

One main window (normal activating WPF window, not an overlay) with a left nav: Home, Item rules,
Map log, Sales, Overlay, Hotkeys, Transparency. It replaces the debug panel as `MainWindow`; tray
left-click opens it, and the tray menu keeps Debug panel, Run setup again, Open settings folder and Exit.
Map log and Sales open the existing `TrackerWindow` and `HistoryView` until they are rebuilt as pages
(backlog). The window is resizable; below about 760 px wide the nav stacks above the content.

- **Home**: title "Ready to play" (or the first problem, e.g. "Game not found"), league picker, four
  status tiles (Game, Client.txt, Prices, Game data) each with a teal or orange dot, one bold line and one
  muted detail. "Your keys": the user's current bindings as keycaps with their names from the trigger-name table (below). "Last session":
  the most recent run of map-tracker runs with no gap over one hour between them: maps run, average map
  time, deaths, and divine **marked sold** in that window (sales are only ever marked by hand), plus a bar
  per map (orange = a death that map). Footer: the GGG disclaimer (required by FAIR-PLAY.md) and a link to
  Transparency. Status and session mapping live in WPF-free, tested code.
- **Hotkeys**: a read-only list of current bindings (the same data and names as Home's "Your keys"). Editing
  bindings in the app is backlog; until then it says where bindings are set today.
- **Item rules**: five profile tabs (the existing `ItemInfoSettings` limit), the active one marked.
  A search box adds a rule by mod text taken from the stats pack translations; the stored rule text must
  normalize, through the same `ModRuleKey` rules, to what real item lines normalize to (templates carry
  `#` and `{0}` placeholders that item lines don't), proven by a test against fixture items. Table: mod
  text, applies to, Want/Avoid segmented control, remove. "Applies to" with several classes is stored as one
  `HighlightRule` per class. Right: a live preview card built from the last copied item (in memory only),
  "Card shows" toggles (compare, defence base percentile, ilvl column, implicits), and the "Omni key"
  group with **Use advanced copy** (§3.1).
- **Overlay**: placement mode (§2) as two option cards; **Overlay size**: Auto (no `ScaleOverride`, the
  current client-height scaling) or a fixed scale from 0.8 to 1.5 written to `ScaleOverride`, which, as
  today, applies to every overlay; density (Compact / Comfortable: row 24 vs 28); colours (Standard / High
  contrast: Secondary text → Primary, Muted → Secondary, borders +1 step); map card thresholds (§4).
- **Transparency**: "Everything ExileSight sent to the game", the last 200 lines of `actions.log`
  newest first: time, trigger, action kind, result (teal Sent / orange refusal reason). The log records
  only `timestamp, hotkey, action, outcome, reason, verb`, where `hotkey` is the binding ID or, for overlay
  buttons, the feature command ID. The Trigger column shows that ID's name from the shared **trigger-name
  table**: a WPF-free map from every binding ID and feature command ID to a short display name (e.g.
  "Omni key", "Shop price button"). Bindings and commands carry no names today, so P7-T12 creates this
  table and Home, Hotkeys and Transparency all use it; an ID missing from it shows raw. It never shows key names (a rebind would make them wrong), item text, amounts or search
  strings, and the page adds no fields to the log. "Open actions.log" button. Below: what is read, which
  hosts are contacted, what is never touched.

## 8. Tokens

Mockup: [tokens](mockups/png/tokens.png). Implement as keys in `OverlayTheme.xaml` (overlays) and an app
theme dictionary (desktop window). The six existing tone keys (`TooltipUnknown`, `TooltipLow`,
`TooltipMedium`, `TooltipHigh`, `TooltipCrafted`, `TooltipUnique`) keep their names; only colours change.
`TooltipBest` is new and equals `OverlayAccent` (the view maps `Best` to `OverlayAccent` today).

| Key | Value | Use |
|---|---|---|
| `TooltipBest` (new) / `OverlayAccent` | #6FE3D5 | Best tier/roll, wanted, good numbers |
| `TooltipHigh` | #6AA8FF | High tier/roll, ▲ better |
| `TooltipMedium` | #AEB6C2 | Medium tier/roll |
| `TooltipLow` | #F2994A | Low roll, ▼ worse, unwanted, warnings |
| `TooltipCrafted` | #C3A6FF | Crafted mods |
| `TooltipUnique` | #E2B97F | Unique names and pills |
| `TooltipUnknown` | #5C6573 | Dashed borders and tracks only, never text (3.2:1) |
| `OverlayBackground` | #0F1217 at 96% | Card surface |
| `OverlayRaised` | #17202A | Verdict band, stat tiles (#161A21) |
| `OverlayBorder` | #2E3540 | Card border, 1 px |
| `OverlayRule` | #262C35 | Hairlines; bar track #2A313C |
| `OverlayForeground` | #E8EAED | Primary text (15.6:1) |
| `TextSecondary` / `TextMuted` / `TextFaint` | #B7BEC8 / #8E97A4 / #7D8592 | 10.0 / 6.4 / 5.0:1 on the card |
| `Danger1` / `Danger2` / `Danger3` | #E6CC5A / #F2994A / #FF6B5E | Map pips; rank-3 row tint #221616 |
| `Accent` | #6FD1C6 | Interactive: selection, focus, links |
| `AppBackground` / `AppPanel` / `AppNav` | #0F1216 / #14181E / #0C0F12 | Desktop window |

Contrast ratios are against the card colour #0F1217. `TextFaint` drops to 4.4:1 on `OverlayRaised`, so use
Faint only on the card surface; text on raised bands is Muted or brighter. Some mockup labels use #6B7480
(4.0:1); ship them as `TextFaint`. Type: Segoe UI Variable (fallback Segoe UI) for text, Cascadia Mono
(fallback Consolas) for numbers, with `Typography.NumeralAlignment="Tabular"`. Overlay: title 13/600, body
13, labels 11/600 caps +12% tracking, numbers 11–12. App: title 24/700, section 18/700, body 14. Spacing on
a 4 px grid; card padding 10 top, 12 sides; radius 6 (cards), 3 (pills), 4 (bands).

## 9. New data and settings

| Item | Owner task | Notes |
|---|---|---|
| Omni advanced copy + setting | P7-T15 | `CopyItem(Advanced: true)` by default; setting in Omni feature settings with `Load`/`Save` helpers |
| Short mod labels | P7-T3 | Keyed by stat ID; ExileSight's own wording, ≤ 14 characters; fall back to the mod template with numbers removed, truncated |
| Item card model (slots, average roll, row kinds, hybrid line span) | P7-T5 | Built from `ItemInfoResult` and GearCmp; WPF-free; added alongside the existing text sections |
| Placement mode, docked position, density, contrast | P7-T4 | `OverlaySettings`, validated and migrated |
| Card-shows toggles | P7-T5 | `ItemInfoSettings` |
| Map rating states (unrated / rated / unknown) and card thresholds | P7-T6 | MapInfo `RatingStore`, `MapInfoModel` and feature settings |
| Structured guide snapshot | P7-T9 | Replaces the preformatted string the guide overlay reads |
| Item name in the omni menu presentation | P7-T10 | `OmniMenuPresentation` |
| Listed-for duration | P7-T7 | Only if shop history has the first-listed time |
| Highest unit prices in a tab | P7-T8 | From prices already fetched; no new requests; no tab total |
| Home status and last session | P7-T12 | WPF-free mapping from existing services and logs |
| Trigger-name table | P7-T12 | WPF-free ID → display name for every binding and feature command; a test fails when a feature registers one without a name; reused by P7-T14 |
| Rule text from stat templates | P7-T13 | Normalizes like item lines; tested against fixtures |
| Transparency reader | P7-T14 | Read-only parser for the existing `actions.log` JSON lines |

No new network hosts, no new `GameAction` kinds, no new hotkeys.

## 10. Implementation rules (speed and testability)

The idle budget in [PERFORMANCE.md](../PERFORMANCE.md) is unchanged (CPU < 1%, private memory < 200 MB),
and first-item latency must not regress.

- No `DropShadowEffect`, `BlurEffect` or other effects on overlay windows; layered windows render them in software.
- No animations, transitions or storyboards on in-game surfaces.
- Build and `Freeze()` brushes once from the theme; don't allocate brushes per row.
- Bars are `Rectangle`/`Border` with a width; not `ProgressBar` or templated controls.
- Keep one window per surface and rebuild its children (current pattern); never create windows per show.
- Card primitives are plain code-built elements shared by every surface; no third-party UI packages.
- **Split each view into a window host and a pure builder** (`model + theme → FrameworkElement`) that
  takes no live services, so the snapshot harness (P7-T2) can render it from fixture models.

## 11. To check in game (🎮 Phase 7 gate)

- The beside-cursor rule never covers the game tooltip, over inventory and over stash, at 16:9, 21:9, 16:10.
- With advanced copy on, the omni key copies tier headers, and pressing it never leaves Alt stuck or
  triggers the game's Alt behaviour in a way that disrupts play.
- Advanced-copy mod order matches the order the game displays (row N = line N) on 20 real items.
- Tier, roll and ilvl on the card match the game's advanced tooltip on the same 20 items.
- Stash tags in the bottom-right corner never hide stack sizes.
- Idle CPU/memory and first-item latency stay within budget on the release build.

## 12. Review checklist for UI pull requests

- [ ] Compared against the mockup PNG for every state the task covers; differences are listed in the report.
- [ ] No in-game surface reprints mod text the game already shows.
- [ ] Every colour has a number, symbol or pip next to it.
- [ ] Unknown values render as unknown or are omitted; nothing is guessed.
- [ ] Only token keys from §8 are used in views (stash threshold colours excepted, §5).
- [ ] No effects, animations or per-row brush allocation on overlays.
- [ ] No new `GameAction` kind, hotkey, network host, or screen read.
