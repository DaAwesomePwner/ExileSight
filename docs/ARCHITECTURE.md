# Architecture

ExileSight is a Windows-first .NET 10 application with a WPF UI. Everything that touches the operating system sits behind interfaces in a single platform project. Everything else is plain .NET that builds and tests on any OS, so most of the code can be developed and tested on macOS or Linux. The app itself only runs on Windows.

## Solution layout

```
ExileSight.slnx
Directory.Build.props          nullable on, warnings as errors, EnableWindowsTargeting=true
Directory.Build.targets        RS0030 forced to error, no warning-error exemptions
Directory.Packages.props       central package versions
BannedSymbols.txt              input/memory APIs banned outside the Action Gate (see FAIR-PLAY.md)
src/
  ExileSight.Core/                    net10.0       game model, item parser, mod matcher, log events, pricing, feature logic
  ExileSight.Platform.Abstractions/   net10.0       IGameWindow, IHotkeys, IActionGate, IClipboard, IClock, IFileTail …
  ExileSight.Platform.Windows/        net10.0-windows10.0.19041.0   Win32/WinRT implementations (CsWin32)
  ExileSight.Features.<Name>/         net10.0       one per feature: logic + view models, no WPF references
  ExileSight.App/                     net10.0-windows10.0.19041.0  WPF views, overlay host, tray, settings, first-run wizard
tools/
  ExileSight.DataGen/                 net10.0       builds versioned data packs (bases, mods, tiers, guides)
tests/
  ExileSight.Core.Tests/              xUnit, fixture-driven (tests/fixtures)
  ExileSight.Features.*.Tests/
  ExileSight.Architecture.Tests/      dependency and banned-API rules (NetArchTest or reflection)
```

**Dependency rule:** `Core` ← `Features.*` ← `App`, and `Platform.Abstractions` ← everything. Only `App` references `Platform.Windows`, and only to wire up dependency injection. `Core` and `Features.*` must never reference WPF or `Platform.Windows`. The architecture tests enforce this.

## Runtime components

