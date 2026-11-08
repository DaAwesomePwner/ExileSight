# Agent guide for ExileSight

Read these before changing anything:
- [docs/FAIR-PLAY.md](docs/FAIR-PLAY.md). These rules are not negotiable.
- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)
- The current phase in [docs/ROADMAP.md](docs/ROADMAP.md)
- The spec for your feature in [docs/FEATURES.md](docs/FEATURES.md)
- For any UI work: [docs/design/UI-SPEC.md](docs/design/UI-SPEC.md), and open the mockup PNGs in [docs/design/mockups/png](docs/design/mockups/png) to compare your result against ([how](docs/design/README.md#how-to-compare-your-work))

## Hard rules

1. Only the Action Gate synthesizes input. Never add `SendInput`, `keybd_event`, `mouse_event`, `PostMessage`/`SendMessage` to foreign windows, or any process-memory API anywhere else. Never suppress the banned-API analyzer outside the Action Gate implementation file.
2. Never send input from a timer, a log event, a file watcher, a network callback or a screen check. Input only follows an `IntentToken` created by a user key press or click.
3. Never touch the game window (move, resize, restyle, close, set always-on-top) or the game's files.
4. No network hosts other than poe.ninja and GitHub. Nothing from pathofexile.com.
5. `ExileSight.Core` and `ExileSight.Features.*` reference neither WPF nor `ExileSight.Platform.Windows`.
6. Never weaken, skip or delete tests to get a build to pass.

## Developing on macOS or Linux

- `Directory.Build.props` sets `EnableWindowsTargeting=true`, so the whole solution **builds** anywhere. Only Windows can **run** the WPF app and the `Platform.Windows` integration tests.
- Run tests locally with `dotnet test`. Windows-only tests are skipped off Windows.
- CI on `windows-latest` is the source of truth for Windows builds and tests. It also publishes the app as an artifact for in-game testing.

## Conventions

- C# latest, nullable enabled, warnings as errors, file-scoped namespaces.
- Keep files small and focused; split anything that grows past about 200 lines.
- Use central package management (`Directory.Packages.props`). Add a package only when your task brief allows it.
- Parser and feature logic are fixture-driven: add the fixture first (tests/fixtures, see docs/FIXTURES.md), then the test, then the code.
- A ported data file or algorithm names its Exile-UI source in a header comment.
- Commits: imperative mood, one logical change per commit. Never commit secrets or personal data (redact Client.txt names).

## Verify before reporting done

```
dotnet build ExileSight.slnx -warnaserror
dotnet test ExileSight.slnx
```
