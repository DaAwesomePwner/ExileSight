# Roadmap

## 1.0 scope (locked 2026-11-06)

1.0 covers Path of Exile 1 only, with an English game client and the 12 features below. Specs are in [FEATURES.md](FEATURES.md).

1.0 has **no screen recognition, OCR or calibration.** Every feature works from copied item text, `Client.txt`, user key presses and poe.ninja.

| ID | Feature | Rough effort (dev-weeks, both games → PoE1 ×0.8) |
|---|---|---|
| `omni` | Omni-key + context menu | 1–2 |
| `iteminfo` | Item-info tooltip | 5–8 |
| `gearcmp` | Gear comparison ("Set as equipped") | 2–3 |
| `guide` | Act-tracker guide | 3–4 |
| `mapinfo` | Map-info panel | 2–3 |
| `maptracker` | Map tracker + log viewer | 3–5 |
| `events` | Map-event notifications | 0.5–1 |
| `stashninja` | Stash-ninja price tags | 3–4 |
| `vaalasync` | Shop repricing + sales log | 2–3 |
| `macros` | Chat macros + chat wheel | 0.5–1 |
| `searchstr` | Search-strings menu (manual vendor picker) | 1.5–2 |
| `timers` | Timers | 0.5–1 |

The foundation adds 12–19 weeks. Altogether that is about 32–50 traditional dev-weeks, or roughly 18–28 with agents doing the implementation.

## Phases

Phases run in order. Inside a phase, tasks own disjoint files and run in parallel. Each task is implemented by one agent and reviewed by an agent from a different model family. A phase is complete once its tasks are merged, the full test suite passes, and Windows CI is green. **The owner is involved only at gates marked 🎮 (in-game testing on Windows).**

### Phase 0: scaffold and spikes
- **P0-T1 Scaffold.** Create the solution layout from ARCHITECTURE.md: `Directory.Build.props`, central package management, `BannedSymbols.txt` and the analyzer, the architecture test project, a placeholder Core test, and `.github/workflows/ci.yml` on `windows-latest` (restore, build, test, `dotnet publish` of the App as an artifact). Add `.editorconfig`.
- **P0-T2 Overlay spike** (after T1). Track the PoE1 window and show a click-through WPF overlay that follows it across monitors and DPI. Include a debug panel showing the game rect, DPI, foreground state, and the app's own memory and CPU use.
- **P0-T3 Input spike** (after T1, can run alongside T2). Low-level hooks on their own thread, the hotkey dispatcher, `IntentToken`, the Action Gate with `CopyItem` and `SendChatCommand`, clipboard restore and the audit log. The debug panel shows the last copied item text.
- 🎮 **Gate:**
  - The owner runs the CI artifact against PoE1 in windowed and borderless modes, under each renderer (DX11, DX12, Vulkan).
  - The overlay follows the game and never takes focus.
  - The hotkey copies the item and the text appears in the overlay.
  - `/hideout` is sent once per key press.
  - The owner records the fixture set ([FIXTURES.md](FIXTURES.md)).

### Phase 1: foundation
- Game window tracker and overlay host, made production-ready from the spike.
- Input layer and Action Gate, production-ready: hotkey contexts, block or pass-through, settings-driven bindings.
- Client.txt tailer and event parser, tested against the fixtures.
- PoE1 item parser for plain and advanced copy, tested against the fixtures.
- `DataGen` and the PoE1 data packs (bases, mods, tiers), plus the pack loader and cache.
- Settings and profiles, first-run wizard (find Client.txt, warn about exclusive fullscreen, explain what is read), tray, and the Velopack updater.
- 🎮 **Gate:** a clean install on a fresh Windows user account; the first-run wizard works; every fixture parses (in CI).

### Phase 2: items and trade
- `omni` router and context menu.
- `iteminfo`: mod and tier logic first, then the tooltip UI.
- `gearcmp`.
- Price service and `stashninja`.
- `vaalasync`.
- 🎮 **Gate:** tooltips match the game on 20 real items; repricing changes exactly one note per click; prices load for the current league.

### Phase 3: mapping and campaign
- `mapinfo`, `maptracker` with the log viewer, `events`, and `guide` (including the guide-content import).
- 🎮 **Gate:** one mapping session is logged correctly, and the guide advances through Acts 1–2 without manual input.

### Phase 4: utilities (can run alongside Phase 3)
- `macros`, `searchstr`, `timers`.
- Merged when CI is green; checked in-game during the Phase 5 gate.

### Phase 5: hardening and beta
- Performance budget: idle CPU under 1% and RAM under 200 MB.
- DPI and monitor matrix.
- README transparency table generated from the feature manifests.
- User docs, signed installer, and update channel.
- 🎮 **Gate:** a full play session on the release build, then a public beta about 4 weeks before a league launch.

### Phase 7: UI redesign (1.1)

