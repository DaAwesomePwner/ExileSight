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

## After 1.0 (backlog)

- **PoE2 support:** data packs and geometry, plus Statlas, Rune-ninja and the gemcutting helper.
- **Screen-recognition services** (template matching, calibration, OCR) and the features that need them: PoB tree and gem overlays, Sanctum planner, currency exchange helper, advanced cheat sheets, Betrayal recognition, TLDR tooltips.
- **Other features:** FilterSpoon, image and app cheat sheets, clone-frames, notepad, Enchant finder, essence tooltip, recombination simulator, seed explorer, lab overlay, Quick-PoB, Betrayal info sheet.
- **Image content** (zone layouts for Act-decoder, boss images), after a licensing check.
- **Non-English game clients**, translated by the community.
- **pathofexile.com APIs**, only if GGG reopens OAuth registration (D-003).