| Component | Responsibility | Key APIs |
|---|---|---|
| Game window tracker | Find the PoE1 client (`POEWindowClass`, `PathOfExile*.exe`, GeForce NOW). Track foreground, move/size, minimize and DPI. Publish `GameWindowState`. | `SetWinEventHook` (`EVENT_SYSTEM_FOREGROUND`, `EVENT_OBJECT_LOCATIONCHANGE`), `GetWindowRect`, `GetDpiForWindow` |
| Overlay host | Create and position overlay windows relative to the game client area. Hide them when the game isn't in the foreground. | WPF `Window` with `AllowsTransparency`; ex-styles `WS_EX_LAYERED \| WS_EX_TRANSPARENT \| WS_EX_NOACTIVATE \| WS_EX_TOOLWINDOW`; click-through is toggled per window when it needs interaction |
| Input hooks + hotkey dispatcher | Global low-level hooks. Hotkey contexts (game focused, overlay X open, mouse over overlay Y). Block or pass through per binding. Create intent tokens. | `WH_KEYBOARD_LL`, `WH_MOUSE_LL` on a dedicated message-loop thread; ignore injected events |
| Action Gate | The only input synthesizer. See [FAIR-PLAY.md](FAIR-PLAY.md). | `SendInput`, clipboard APIs |
| Client.txt tailer | Tail the log by polling or file-change events (reading is passive). Parse lines into typed events: `AreaEntered`, `CharacterLevelUp`, `PlayerDied`, `NpcDialogue`, `FilterReloaded` … | `FileStream` with `FileShare.ReadWrite` |
| Item parser | Copied item text → `ParsedItem` (class, rarity, base, mods with tiers and ranges, sockets, quality, ilvl). Handles plain and advanced (Alt) copy. | pure .NET |
| Data packs | Versioned JSON packs per game (`poe1/bases`, `poe1/mods`, `poe1/guide` …), downloaded from GitHub releases and cached. League data updates without an app release. | System.Text.Json |
| Price service | poe.ninja overview per league and category. Cache at least 1 h. Identifiable User-Agent. | `HttpClient` |
| Settings | JSON per profile, under `%APPDATA%\ExileSight\<game>\`. Schema-versioned with migrations. | System.Text.Json |
| Updater | Delta updates and the installer. | Velopack |
| Logging | Rolling file logs. No personal data apart from local paths. | Serilog |

## Feature module contract

Each feature project exposes one `IFeature`:

```csharp
public interface IFeature
{
    FeatureManifest Manifest { get; }        // id, games, services, GameActions used, data packs
    void Register(IFeatureContext context);  // hotkeys, log subscriptions, overlay view models
}
```

`FeatureManifest.GameActions` is enforced at runtime: the gate refuses an action that isn't declared. A build step generates the README's "what it sends" table from the manifests.

The omni-key is a router feature. Other features register `IOmniHandler`s with a priority and a predicate on `(ParsedItem, context)`. The first match handles the key press.

## Overlays

- Overlays are positioned in **game-client coordinates** and scaled by client height. 1080p is the design baseline, the same convention Exile-UI uses.
- Tooltips and panels are small layered windows created once and shown or hidden. They are never created while the user is pressing a key.
- One `OverlayTheme` resource dictionary covers colors and fonts. Use bundled open fonts; don't ship GGG's Fontin.
- Multi-monitor and per-monitor DPI v2 are supported from day one. For aspect ratios wider than 21:9, compensate for the black bars.

## Threading

- The UI runs on the WPF dispatcher. Hooks run on their own STA thread with a message loop, and hook callbacks must return in under 1 ms (Windows drops slow hooks). They only enqueue work.
- The log tailer and network calls are async on the thread pool and marshal results to view models.

## Testing

- **Core and Features:** fixture-driven unit tests. Copied item texts and `Client.txt` excerpts are recorded on real Windows clients ([FIXTURES.md](FIXTURES.md)).
- **Architecture tests:** the dependency rule, banned APIs, and every feature having a manifest.
- **Platform.Windows:** a thin layer with integration tests that only run on Windows CI (`[WindowsOnlyFact]`).
- **In-game:** each phase ends with a short manual checklist the owner runs on Windows. This is the only step that needs a person.

## Mac later

A future `Platform.Mac` would implement the same abstractions (CGEventTap, ScreenCaptureKit, Vision OCR), and a new UI project would reuse `Features.*` view models. Nothing in Core or Features may assume Windows.

## Scaffold enforcement decisions (D-015, D-018–D-029)

The App targets Windows 10 build 19041 to reference Platform.Windows (the shorthand `net10.0-windows` defaults to Windows 7). Its application manifest declares PerMonitorV2 DPI awareness. WPF supplies HwndSource and WindowInteropHelper without additional package or project changes.

CsWin32 is the only native-import path: hand-written imports, extern methods and unmanaged function pointers are forbidden throughout src/ and tools/, including Platform.Windows. Its generated members are internal; marshaling is enabled for typed callback delegates, while friendly overloads and SafeHandle wrappers are disabled. Window/NativeMethods.txt and Input/NativeMethods.txt are separate generator inputs. Only SendInput among restricted APIs may be generated in production. A separate test-only ExileSight.BanCoverage.Tests inventory generates the other restricted APIs for exact metadata/ban coverage; production cannot reference it. Only Input/ActionGate.cs may suppress RS0030. Evaluated compiler guards before and after CoreCompile reject removed analyzers/bans, warning downgrades and unapproved analyzer configs, including mutations registered late in the build.

Architecture tests inspect exact current-configuration assembly paths, fail clearly when an assembly is missing, enforce project and compiled dependency edges, and reject P/Invoke metadata everywhere except Platform.Windows. DataGen is portable net10.0 and may reference Core and Abstractions. Core and Abstractions cannot reference features; features cannot reference each other. Only App and Platform.Windows.Tests may reference Platform.Windows. Build the entire solution before running architecture tests independently.

The normal debug window polls thread-safe IDebugSection snapshots; a section failure becomes an error row. Generic-host startup and shutdown are awaited off the dispatcher, while spike startup and WPF window work stay on the dispatcher. Source uses LF line endings and build-enforced file-scoped namespaces.

The build also rejects the compiler's final `_SkipAnalyzers` flag and any compiler response file. A separate architecture test audits every solution project's resolved compiler state through MSBuild, checks canonical imports and guard execution markers, and independently enforces the pinned analyzer allowlist. Production native checks match restricted names and entry points across all src assemblies. Test subprocess cleanup alone has one narrowly scoped Process.Kill exception; no production suppression permissions change.
