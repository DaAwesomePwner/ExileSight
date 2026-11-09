# Architecture

ExileSight is a Windows-first .NET 10 application with a WPF UI. Everything that touches the operating system sits behind interfaces in a single platform project. Everything else is plain .NET that builds and tests on any OS, so most of the code can be developed and tested on macOS or Linux. The app itself only runs on Windows.

## Solution layout

```
ExileSight.slnx
Directory.Build.props          nullable on, warnings as errors, EnableWindowsTargeting=true
Directory.Build.targets        RS0030 forced to error, no warning-error exemptions
Directory.Packages.props       central package versions
BannedSymbols.txt              global input/interop/Reflection.Emit/Win32 process-memory bans
BannedSymbols.Production.txt   additional managed Process member bans for src/ and tools/
src/
  ExileSight.Core/                    net10.0       game model, item parser, mod matcher, log events, pricing, feature logic
  ExileSight.Platform.Abstractions/   net10.0       IGameWindow, IHotkeys, IActionGate, IClipboard, IClock, IFileTail …
  ExileSight.Platform.Windows/        net10.0-windows10.0.19041.0   Win32/WinRT implementations (CsWin32)
  ExileSight.Features.<Name>/         net10.0       one per feature: logic + view models, no WPF references
  ExileSight.App/                     net10.0-windows10.0.19041.0  WPF views, overlay host, tray, settings, first-run wizard
tools/
  ExileSight.RedactLog/               net10.0       redacts Client.txt excerpts for fixtures
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
| Data packs | Schema-3 `poe1/bases`, `poe1/mods` and `poe1/stats` from pinned RePoE. CI bundles verified packs and ATTRIBUTION.txt under `packs/poe1`; startup loads cache/local fallback, prepares translations in the background, then refreshes from GitHub after setup at most hourly with persisted per-pack/tag attempts. Manual release workflow defaults to dry-run; publication requires main and the release environment. | System.Text.Json |
| Price service | poe.ninja exchange/stash overview per league/type and index-state leagues. Persist attempts before HTTP; throttle across restarts for at least 1 h, including failures. ETag/304, stale fallback, 8 MB response limit and identifiable User-Agent. Prefer the current softcore challenge league excluding SSF/Ruthless, else Standard. | `HttpClient` |
| Settings | JSON per profile, under `%APPDATA%\ExileSight\<game>\`. Schema-versioned with migrations. | System.Text.Json |
| Updater | GitHub updates after a 5 s startup delay, at most every 6 h; background download, apply on normal exit without restart. Stable/prerelease settings; dev installs are inert. Explicit Program Main selected via StartupObject; App.xaml stays an ApplicationDefinition. CI packages unsigned installers without publishing releases. | Velopack |
| Logging | Rolling file logs exclude item/chat text. Global dispatcher and unobserved-task handlers record exception types only. | Serilog |

## Feature module contract

Features are compiled in and registered explicitly in App; runtime plugin loading and assembly discovery are forbidden. Each feature project exposes one `IFeature`:

```csharp
public interface IFeature
{
    FeatureManifest Manifest { get; }        // immutable identity, games, actions and resources
    void Register(IFeatureContext context);  // commands, defaults, subscriptions and contributions
}
```

Registration finishes before input starts. Manifests declare actions, screen-read purposes, packs, services, HTTP hosts and browser hosts separately. Features cannot use `System.Net`; App supplies shared services. Architecture tests validate manifests, declared actions and network boundaries. The README transparency table is generated from these declarations and registered defaults by the transparency tests.

The hook pipeline attaches feature identity when issuing an intent token; features cannot claim another owner. The issuer can be installed only once. `GatePolicy` combines built-in CopyItem/SendChatCommand permissions with per-feature manifest declarations and validates the token's owner and action. PasteSearch, SetPriceNote and SetListingAmount are available only through declaring features.

Features may declare default bindings for their own commands. A persisted binding with the same ID wins, including an explicitly disabled binding. Omni declares blocked CapsLock by default, spends its token on CopyItem once at key-down and classifies the token-free release as long only after 200 ms. A missing release does not dispatch a handler. One host-owned router selects the first matching handler by descending priority, with registration order breaking ties. Routing and long-press menus run on a serial token-free queue off the input worker.

Features contribute WPF-free tooltip sections and local item-menu entries for a `ParsedItem`. Tooltip sections sort by ascending priority; menu providers retain registration order. Provider failures are isolated, and the host stamps menu-entry ownership. Menu contributions receive no intent token. GearCmp contributes comparisons to the tooltip and Set as equipped/character selection to the omni long-press menu.

## Overlays

- Overlays use **game-client coordinates** and client-height scaling, with 1080p as the baseline and an optional user scale override. Per-monitor DPI v2 and multi-monitor positioning are supported.
- Feature logic and view models stay WPF-free; App creates and styles the overlay windows. Item-info and omni windows are prepared before presentation, then shown or hidden as needed.
- Item-info uses the shared `OverlayTheme` dictionary. Its tooltip is click-through and closes on movement beyond the scaled threshold or Esc; it has no interactive equipped button.
- A shared passive hook-to-UI channel is interested only while an observing overlay is visible. It coalesces mouse moves, queues clicks/Esc separately and uses visibility epochs to reject old observations after reopening. Its dispatcher drain cannot issue game input.
- Interactive overlay buttons redeem tokens only by pairing WPF mouse-up with a hook-observed, non-injected left click on the same owned HWND, within 4 px and 500 ms of physical mouse-up. The gesture also requires matching down/up within 4 px and 1.5 s. A UI-built z-order snapshot chooses the topmost matching overlay; redemption is single-use and resolves a host-registered feature command.
- Overlay open/close paths use failure guards and cleanup; diagnostics record exception types without copied item or chat text.

## Display-only stash screen reading (1.1, D-104)

Only App types in `ExileSight.App.Features.Stashninja` consume `IScreenSampler`, apart from its DI registration and platform implementation/contract. An architecture test guards this boundary.

`IScreenSampler` is the platform boundary for GDI `BitBlt` capture in Windows; recognition logic stays WPF-free. Capture is limited to the game client rectangle while the game is foreground and the stash overlay is open. No DXGI capture, DirectX/Vulkan hooks or Windows.Graphics.Capture are allowed. Exclusive fullscreen may not capture; recommend Windowed Fullscreen.

The user teaches page references, captured locally after calibration or with **Remember this page**. References remain on the user’s machine; no game images ship or are committed. Recognition only switches overlay price tags; sampling and recognition cannot obtain an `IntentToken` or send game input. The manifest declares the screen-read purpose. Calibration-free grid location is outside D-104 and may be considered later.

## Threading

- The UI runs on the WPF dispatcher. Hooks run on their own STA thread with a message loop, and hook callbacks must return in under 1 ms (Windows drops slow hooks). They only enqueue work.
- The log tailer and network calls are async on the thread pool and marshal results to view models.

## Testing

- **Core and Features:** fixture-driven unit tests. Copied item texts and `Client.txt` excerpts are recorded on real Windows clients ([FIXTURES.md](FIXTURES.md)).
- **Architecture tests:** the dependency rule, banned APIs, and every feature having a manifest.
- **Platform.Windows:** a thin layer with integration tests that only run on Windows CI (`[WindowsOnlyFact]`).
- **In-game:** each phase ends with a short manual checklist the owner runs on Windows. This is the only step that needs a person.

## Mac later

A future `Platform.Mac` would implement the same abstractions (for example, CGEventTap; screen capture would need a separate platform decision), and a new UI project would reuse `Features.*` view models. Nothing in Core or Features may assume Windows.

## Scaffold enforcement decisions (D-015, D-018–D-029)

The App targets Windows 10 build 19041 to reference Platform.Windows (the shorthand `net10.0-windows` defaults to Windows 7). Its application manifest declares PerMonitorV2 DPI awareness. WPF supplies HwndSource and WindowInteropHelper without additional package or project changes.

CsWin32 is the only native-import path: hand-written imports, extern methods and unmanaged function pointers are forbidden throughout src/ and tools/, including Platform.Windows. Its generated members are internal; marshaling is enabled for typed callback delegates, while friendly overloads and SafeHandle wrappers are disabled. Window/NativeMethods.txt and Input/NativeMethods.txt are separate generator inputs. Only SendInput among restricted APIs may be generated in production. A separate test-only ExileSight.BanCoverage.Tests inventory generates the other restricted APIs for exact metadata/ban coverage; production cannot reference it. Only Input/ActionGate.cs may suppress RS0030. Evaluated compiler guards before and after CoreCompile reject removed analyzers/bans, warning downgrades and unapproved analyzer configs, including mutations registered late in the build.

Architecture tests inspect exact current-configuration assembly paths, fail clearly when an assembly is missing, enforce project and compiled dependency edges, and reject P/Invoke metadata everywhere except Platform.Windows. DataGen is portable net10.0 and may reference Core and Abstractions. Core and Abstractions cannot reference features; features cannot reference each other. Only App and Platform.Windows.Tests may reference Platform.Windows. Build the entire solution before running architecture tests independently.

The normal debug window polls thread-safe IDebugSection snapshots; a section failure becomes an error row. Generic-host startup and shutdown are awaited off the dispatcher, while spike startup and WPF window work stay on the dispatcher. Source uses LF line endings and build-enforced file-scoped namespaces.

The build also rejects the compiler's final `_SkipAnalyzers` flag and any compiler response file. A separate architecture test audits every solution project's resolved compiler state through MSBuild, checks canonical imports and guard execution markers, and independently enforces the pinned analyzer allowlist. Production native checks match restricted names and entry points across all src assemblies. Managed System.Diagnostics.Process member bans apply through BannedSymbols.Production.txt only to src/ and tools/. Tests can terminate their owned child processes normally, without any pragma. The only permitted RS0030 suppression anywhere remains Input/ActionGate.cs. Both the guard and independent audit require the production list in addition to the global list for all production projects.
