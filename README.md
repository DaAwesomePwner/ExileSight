# ExileSight

A fair-play, Windows-native quality-of-life overlay for **Path of Exile**. You get item info, map info, tracking, and price tags drawn over the game. It never reads game memory, never injects anything, and never automates the game.

> **This product isn't affiliated with or endorsed by Grinding Gear Games in any way.**

**Status:** pre-alpha. Nothing is ready to download yet.

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
| Shop repricing + sales log | Click a price button and one price note is pasted. Your listings and sales are tracked |
| Chat macros + chat wheel | Hideout and custom commands, one command per key press |
| Search-strings menu | Saved stash or vendor searches pasted into Ctrl+F |
| Timers | Countdowns and a stopwatch |

PoE2 support and more features are planned after 1.0. See [docs/ROADMAP.md](docs/ROADMAP.md).

## Fair play

ExileSight is built to follow [GGG's third-party policy](https://www.pathofexile.com/developer/docs/index#policy) by design, not by convention:

- **One key press gives one action.** Only one component, the Action Gate, can send input to the game. It only acts on a real key press or click, and only once per press.
- **Nothing automatic.** Timers, log events and screen checks can only change what the overlay shows. They can never send input.
- **No memory reading, injection or graphics hooks.** ExileSight never opens the game process and never changes the game window.
- **Transparent.** What it reads: the game's `Client.txt` log and text you copy with Ctrl+C. What it sends: only the actions listed in [docs/FAIR-PLAY.md](docs/FAIR-PLAY.md).

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