Phase 6 shipped with 1.0. Phase 7 restyles every surface to the approved design, without changing what
the app is allowed to do. Read [design/UI-SPEC.md](design/UI-SPEC.md) and compare against the mockup PNGs
in [design/mockups/png](design/mockups/png) ([how](design/README.md#how-to-compare-your-work)).

Rules for every task below:
- **Compare against the mockup.** Each view task adds its own snapshot test class (one file, owned by that
  task, under the P7-T2 project; there is no shared registry) and lists any difference from the mockup
  under `RISKS`. Reviewers check the renders against the PNG and the UI-SPEC §12 checklist.
- **Renders come from Windows.** Run the snapshot tests on the Windows host for a task branch, or read the
  `ui-snapshots` artifact from CI (PRs, `integration/**` pushes, or a manual dispatch).
- **Views are testable.** Split each view into a window host and a pure builder (`model + theme →
  FrameworkElement`) with no live services (UI-SPEC §10).
- **Fair play and speed are unchanged.** No new `GameAction` kind, hotkey, network host or screen read.
  Idle budget and first-item latency in [PERFORMANCE.md](PERFORMANCE.md) must not regress.
- **Logic stays WPF-free and fixture-driven.** New model logic goes in `Core`/`Features.*` with tests first.

**Wave 1** (no dependencies):
- **P7-T2 UI snapshot harness.** Owns a new Windows-only test project
  (`tests/ExileSight.App.Snapshots.Tests`) with a helper that renders a `FrameworkElement` to PNG with
  `RenderTargetBitmap` at 1080p scale on an STA thread, one sample test class, `ExileSight.slnx`, and in
  `.github/workflows/ci.yml` a `workflow_dispatch` trigger plus upload of a `ui-snapshots` artifact. If the
  architecture tests reject a test project referencing App, add the narrowest allowance and record it as a
  decision. Done when CI publishes the sample PNG and the harness is documented in `docs/design/README.md`.
- **P7-T3 Short mod labels.** Owns a label table and lookup in `src/ExileSight.Features.ItemInfo` and its
  tests. Keyed by stat ID; ExileSight's own wording, at most 14 characters; fallback per UI-SPEC §9.
  Fixture-first: every mod in the existing item fixtures gets a label or a tested fallback.
- **P7-T4 Card placement and overlay settings.** Owns `src/ExileSight.Core/Overlay/PopupPlacement.cs`,
  `OverlaySettings` in `src/ExileSight.Core/Settings/` with validation and migration, and their tests.
  Beside-cursor side rule, docked position as client fractions, density, contrast (UI-SPEC §2, §7).
  Done when placement is unit-tested for both sides, clamping and docked mode across client sizes and
  multi-monitor work areas.
- **P7-T15 Omni advanced copy.** Owns `src/ExileSight.Features.Omni/OmniPress.cs`, the Omni feature's
  settings (`OmniFeature.cs`), their tests, and the omni/iteminfo lines in `docs/FEATURES.md` and
  `docs/USER-GUIDE.md`. The omni key sends `CopyItem(Advanced: true)` unless the new **Use advanced copy**
  setting is off (UI-SPEC §3.1, D-108). Done when tests show one `CopyItem` per press with the flag
  following the setting, and the item-info fixtures parse advanced text from the omni path.

**Wave 2:**
- **P7-T1 Theme tokens and card primitives** (after T2). Owns `src/ExileSight.App/Overlay/OverlayTheme.xaml`,
  `src/ExileSight.App/Features/ItemInfo/OverlayTheme.xaml`, a new app theme dictionary, a new
  `src/ExileSight.App/Ui/` folder of shared code-built primitives (tier pill, roll bar, slot pips, delta,
  stat tile, verdict band, card frame, hairline), and a primitives snapshot test class. Frozen brushes, font
  fallbacks, tabular numerals. Compare: [tokens](design/mockups/png/tokens.png). Done when every UI-SPEC §8
  key exists and each primitive renders in its snapshot test.
- **P7-T5 Item card model** (after T3; no view code). Owns `src/ExileSight.Features.ItemInfo` (except
  `ItemInfoSection.cs`), `src/ExileSight.Features.GearCmp` (except its `TooltipSection` builder), and any
  contract it adds in `src/ExileSight.Core/Features/`. Adds a structured, WPF-free card model **alongside**
  the existing text sections, which keep working until P7-T11: name line, verdict counts or unique average
  roll, slot pips with max per rarity/class, defence percentiles, rows (slot kind, label, tier, tones, roll,
  ilvl, highlight, game-line span), per-slot compare deltas, and the card-shows toggles in
  `ItemInfoSettings`. Keeps the current tone thresholds. Compare:
  [item-card-states](design/mockups/png/item-card-states.png). Done when fixtures cover every state in
  UI-SPEC §3 and row order equals the advanced-copy order.

**Wave 3** (after T1 and T2, plus any dependency named):
- **P7-T6 Map card** (also after T4). Owns `src/ExileSight.Features.MapInfo` (rating states in
  `RatingStore`, ordering in `MapInfoModel`, thresholds) and `src/ExileSight.App/Features/MapInfo/`.
  Compare: [map-card](design/mockups/png/map-card.png). Done when tests cover unrated vs rated-0 vs unknown,
  folding only rated-0 mods, and the first-time hint.
- **P7-T7 Shop pricer.** Owns `src/ExileSight.App/Features/Vaalasync/ShopView.cs` (and Vaalasync model code
  only if listed-for needs it). Armed/disarmed states, amount buttons, custom amount. Compare:
  [trade](design/mockups/png/trade.png), left column. Done when async and note modes both render and the
  disarm rule is unchanged.
- **P7-T8 Stash tags and summary.** Owns `src/ExileSight.App/Features/Stashninja/` (except calibration
  behaviour) and Stashninja model code for tag text and the highest-unit-price list. Compare:
  [trade](design/mockups/png/trade.png), right column. No new poe.ninja requests; no tab total.
- **P7-T9 Guide, timers, toasts.** Owns `src/ExileSight.App/Features/{Guide,Timers,Events}/` views and a
  structured snapshot in `src/ExileSight.Features.Guide/GuideModel.cs` with tests. Toast queue and position
  stay as they are. Compare: [ambient-hud](design/mockups/png/ambient-hud.png), top row.
- **P7-T10 Omni menu, chat wheel, search strings.** Owns `src/ExileSight.App/Features/OmniMenuView.cs`,
  `src/ExileSight.App/Features/{Macros,SearchStrings}/` views, and `src/ExileSight.Features.Omni/OmniMenu.cs`
  (item name in `OmniMenuPresentation`; it may pass the name from `OmniPress.cs`, which P7-T15 has merged).
  Compare: [ambient-hud](design/mockups/png/ambient-hud.png), bottom row. No number-key selection (backlog).
- **P7-T12 App shell, Home and Hotkeys.** Owns `src/ExileSight.App/App.xaml`, `App.xaml.cs`, a new
  `src/ExileSight.App/Shell/` (main window, nav, Home and Hotkeys pages, placeholder pages for Item rules,
  Overlay and Transparency), `src/ExileSight.App/Tray/`, and a new WPF-free `src/ExileSight.Core/Status/`
  for status-tile and last-session mapping with tests. The shell replaces the debug panel as `MainWindow`;
  tray left-click opens it. Compare: [app-home](design/mockups/png/app-home.png). Done when the GGG
  disclaimer is visible and each status tile and the session window have tested mappings.

**Wave 4:**
- **P7-T11 Item card view** (after T4, T5). Owns `src/ExileSight.App/Features/ItemInfo/`, and removes the
  old text path: `src/ExileSight.Features.ItemInfo/ItemInfoSection.cs` and GearCmp's `TooltipSection`
  builder, with their tests updated. Renders the card model with T1 primitives and T4 placement, applying
  the height cap (UI-SPEC §3). Compare: [item-card](design/mockups/png/item-card.png) and
  [item-card-states](design/mockups/png/item-card-states.png), every state. Done when first-item latency is
  measured against PERFORMANCE.md.
- **P7-T14 Overlay settings and Transparency pages** (after T4, T6, T12). Owns
  `src/ExileSight.App/Shell/Pages/Overlay*`, `Transparency*`, and a WPF-free `actions.log` reader in
  `src/ExileSight.Core/Input/` with fixture tests. The trigger column maps logged IDs to display names
  (UI-SPEC §7). Compare: [app-overlay-settings](design/mockups/png/app-overlay-settings.png).

**Wave 5:**
- **P7-T13 Item rules page** (after T11, T12, T15). Owns `src/ExileSight.App/Shell/Pages/ItemRules*` and a
  rule-text helper in `src/ExileSight.Features.ItemInfo` with tests proving template-derived rules match
  fixture item lines. Profile tabs, mod search from stats translations, applies-to, want/avoid, live
  preview of the last copied item, card-shows toggles and the advanced-copy setting. Saves through
  `ItemInfoSettings` validation. Compare: [app-item-rules](design/mockups/png/app-item-rules.png).

🎮 **Gate:** the in-game checks in [UI-SPEC §11](design/UI-SPEC.md#11-to-check-in-game--phase-7-gate),
then the owner compares each live surface with its mockup.

## After 1.0 (backlog)

- **UI follow-ups to Phase 7:** number-key selection in the omni menu (needs an input context while the
  menu is open); Map log and Sales rebuilt as pages in the app shell; editing hotkeys in the app.

- **PoE2 support:** data packs and geometry, plus Statlas, Rune-ninja and the gemcutting helper.
- **Screen-recognition services** (template matching, calibration, OCR) and the features that need them: PoB tree and gem overlays, Sanctum planner, currency exchange helper, advanced cheat sheets, Betrayal recognition, TLDR tooltips.
- **Other features:** FilterSpoon, image and app cheat sheets, clone-frames, notepad, Enchant finder, essence tooltip, recombination simulator, seed explorer, lab overlay, Quick-PoB, Betrayal info sheet.
- **Image content** (zone layouts for Act-decoder, boss images), after a licensing check.
- **Non-English game clients**, translated by the community.
- **pathofexile.com APIs**, only if GGG reopens OAuth registration (D-003).
