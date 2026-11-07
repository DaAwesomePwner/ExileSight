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
- Reading the text that the game copies to the clipboard after a Ctrl+C sent by the Action Gate.
- Writing to ExileSight's own settings and data folders under `%APPDATA%\ExileSight`.
- HTTP GET to poe.ninja for prices and to GitHub for updates and data packs, with an identifiable User-Agent and at least one hour of caching.
- Opening URLs (wikis, poedb, trade-site searches) in the user's default browser.

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

Timers, the log watcher, poe.ninja responses and future screen checks have no way to obtain a token. They can only update view models.

### Example: repricing a shop item

1. Press the omni-key over an item. The token is spent on `CopyItem`, and the overlay shows price buttons for the copied text.
2. The user right-clicks the item to open the game's note box. ExileSight does **not** send that right-click.
3. The user clicks a price button. A new token is spent on `SetPriceNote("~b/o 3 divine")`.

That is one server-side change for each physical click.

## Review checklist for every pull request

- [ ] No new P/Invoke or WinRT call outside `ExileSight.Platform.Windows`.
- [ ] No input synthesis outside the Action Gate. The analyzer and the architecture test pass.
- [ ] Every new `GameAction` use is reached only from a token created by a user key press or click.
- [ ] No new network host apart from poe.ninja and GitHub.
- [ ] The feature's manifest lists every `GameAction` it uses, and the README transparency table is updated.
