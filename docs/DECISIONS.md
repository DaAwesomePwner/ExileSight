# Decision log

| ID | Date | Decision | Why |
|---|---|---|---|
| D-001 | 2026-11-06 | C# on .NET 10 (LTS) with **WPF**. Feature view models stay WPF-free. | The most mature Windows UI stack with direct Win32/WinRT access. Avalonia was considered; it is only worth revisiting if a Mac build becomes a priority. |
| D-002 | 2026-11-06 | **PoE1 first.** PoE2 comes after 1.0. Every model is keyed by `Game` from day one. | The data and mechanics are more stable to get right first. Adding PoE2 then means data packs and geometry, not a refactor. |
| D-003 | 2026-11-06 | **No pathofexile.com API calls** (trade, exchange, character). Prices come from poe.ninja; trade searches open in the browser. | GGG's OAuth registration is closed and undocumented endpoints violate its ToS. |
| D-004 | 2026-11-06 | **Action Gate**: one physical key press or click gives one intent token, which allows exactly one `GameAction`. | This puts GGG's macro rules into code. See FAIR-PLAY.md. |
| D-005 | 2026-11-06 | The game window is never moved, restyled or closed. | Exile-UI did this; it counts as interacting with the game. |
| D-006 | 2026-11-06 | **No screen recognition in 1.0.** Gear comparison uses a "Set as equipped" button instead of detecting the inventory. | It saves 3–5 weeks of capture and calibration work, and avoids per-resolution fragility. |
| D-007 | 2026-11-06 | No runtime plugin or add-on loading. | Security: Exile-UI's add-on loader runs arbitrary code. |
| D-008 | 2026-11-06 | Zone-layout and boss images, and data Exile-UI derived from poedb, are not shipped until their licensing is checked. | IP hygiene for a public repo. |
| D-009 | 2026-11-06 | MIT license under `daplabs`, with credit to Exile-UI (MIT) in THIRD-PARTY-NOTICES.md. | Matches upstream and makes porting straightforward. |
| D-010 | 2026-11-06 | Built by AI agents. Every task is reviewed by an agent from a different model family. The owner is involved only for in-game Windows testing gates. | The owner's instruction. |
