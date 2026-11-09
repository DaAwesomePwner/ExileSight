# ExileSight

[User guide](docs/USER-GUIDE.md)

A fair-play, Windows-native quality-of-life overlay for **Path of Exile**. You get item info, map info, tracking, and price tags drawn over the game. It never reads game memory, never injects anything, and never automates the game.

> **This product isn't affiliated with or endorsed by Grinding Gear Games in any way.**

**Status:** 1.0 for Path of Exile 1. Download the installer from [Releases](https://github.com/DaAwesomePwner/ExileSight/releases); installed copies update themselves. See the [changelog](CHANGELOG.md).

## What 1.0 will do (Path of Exile 1)

| Feature | What you get |
|---|---|
| Omni-key + context menu | One hotkey on an item opens the right tool, plus links to wikis and crafting sites |
| Item-info tooltip | Mod tiers, roll ranges, affix levels, highlight profiles |
| Gear comparison | Compare a hovered item against gear you registered as equipped |
| Act-tracker guide | A leveling guide that moves forward as you change zones |
| Map-info panel | Map mods ranked by how dangerous you think they are |
| Map tracker + log viewer | Run times, deaths, league mechanics, notes, CSV export |
| Map-event notifications | Alerts from the game log, such as mercenaries and discoverable hideouts |
| Stash-ninja price tags | poe.ninja prices drawn over fixed stash tabs |
| Shop repricing + sales log | By default, click a price button to paste an async listing amount; pick the currency and click LIST ITEM yourself. Optional `~b/o` note mode is also available. Your listings and sales are tracked |
| Chat macros + chat wheel | Hideout and custom commands, one command per key press |
| Search-strings menu | Saved stash or vendor searches pasted into Ctrl+F |
| Timers | Countdowns and a stopwatch |

PoE2 support and more features are planned after 1.0. See [docs/ROADMAP.md](docs/ROADMAP.md).

## Fair play

ExileSight is built to follow [GGG's third-party policy](https://www.pathofexile.com/developer/docs/index#policy) by design, not by convention:

- **One key press gives one action.** Only one component, the Action Gate, can send input to the game. It only acts on a real key press or click, and only once per press.
- **Display-only updates.** Timers, log events and screen checks can only change what the overlay shows. They can never send input or obtain an IntentToken.
- **No memory reading, injection or graphics hooks.** ExileSight never opens the game process and never changes the game window.
- **Transparent.** What it reads: the game's `Client.txt` log, text you copy with Ctrl+C, and, for 1.1 Stashninja page recognition (D-104), pixels inside the game client rectangle only while the game is foreground and the stash overlay is open. What it sends: only the actions listed in [docs/FAIR-PLAY.md](docs/FAIR-PLAY.md).

Stash page recognition uses GDI BitBlt, with no DXGI capture, DirectX/Vulkan hooks or Windows.Graphics.Capture. User-taught page references are captured locally after calibration or with **Remember this page** and stay on your machine; no game images are shipped or committed. Exclusive fullscreen may not capture; Windowed Fullscreen is recommended. Calibration-free grid location is a possible later follow-up, outside D-104.

See the [transparency table](#transparency) for each feature’s declared permissions and defaults.

<!-- transparency:start -->
## Transparency

Generated from compiled feature manifests and registered default bindings; supplemental file writes and App services are declared in `tests/ExileSight.Transparency.Tests/DeclaredResources.cs`.

| Feature | Game actions | Screen reads | Data packs | Hosts | Writes | Default keys |
|---|---|---|---|---|---|---|
| events | None | None | `poe1/events` | None | `active profile JSON (event settings)` | `Control+Shift+E → events:settings (blocked)` |
| gearcmp | None | None | `poe1/stats` | None | `poe1/gearcmp/active.json`<br>`poe1/gearcmp/character-{hash}.json`<br>`poe1/gearcmp/default.json` | None |
| guide | None | None | `poe1/guide` | None | `poe1/guide/progress.json`<br>`poe1/guide/{hash}.json`<br>`user-selected CSV export` | `Control+Shift+F10 → guide:toggle (blocked)`<br>`Control+Shift+F11 → guide:back (blocked)`<br>`Control+Shift+F12 → guide:next (blocked)` |
| iteminfo | None | None | `poe1/bases`<br>`poe1/mods`<br>`poe1/stats` | None | `active profile JSON (highlight profiles)` | None |
| lab | None | None | None | `Browser: poelab.com`<br>`Browser: www.poelab.com` | `poe1/lab/latest.txt`<br>`poe1/lab/{difficulty}.json` | `Control+Shift+L → lab:toggle (blocked)` |
| macros | `SendChatCommand` | None | None | None | `active profile JSON (commands)` | `F5 → macros:travel (blocked)`<br>`F6 → macros:macro (blocked)` |
| mapinfo | None | None | `poe1/mapmods` | None | `poe1/mapinfo/ratings.json` | None |
| maptracker | `SendChatCommand` | None | None | `HTTP: poe.ninja` | `active profile JSON (league)`<br>`poe1/maptracker/{league-hash}.jsonl`<br>`user-selected CSV export` | `Control+Shift+K → maptracker:kills (blocked)`<br>`Control+Shift+M → maptracker:viewer (blocked)` |
| omni | `CopyItem` | None | None | `Browser: poedb.tw`<br>`Browser: www.craftofexile.com`<br>`Browser: www.pathofexile.com`<br>`Browser: www.poewiki.net` | None | `CapsLock → omni:press (blocked)` |
| searchstr | `PasteSearch` | None | None | None | None | `F9 → searchstr:open (blocked)` |
| stashninja | `PasteSearch` | `automatic stash watch, page recognition and grid calibration (foreground game client, display-only)` | None | `HTTP: poe.ninja` | `poe1/stashninja/calibration.json` | `Control+Shift+N → stashninja:open (blocked)` |
| timers | None | None | None | None | `poe1/timers/timers.json` | `Control+Shift+T → timers:toggle (blocked)`<br>`Tab → timers:hold (passed through)` |
| vaalasync | `SetListingAmount`<br>`SetPriceNote` | None | None | `HTTP: poe.ninja` | `poe1/vaalasync/history.json` | None |

Omni owns the initial CopyItem action for other item tools. Browser hosts (including www.pathofexile.com) open your browser only; ExileSight itself makes no request to those browser links.

Paths are relative to `%APPDATA%/ExileSight` (or the configured portable root), except user-selected exports and Velopack install files. Packs and the Action Gate audit always use `%APPDATA%/ExileSight`. Active profile JSON is `poe1/{profile-id}.json`. Stores may also create temporary, backup, recovery and corrupt-preservation files (`.tmp`, `.bak`, `.recovery`, `.corrupt*`).

### App services

| Service | HTTP hosts | Writes |
|---|---|---|
| Data packs | `github.com`<br>`objects.githubusercontent.com`<br>`release-assets.githubusercontent.com` | `cache/poe1/packs/*.pack.json`<br>`cache/poe1/packs/*.refresh.json` |
| Prices | `poe.ninja` | `cache/poe1/poeninja/*.json`<br>`cache/poe1/poeninja/*.json.lock` |
| Updates (installed builds) | `api.github.com`<br>`github.com`<br>`objects.githubusercontent.com`<br>`release-assets.githubusercontent.com` | `Velopack-managed update packages and install files` |
| Settings and profiles | None | `poe1/profiles.json`<br>`poe1/{profile-id}.json` |
| Action Gate audit | None | `logs/actions.log` |

Regenerate from the repository root (PowerShell: set `$env:UPDATE_TRANSPARENCY='1'` first):

```sh
UPDATE_TRANSPARENCY=1 dotnet test tests/ExileSight.Transparency.Tests
```
<!-- transparency:end -->

## Building

You need the .NET 10 SDK. The app runs on Windows 10 (1903+) and Windows 11. Core libraries and tests also build on macOS and Linux.

```
dotnet build ExileSight.slnx
dotnet test ExileSight.slnx
```

## Docs

- [Architecture](docs/ARCHITECTURE.md)
- [Fair play and the Action Gate](docs/FAIR-PLAY.md)
- [Roadmap and 1.0 scope](docs/ROADMAP.md)
- [1.0 feature specs](docs/FEATURES.md)
- [Recording test fixtures](docs/FIXTURES.md)
- [Decision log](docs/DECISIONS.md)

## Credits

ExileSight is a from-scratch rewrite inspired by [Exile-UI](https://github.com/Lailloken/Exile-UI) by Lailloken (MIT). Parts of its data and logic are ported with attribution; see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). Price data comes from [poe.ninja](https://poe.ninja).

## License

[MIT](LICENSE)

The ExileSight icon is original geometric artwork under this repository’s MIT license. Regenerate it with `dotnet run --project tools/ExileSight.IconGen`; verify the committed asset with `dotnet run --project tools/ExileSight.IconGen -- --check`. WPF windows inherit the executable icon.
