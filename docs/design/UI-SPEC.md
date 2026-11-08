# UI spec (1.1 redesign)

Status: approved by the owner on 2026-11-08 (D-104). Tasks: [Phase 7](../ROADMAP.md#phase-7-ui-redesign-11).
Mockups and how to compare against them: [README.md](README.md).

The redesign changes how ExileSight looks, not what it is allowed to do. [FAIR-PLAY.md](../FAIR-PLAY.md)
is unchanged: no new `GameAction`s, no screen reading, nothing sent without a key press or click.

## 1. Principles

1. **Don't repeat the game.** The game's item tooltip already prints every mod line. Overlays add only
   what the game hides: tiers, roll positions, affix slots, base percentiles, deltas, prices, danger.
   Never reprint full mod text on an in-game surface.
2. **Row N = line N.** Item-card mod rows follow the game tooltip's mod order, so the eye jumps sideways
   from a game line to its row instead of reading twice. (Map cards are the exception: they sort by danger.)
3. **Verdict first.** The top of every card answers one question at a glance ("worth a second look?",
   "safe to run?", "what's this tab worth?"). Detail comes after.
4. **Colour + shape + number.** Colour never carries meaning alone. Tier pills print the tier, bars print
   the %, deltas carry ▲/▼, danger carries pips. Use blue/orange for good/bad, not red/green.
5. **Unknown stays unknown.** A value we can't compute is shown as unknown (dashed) or left out. Never
   guess, never fill in a default that looks like data.
6. **Instant and quiet.** Overlays show and hide with no animation and no effects, take no focus, and
   stay click-through unless they have buttons.

## 2. Placement of item and map cards (D-106)

1.0 never reads the screen (D-006), so the game tooltip's rectangle is unknown. Only the cursor and the
game client rect are known. Two modes, set in Overlay settings:

- **Beside the cursor (default).** Cursor right of the client's horizontal centre → card opens to the
  **right** of the cursor (the game draws its tooltip to the left). Cursor left of centre → card opens to
  the **left**. Card top aligns with the cursor; the existing 12-unit offset applies; the card is clamped
  inside the client and work area as `PopupPlacement.Place` does today.
- **Docked.** A fixed position stored in client-relative units (fractions of client width and height), set
  by dragging a sample card in the app. Survives resolution changes.

Max card height is 60% of client height; longer content scrolls inside the card. Dismissal is unchanged
(mouse moved beyond the scaled threshold, or Esc). Whether the "opposite side" rule really avoids the
game tooltip is a 🎮 gate check at 16:9, 21:9 and 16:10.

## 3. Item card

Mockups: [item-card](mockups/png/item-card.png), [item-card-states](mockups/png/item-card-states.png).

Width 300 at 1080p, scaled by `OverlayRules.Scale`. Sections top to bottom; a section with nothing known
is dropped, not shown empty.

| Section | Content | Source |
|---|---|---|
| Name line | Base type (unique: unique name in Unique colour, base below), rarity + class, `ilvl N` | `ParsedItem` |
| Verdict band | `◆ N wanted` (teal) and `✕ N unwanted` (orange); omitted when both are 0 or no profile exists. Uniques show **Average roll** (mean roll position of ranged mods) instead | `ModInfo.Highlight`; average is new, derived |
| Slots + base | `P` and `S` pips: filled = used, teal outline = open. Max per item: rare 3/3, magic 1/1, rare jewels 2/2; if the count or max is unknown, draw dashed pips and no "open" text. Right side: defence base percentile bar, e.g. `ES base 94th` | `ItemInfoResult.Prefixes/Suffixes`, `DefenceInfo` |
| Mod rows | One row per mod, in game order. Implicits first, separated by a hairline. See row spec below | `ModInfo` |
| Compare | `vs equipped <slot>`, two-column list of stat deltas with ▲ (blue) / ▼ (orange) and the absolute delta. Weapons show physical, elemental and total DPS. Stats GearCmp can't compute are omitted | GearCmp contribution |

**Mod row** (24 high): columns 12 · 12 · * · 26 · 40 · 30 · 20, gap 5.

1. Slot letter: `P` prefix, `S` suffix, `I` implicit, `?` unknown (Faint text).
2. Highlight mark: `◆` wanted (teal), `✕` unwanted (orange), else empty. An unwanted row's label is Secondary.
3. Short label (see §9), single line, ellipsis if it doesn't fit. Crafted mods: label in Crafted colour.
4. Tier pill: `T1`…`Tn` coloured by tier tone; `imp` outlined for implicits; `cr` Crafted; `U` Unique;
   `T?` dashed when unknown. Enchants: `ench` outlined.
5. Roll bar 40×6, fill width = roll position, coloured by roll tone; dashed empty track when unknown.
6. Roll %, right-aligned; `—` when unknown.
7. Required ilvl for that tier, right-aligned; blank for implicits, crafted and unique mods; `—` when unknown.

**Hybrid mods** (one mod, several game lines): one row, 40 high, a bracket left of the label and
`N game lines` under it, so row-to-line matching still holds. **Fixed unique lines** (no range): a dim row
with `·` in the pill column and `fixed` in place of the bar.

**Tones** reuse the existing calculator thresholds and `InfoColor` enum unchanged:
tier 1 = Best, tiers 2–3 = High, 4+ = Medium; roll 100% = Best, 75–99 = High, 25–74 = Medium, <25 = Low.

**States** (all in [item-card-states](mockups/png/item-card-states.png)):
- *Plain copy* (no advanced-copy headers): dashed band "Tiers and rolls need advanced copy. Turn it on in
  Settings › Items.", dashed pips and pills, empty tracks. Highlights still apply.
- *Unidentified*: name line plus base percentile only, with "Unidentified · mods hidden".
- *Unique*: no tiers, no slots; unique drop tier is unknown and is **not shown**.

## 4. Map card

Mockup: [map-card](mockups/png/map-card.png). Width 300. Interactive (not click-through), as today.

- Name line: map name; right side `T16 · rare · corrupted` (only parts that are known).
- Three stat tiles: Quantity, Rarity, Pack size, mono 20. A tile's number turns teal at or above the
  user's threshold (Overlay settings, defaults 70 / 40 / 25); otherwise primary text. Unknown: `?`.
- Danger band: counts of mods rated 3 ("deadly") and 2 ("risky") "for your ratings"; red-tinted when any
  3s. First time (no rated mods): dashed hint "Rate each mod once by clicking it. Every later map sorts itself."
- Mod rows sorted by rating (3 → 0), then game order. Columns: pips (34) · area (78) · short text. Rank 3
  rows get a red tint. Rank 0 mods fold into one row "+N more · harmless · click to show" once any mod is
  rated. Clicking a row cycles its rating 0–3 (existing behaviour); unknown mods (no ID) aren't clickable.
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
cell's **bottom-right** corner, mono 10, 14 high, solid background so it reads over item art. Chaos is
the unit; ≥1 divine shows `1.4d`. Tag colours by the user's threshold profile: ≥1 divine Best, ≥10 chaos
High, below that a neutral dark tag. A summary card **outside** the tab rectangle: tab total (≈ N div),
top three items with a 7-day sparkline and price, and price age.

## 6. Ambient and on-demand surfaces

Mockup: [ambient-hud](mockups/png/ambient-hud.png). All click-through unless noted.

- **Act guide**, width 340: `Act N · step i of n`, act and total timers right-aligned, a 3-high progress
  bar, the current step in 15/600, then `NEXT` + next step in Muted. Nothing else.
- **Timers** (Tab held), width 300: name, a 4-high bar, mono time. Under one minute turns orange; that is
  the only colour change. Stopwatch has no bar.
- **Event toast**, top centre, width 340: 4-wide colour strip (teal info, orange notable), one bold line,
  one muted line. 6 s, at most two stacked; older drop.
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
Map log, Sales, Overlay, Hotkeys, Transparency. Tray left-click opens it; the tray menu keeps Debug panel,
Run setup again, Open settings folder and Exit. Map log, Sales and Hotkeys open the existing windows
until they are rebuilt as pages (backlog). The window is resizable; below about 760 px wide the nav
stacks above the content.

- **Home**: title "Ready to play" (or the first problem, e.g. "Game not found"), league picker, four
  status tiles (Game, Client.txt, Prices, Game data) each with a teal or orange dot, one bold line and one
  muted detail. "Your keys": the user's real bindings as keycaps with their meaning. "Last session": maps
  run, average map time, deaths, divine sold, and a bar per map (orange = a death that map). Footer: the
  GGG disclaimer (required by FAIR-PLAY.md) and a link to Transparency.
- **Item rules**: five profile tabs (the existing `ItemInfoSettings` limit), the active one marked.
  A search box adds a rule by mod text (from the stats pack translations). Table: mod text, applies to,
  Want/Avoid segmented control, remove. "Applies to" with several classes is stored as one
  `HighlightRule` per class. Right: a live preview card built from the last copied item (in memory only)
  and "Card shows" toggles: compare, defence base percentile, ilvl column, implicits.
- **Overlay**: placement mode (§2) as two option cards; size slider (Auto = client-height scaling,
  80–150% override); density (Compact / Comfortable: row 24 vs 28); colours (Standard / High contrast:
  text Secondary → Primary, Muted → Secondary, borders +1 step); map highlight thresholds (§4).
- **Transparency**: "Everything ExileSight sent to the game", the last 200 lines of `actions.log`
  newest first: time, what was pressed (hotkey name or "Overlay click"), action kind, result (teal Sent /
  orange refusal reason). Show only fields the log already records (timestamp, hotkey, action, outcome,
  reason, chat verb); the log never stores item text, amounts or search strings, and the page must not
  add them. "Open actions.log" button. Below: what is read, which hosts are contacted, what is never touched.

## 8. Tokens

Mockup: [tokens](mockups/png/tokens.png). Implement as keys in `OverlayTheme.xaml` (overlays) and an app
theme dictionary (desktop window). The six `Tooltip*` keys keep their names; only colours change.

| Key | Value | Use |
|---|---|---|
| `TooltipBest` / `OverlayAccent` | #6FE3D5 | Best tier/roll, wanted, good numbers |
| `TooltipHigh` | #6AA8FF | High tier/roll, ▲ better |
| `TooltipMedium` | #AEB6C2 | Medium tier/roll |
| `TooltipLow` | #F2994A | Low roll, ▼ worse, unwanted, warnings |
| `TooltipCrafted` | #C3A6FF | Crafted mods |
| `TooltipUnique` | #E2B97F | Unique names and pills |
| `TooltipUnknown` | #5C6573, dashed | Unknown pills, pips, tracks |
| `OverlayBackground` | #0F1217 at 96% | Card surface |
| `OverlayRaised` | #17202A | Verdict band, stat tiles (#161A21) |
| `OverlayBorder` | #2E3540 | Card border, 1 px |
| `OverlayRule` | #262C35 | Hairlines; bar track #2A313C |
| `OverlayForeground` | #E8EAED | Primary text (15.6:1) |
| `TextSecondary` / `TextMuted` / `TextFaint` | #B7BEC8 / #8E97A4 / #7D8592 | 10.0 / 6.4 / 5.0:1 on the card |
| `Danger1` / `Danger2` / `Danger3` | #E6CC5A / #F2994A / #FF6B5E | Map pips; rank-3 row tint #221616 |
| `Accent` | #6FD1C6 | Interactive: selection, focus, links |
| `AppBackground` / `AppPanel` / `AppNav` | #0F1216 / #14181E / #0C0F12 | Desktop window |

Some mockup labels use #6B7480 (4.0:1); ship them as `TextFaint`. Type: Segoe UI Variable (fallback
Segoe UI) for text, Cascadia Mono (fallback Consolas) for numbers, with
`Typography.NumeralAlignment="Tabular"`. Overlay: title 13/600, body 13, labels 11/600 caps +12%
tracking, numbers 11–12. App: title 24/700, section 18/700, body 14. Spacing on a 4 px grid; card
padding 10 top, 12 sides; radius 6 (cards), 3 (pills), 4 (bands).

