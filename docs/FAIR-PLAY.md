# Fair play and the Action Gate

ExileSight must never break GGG's third-party policy, and never look like it does to an anti-cheat system. This document is the contract. A pull request that breaks any rule here is rejected, even when the feature behind it would be useful.

## What GGG's policy says

From https://www.pathofexile.com/developer/docs/index#policy (fetched 2026-11-06):

> Reading the game's log files is okay as long as the user is aware of what you are doing with that data.

> Automation of key-strokes (or other inputs) that affect the game in any way **must** follow our macro rules:
> - Macros must be invoked manually by the user (automated invocations such as but not limited to: timers, reacting to file changes, or from reading the screen are not allowed).
> - Each macro invocation must have one set function (it cannot cycle between actions).
> - The resulting function must only perform one action that interacts with the game (sending a single chat message or command counts as one action).

> [Executable apps that interact with the game or game files:] This behaviour is strictly against our Terms of Use… Creation or use of these type of applications will result in immediate account termination.

> Every public or widely available application should include some form of the following statement in a visible location: "This product isn't affiliated with or endorsed by Grinding Gear Games in any way."

GGG says new OAuth app registrations are currently closed, and that reverse-engineering undocumented endpoints is against the Terms of Use. ExileSight therefore makes **no calls to pathofexile.com APIs** (decision D-003).

## Allowed

- Drawing ExileSight's own top-most, click-through, no-activate windows over the game.
- Reading `Client.txt`. The first-run wizard tells the user what is read and why.
- Reading game-client pixels for display-only Stashninja page recognition and grid calibration under the limits below (D-104, 1.1).
- Reading the text that the game copies to the clipboard after a Ctrl+C sent by the Action Gate.
- Writing to ExileSight's own settings and data folders under `%APPDATA%\ExileSight`.
- HTTP GET to poe.ninja for prices and to GitHub for updates and data packs, with an identifiable User-Agent and at least one hour of caching.
- Opening URLs (wikis, poedb, trade-site searches) in the user's default browser.

## Screen reading limits (D-104)

- Capture only the game client rectangle, only while the game is foreground. A light Stashninja watch is enabled by default and can be disabled with **Show stash prices automatically**, independently of the menu.
- Check the header every 450 ms; identified Currency/Fragment pages also check the sub-tab strip against a fixed baseline without hiding tags. Stable identified pages have no periodic full capture. While unidentified, sparse fixed-transform grid pre-checks gate full reads at score ≥12.5; unidentified scenes are checked immediately and then no more often than every 1.8 s, regardless of header motion. Wider first-use calibration is limited to one batch per header baseline, at most two qualifying uncalibrated candidates, and one attempt per layout per 30 seconds even across watcher resets. A real header change is required for another batch. The ±12 px pre-check band cannot reliably admit larger first-use offsets; use Ctrl+Shift+N → Auto-calibrate or manual calibration.
- Recognize the open stash tab/page, locate its grid for calibration, and switch or position displayed price tags only. Screen results never send input or obtain an `IntentToken`.
- Use GDI `BitBlt` only: no DXGI capture, DirectX/Vulkan hooks or Windows.Graphics.Capture. Exclusive fullscreen may not capture; recommend Windowed Fullscreen.
- Supported grids are identified and calibrated locally without teaching. Optional user-taught references may still be captured after manual calibration or with **Remember this page**. Store them only on the user’s machine; ship and commit no game images.
- Declare screen-read purposes in the feature manifest and generated README transparency table. Display-only grid auto-calibration is approved by the owner as an extension of D-104; manual two-click calibration remains the fallback.

Windows drawn over the game (notifications or other overlays) can appear in the sampled region. Samples never leave the PC; only calibration transforms and optional reduced reference signatures are stored locally.

## Forbidden