## 9. New data and settings

| Item | Owner task | Notes |
|---|---|---|
| Short mod labels | P7-T3 | Keyed by stat ID; ExileSight's own wording, ≤ 14 characters; fall back to the mod template with numbers removed, truncated |
| Item card model (slots, average roll, row kinds, hybrid line span) | P7-T5 | Built from `ItemInfoResult` and GearCmp; WPF-free |
| Placement mode, docked position, density, contrast | P7-T4 | `OverlaySettings`, validated and migrated |
| Card-shows toggles | P7-T5 | `ItemInfoSettings` |
| Map highlight thresholds | P7-T6 | MapInfo feature settings |
| Listed-for duration | P7-T7 | Only if shop history has the first-listed time |
| Stash tab total and top items | P7-T8 | From prices already fetched; no new requests |
| Home status and last session | P7-T12 | From existing services and logs |
| Transparency reader | P7-T14 | Read-only parser for the existing `actions.log` JSON lines |

No new network hosts, no new `GameAction`s, no new hotkeys.

## 10. Implementation rules (speed)

The idle budget in [PERFORMANCE.md](../PERFORMANCE.md) is unchanged (CPU < 1%, private memory < 200 MB),
and first-item latency must not regress.

- No `DropShadowEffect`, `BlurEffect` or other effects on overlay windows; layered windows render them in software.
- No animations, transitions or storyboards on in-game surfaces.
- Build and `Freeze()` brushes once from the theme; don't allocate brushes per row.
- Bars are `Rectangle`/`Border` with a width; not `ProgressBar` or templated controls.
- Keep one window per surface and rebuild its children (current pattern); never create windows per show.
- Card primitives are plain code-built elements shared by every surface; no third-party UI packages.

## 11. To check in game (🎮 Phase 7 gate)

- The beside-cursor rule never covers the game tooltip, over inventory and over stash, at 16:9, 21:9, 16:10.
- Advanced-copy mod order matches the order the game displays (row N = line N) on 20 real items.
- Stash tags in the bottom-right corner never hide stack sizes.
- Tier, roll and ilvl on the card match the game's advanced tooltip on the same 20 items.
- Idle CPU/memory and first-item latency stay within budget on the release build.

## 12. Review checklist for UI pull requests

- [ ] Compared against the mockup PNG for every state the task covers; differences are listed in the report.
- [ ] No in-game surface reprints mod text the game already shows.
- [ ] Every colour has a number, symbol or pip next to it.
- [ ] Unknown values render as unknown or are omitted; nothing is guessed.
- [ ] Only token keys from §8 are used; no hard-coded colours in views.
- [ ] No effects, animations or per-row brush allocation on overlays.
- [ ] No new `GameAction`, hotkey, network host, or screen read.