- Opening a handle to the game process (`OpenProcess`, `ReadProcessMemory`, `WriteProcessMemory`, `VirtualAllocEx`, `CreateRemoteThread`), injecting DLLs, or using `SetWindowsHookEx` with any hook type that targets a thread rather than the low-level hooks `WH_KEYBOARD_LL` and `WH_MOUSE_LL`.
- Hooking DirectX, Vulkan or DXGI, or drawing into the game's swapchain.
- Moving, resizing, restyling, setting always-on-top on, or closing the game window. Exile-UI did some of this; ExileSight does not.
- Writing anything in the game's install folder, or changing `production_Config.ini` or `Client.txt`.
- Sending input from a timer, a log event, a file watcher, a network response or a screen-check result.
- Sending more than one game action per user invocation, or cycling between actions.
- Calling pathofexile.com endpoints, including the trade, trade-exchange and character APIs.
- Loading plugin or add-on code at runtime (decision D-007).

## The Action Gate

`IActionGate` (implemented in `ExileSight.Platform.Windows`) is the **only** code allowed to synthesize input. A banned-API analyzer (`Microsoft.CodeAnalysis.BannedApiAnalyzers`, `BannedSymbols.txt`) fails the build on `SendInput`, `keybd_event`, `mouse_event`, `PostMessage`/`SendMessage` to foreign windows, and any process-memory APIs. The only suppression allowed is in the Action Gate's own implementation file, and an architecture test checks that.

### How a key press becomes one action

1. The low-level hook receives a key or mouse event. If `LLKHF_INJECTED` or `LLMHF_INJECTED` is set, the event is passed through and never treated as a hotkey, so ExileSight can never trigger itself.
2. The hotkey dispatcher matches the event against the active hotkey context and creates an **`IntentToken`**: single-use, valid for about 1.5 s, and stamped with the event's timestamp and the hotkey ID. A click on an ExileSight overlay button also creates a token.
3. A feature calls `gate.TryExecute(token, action)` with **exactly one** `GameAction`:
   - `CopyItem` sends Ctrl+C. Holding Alt for advanced copy is optional.
   - `PasteSearch(text)` sends Ctrl+F, Ctrl+A, Ctrl+V, Enter. The search only highlights matches in the game client.
   - `SetListingAmount(amount)` sends Ctrl+A, Ctrl+V into the amount field of a Set Item Price dialog the user opened. It sends no Enter or clicks.
   - `SetPriceNote(text)` sends Ctrl+A, Ctrl+V, Enter, into a note box the user opened.
   - `SendChatCommand(text)` sends Enter, Ctrl+V, Enter. That is one chat message.
4. The gate refuses the action if the token is missing, used, or expired, or if the game is not the foreground window. It then:
   1. saves the user's clipboard
   2. sets the text
   3. sends the keys with `SendInput`
   4. waits for the game to read the clipboard
   5. **restores the user's clipboard**
   6. appends one line to the local audit log (`%APPDATA%\ExileSight\logs\actions.log`)
5. The token is consumed even when the action fails. Retrying needs a new key press.

Timers, the log watcher, poe.ninja responses and screen checks have no way to obtain a token. They can only update view models.

### Example: repricing a shop item

1. Press the omni-key over an item. The token is spent on `CopyItem`, and the overlay shows price buttons for the copied text.
2. In the default async mode, the user opens the game's **Set Item Price** dialog and focuses its amount field. ExileSight does **not** open the dialog.
3. The user clicks a price button showing **3 divine**. A new token is spent on `SetListingAmount(3)`: Ctrl+A, Ctrl+V, with no Enter or clicks.
4. The user chooses divine in the game's currency dropdown and clicks **LIST ITEM** themselves. Any currency reminder in ExileSight is display-only.

In optional note mode, the user instead opens the game's note box, then clicks a price button. That click spends one new token on `SetPriceNote("~b/o 3 divine")`: Ctrl+A, Ctrl+V, Enter. Each overlay click authorizes only its one declared game action.

## Review checklist for every pull request

- [ ] No new P/Invoke or WinRT call outside `ExileSight.Platform.Windows`.
- [ ] No input synthesis outside the Action Gate. The analyzer and the architecture test pass.
- [ ] Every new `GameAction` use is reached only from a token created by a user key press or click.
- [ ] No new network host apart from poe.ninja and GitHub.
- [ ] The feature's manifest lists every `GameAction` it uses, and the README transparency table is updated.
- [ ] Every screen-read purpose is declared in the feature manifest and generated README transparency table, and obeys the screen reading limits above.
