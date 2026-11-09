# ExileSight user guide

ExileSight is a Windows overlay for **Path of Exile 1 with an English client**.
It lives in the notification area beside the clock. Right-click its icon for
**Debug panel**, **Run setup again**, **Open settings folder**, and **Exit**.
Clicking the icon opens diagnostics; closing that window leaves the app running.

The stash price panel supports eight measured pages; see [stashninja](#price-check--stashninja--ctrlshiftn).

This product isn't affiliated with or endorsed by Grinding Gear Games in any way.

## Install and update

Download `ExileSight-stable-Setup.exe` from the project’s
[GitHub Releases](https://github.com/DaAwesomePwner/ExileSight/releases) page,
run it, then complete the wizard. Each release lists what changed. Test builds
from GitHub Actions (**ExileSight-Velopack-win-x64** installer,
**ExileSight-App-win-x64** portable app) are for testing only.

Installed copies check the project's GitHub releases after a five-second startup
delay, then every six hours. Available updates download in the background and
apply after a normal **Exit**, without reopening the app. Start it again yourself.
Portable/development copies do not use automatic updates. Windows shutdown does
not apply a pending update through the normal-exit path.

The default channel is `stable`. To include prereleases, set
`general.updateChannel` to `"prerelease"` in your active settings file; set
`general.checkForUpdates` to `false` to disable checks. Preferences are reread
for checks, downloads and normal exit; channel changes take effect at the next
scheduled check. See [Rebinding keys](#rebinding-keys) for finding that file.
Diagnostics show the current version, update status and last check.

Settings normally live in `%APPDATA%\ExileSight\poe1\`. Back up the whole
`%APPDATA%\ExileSight` folder to keep settings and feature history. To uninstall
an installed copy, exit and use Windows **Installed apps**. For a portable copy,
exit and remove its extracted folder. To remove saved settings and history too,
remove `%APPDATA%\ExileSight` separately after backing up anything you want.

## First-run wizard

1. Read the clipboard, log and hotkey explanation. The wizard lists the bindings
   already in your profile; feature defaults are described below.
2. Choose the log for the installation you actually play. Detected readable paths
   appear in the list, or use **Browse…**. A usual location is
   `<game installation>\logs\Client.txt`. `KakaoClient.txt` and `LatestClient.txt`
   are also accepted. The file must exist, be readable, and be on a local drive;
   network shares and mapped network drives are refused.
3. Set the game to **Windowed** or **Windowed Fullscreen** yourself. Exclusive
   Fullscreen cannot show these overlays. The wizard does not change game settings.
4. Choose **Finish** to save the path and start in the tray. Use **Run setup again**
   if you change installations. Canceling initial setup exits without completing it.

## Fair play: what is read and sent

ExileSight reads the selected game log without changing it, and reads item text
copied after your hotkey. In 1.1, Stashninja may also read pixels of the foreground
foreground game client to recognize stash pages automatically (D-104). It does not
read game memory, inject code, or change the game window or game files.

Every game action follows your own physical key press or overlay click, and each
invocation can perform only one game action: copy an item, paste a search, set a
listing amount or price note in an already-open dialog, or send one chat
message/command. Timers, log events and price downloads only update local displays;
they never send game input. Actions are refused when the game is not foreground.
A failed action needs a fresh gesture.

The Action Gate is the single input sender that enforces one game action per
physical gesture. It attempts to preserve and restore your clipboard. Diagnostics
report unsupported clipboard formats or restoration problems; preservation is not guaranteed
for every format. Close unfinished chat drafts before using chat macros: opening chat
with Enter can submit an existing draft.

See [Fair play and the Action Gate](FAIR-PLAY.md) for the full contract.

## Features and default keys

These are defaults; saved bindings take precedence. Hotkeys normally work while
the game is foreground. Unless noted, a bound key is consumed by ExileSight.

### Omni and item info — CapsLock

Omni is the shared item hotkey that selects a tool from copied item text.
Hover an item, tap **CapsLock** for at most 200 ms, then release to show the
matching tool. Maps and priced items route to their specialized panels only on
this short tap. Equipment opens item info, with tiers, rolls and other supported
details when the copied text
and data packs provide them. Unsupported values stay unknown. Move away or press
**Esc** to dismiss the item tooltip.

Hold CapsLock for more than 200 ms, then release to open the item menu instead.
It offers applicable local tools and browser links. Copy happens once on key-down;
keeping the key held does not repeatedly copy. **F7** also copies an item, but
does not run the omni tools.

### Gear comparison — omni menu

There is no separate default key. Long-press CapsLock over equipment and choose
**Set as equipped** for the appropriate slot. Rings and one-handed weapons offer
side choices. Later item tooltips compare against those saved snapshots; update
snapshots yourself when you change gear. Weapon DPS is not your character's DPS.
Use the menu's character confirmation or the labelled default profile; log names
are suggestions until you explicitly choose one.

### Price check / stashninja — Ctrl+Shift+N

Open a supported stash page and poe.ninja prices appear automatically. ExileSight
identifies the cell arrangement and saves calibration after two consecutive checks agree on the page and position at each resolution;
no menu, page selection or teaching is required. Closing the stash or opening an
unknown page hides its tags.

**Show stash prices automatically** defaults ON. Open Ctrl+Shift+N to turn it off;
the choice is saved in your profile as `stashninja.automaticPrices`. The optional
menu also provides price profiles, **Highlight**, **Auto-calibrate**, and manual
calibration. Failed Auto-calibrate attempts retain the last good calibration and the loaded page’s tags; only the status changes. Foreground loss and page changes still hide tags normally. Its page list follows automatic recognition. For a manual override,
select a page and click **Load selected tab**. This pins the page even with automatic
prices enabled, until the header changes or the stash closes; automatic mode then
resumes. Currency pages are distinguished by the highlighted sub-tab button, without teaching. An ambiguous highlight is not accepted as a currency page. The status shows the automatic page, score and margin for reporting issues. Closing
the menu does not stop the automatic watch.

Supported pages: Currency General, Atlas and League; Fragment General; Delve,
Essence, Blight and Delirium. Unknown/unpriced cells have no tag. Tags describe item
types, not quantities or ownership. See the [coverage guide](../data/stash-layouts/poe1/README.md).
Prices default to the current softcore challenge league excluding SSF/Ruthless,
with Standard as fallback; set `prices.league` to override. Stale or incomplete
prices are labelled.

If recognition cannot confidently identify the grid, tags stay hidden. Move
covering windows away, or disable automatic prices and select the page manually.
**Auto-calibrate** performs a wider grid search. Automatic first-use detection uses stricter score and scale checks; if it cannot confirm the page, try this menu action or manual calibration. As a fallback, select two distinct
**empty** cells far apart diagonally, click **Calibrate**, then click their centers
with no item held. These clicks reach the game; Esc cancels. Calibration is local
and specific to the layout and resolution. A separate physical **Highlight** click
pastes one search; loading prices never sends input.

The watch checks a small header strip every 450 ms while the game is foreground,
pausing during calibration. Currency and Fragment also check their sub-tab strip
with tags visible. These checks compare against the baseline captured when the page
was identified. A stable page never causes a periodic full read or recurring blink.

When no page is identified, sparse border checks at the saved calibration or default
fit compare dark inner-slot bands, normalized against their neighboring pixels, and gate larger reads; they must score at least 12.5 before a full search starts.
After the first immediate check, unidentified scenes are checked no more often than
every 1.8 seconds, even when the moving world changes the header every tick. The
header itself stays on its 450 ms schedule. An already-calibrated page reopening
from an unidentified scene normally waits up to one 1.8-second admission interval;
a change from an identified page is noticed on the next 450 ms header/sub-tab check.
First use needs at least two identification ticks: allow roughly 3.6 seconds for
two admission intervals, or 5.4 seconds if a third tick is needed. Capture, scoring
and price loading add time. Immediate first checks can finish sooner; missing
samples or search cooldowns can take longer. Previous-page tags and Highlight are
unavailable while the new calibration is being confirmed.
Qualifying uncalibrated candidates can receive one fully admitted wider-search batch per header
baseline (at most two pages), with a separate 30-second cooldown for each page.
A fully admitted batch requires a real header change to retry. If a top candidate
is still cooling down, the baseline stays available for later attempts; each page
can still be searched at most once per 30 seconds.
The sparse pre-check only covers a ±12 px band around predicted borders. For larger
first-use offsets, use Ctrl+Shift+N → **Auto-calibrate** or manual calibration.
Tags can hide during a genuine page-change capture/search. Unknown after a change
hides tags immediately; otherwise missing or blocked baseline samples must remain
Unknown for about one second. Screen checks never send input.

Only game-client pixels are captured, using GDI BitBlt, with no DXGI capture,
DirectX/Vulkan hooks or Windows.Graphics.Capture. Use Windowed Fullscreen;
exclusive fullscreen may not capture. Screen checks never send input or obtain
an IntentToken. Samples never leave the PC and screenshots are never stored.
Only calibration and optional reduced references are saved locally; no game images
ship. **Remember this page** and **Forget this page** remain optional local-reference
controls for compatibility; automatic identification does not use taught references.

### Shop repricing / vaalasync — omni

Vaalasync is the shop repricing and local price-history tool.

There is no separate default key. Tap CapsLock over an item with a `~b/o` or
`~price` note to show price buttons. Async listing mode is the default:

1. Open the game's **Set Item Price** dialog yourself and focus its amount field;
   the current value is pre-selected.
2. Click one ExileSight price button. It shows amount + currency and sends only
   Ctrl+A, Ctrl+V of the amount, with no Enter or clicks.
3. Pick the currency in the game's dropdown, then click **LIST ITEM** yourself.
   ExileSight's currency reminder is display-only.

Async amounts must be positive whole numbers, because the game's amount box takes
only whole numbers. Suggestions round to whole numbers;
custom amounts must already be whole numbers. Display and paste use the same
invariant format, without thousands separators and with a dot for decimals.

For an unpriced item, hold CapsLock and choose **Set listing price…**. Only the
custom amount is available, seeded from a poe.ninja estimate when known. The
panel says **No current price**; an estimate is not a current listing price.

For legacy note mode, set `features.sections.vaalasync.listingMode` to `"note"`
in your active profile, then restart. Set it to `"async"` to return to the default.
You can also use **Use legacy note mode** under **History / custom price / record
purchase**; this also applies after restart. In note mode, open the game's note
box yourself, then click one price button to paste the full note with Ctrl+A,
Ctrl+V, Enter. Note mode allows decimals.

Read the item with omni again before another repricing attempt. If the result
says the price was possibly set or the paste was unconfirmed, check it in game
before trying again.

**History / custom price / record purchase** opens the local history tools.
Whisper matches are possible sale evidence, not confirmed sales; buyer names and
raw whispers are not saved.

### Macros — F5 and F6

Tap and release **F5** within 200 ms for `/hideout`; **F6** sends command 0,
`/dnd` by default. **F8** is another default `/hideout` shortcut.
Hold F5 or F6 longer, then release to open the travel or macro wheel. Opening the
wheel sends nothing; click a slot to send one command. Use **Edit commands** in
its center to change commands. Blank slots are disabled. Travel starts with
`/hideout` and `/menagerie`; the macro wheel starts with `/dnd`.

### Search strings — F9

Press **F9**, choose the vendor/UI manually, then click a saved search to highlight
matches once. Scroll over a multiline search to choose another line without
sending input. Esc, Close or clicking outside dismisses it. Custom searches live
in `features.sections.searchstr.vendors` in the profile; the
[search-string reference](../src/ExileSight.Features.SearchStrings/README.md)
includes an example. Invalid or oversized searches are disabled, not truncated.

### Timers — hold Tab; Ctrl+Shift+T to edit

Hold **Tab** for the passive timer list. Tab still reaches the game.
**Ctrl+Shift+T** toggles the editor: add named countdowns or stopwatches, then
start, pause, reset or delete them. Click a row to select it even while timers
are running; selection does not take keyboard focus from the game. Names use the
panel's mouse keyboard.
Optional completion toasts never send input. Running timers include time while
the app is closed; pausing stops them.

### Map tracker — Ctrl+Shift+M; Ctrl+Shift+K for kills

Open history with **Ctrl+Shift+M** and explicitly select the recording league.
The picker lists current poe.ninja leagues, with the current softcore challenge
league first. It shares the price service's cache and at least one-hour request
throttle. If the list is unavailable or stale, a note appears with the saved league
plus Standard and Hardcore. Your saved league stays selected. Type SSF/Ruthless
league names yourself; poe.ninja does not list them.

Confirm your character in the viewer for death counting; names offered from the
log are only candidates. Runs begun before confirmation have unknown deaths.
The tracker follows new map instances from the log, recording time, portals and
recognized mechanics. Copy a map with a short omni press before entering to attach
its mods to the next new map instance.

Press **Ctrl+Shift+K** for one `/kills` request. Take samples near the start and
end of the run; without both, kills stay unknown. They cover the sampled interval.
You can add notes, filter history and export the filtered rows to CSV. A storage
warning can stop recording for a league while retaining its saved history.

### Map info — omni on a map or Expedition Logbook

Tap CapsLock to show map properties and ranked modifiers. Click a known modifier
to cycle its personal danger rating from 0 to 3; higher ratings sort first.
Unknown modifiers remain at rank 3. Esc, clicking outside or leaving the game
closes the panel. There is no default hold key. You may bind `mapinfo:hold` to
show the last copied map while held; Tab already belongs to timers.

### Events — Ctrl+Shift+E for settings

Log-based notifications cover mercenary approaches, the Nameless Seer, Reflecting
Mist and discoverable hideouts when their data pack is available. Open settings
with **Ctrl+Shift+E**. Hideout alerts default off; the other shipped definitions
default on. Toasts last five seconds. Enabling an alert does not replay old events.

### Act guide — Ctrl+Shift+F10 / F11 / F12

**Ctrl+Shift+F10** toggles the guide; **Ctrl+Shift+F11** goes back and
**Ctrl+Shift+F12** goes forward. Zone changes can advance it automatically.
Use **Reset run** before starting another character: the guide shares one default
profile. League-start and bandit choices, and importing a custom JSON guide, reset
progress and timers. Import/export use file dialogs. Guide timing pauses on AFK
or after 120 seconds without a zone change, level-up or manual navigation, so a
long fight can appear idle. Progress persists; restart begins timing paused.

## Rebinding keys

There is currently no tray key-binding editor. Choose **Open settings folder**,
then **Exit**. Back up your profile JSON before editing. `profiles.json` identifies
`activeProfileId`; edit `<activeProfileId>.json` beside it (initially `default.json`).
The Debug panel's Settings section also identifies the loaded source.

In the profile's `hotkeys` array, edit an existing entry with the same `id`, or
add an entry to override a feature default. For example, this array entry moves
omni from CapsLock to F4:

```json
{ "id": "omni.press", "key": 115, "modifiers": 0, "block": true,
  "context": "gameFocused", "featureCommand": "omni:press", "enabled": true }
```

Keys are decimal Windows virtual-key codes: F4 = 115, F5 = 116, F6 = 117,
F9 = 120, Tab = 9, CapsLock = 20. Modifiers are numbers: none = 0,
Ctrl = 1, Alt = 2, Shift = 4, Windows = 8; add them for combinations
(Ctrl+Shift = 5). `block: false` also passes the key to the game.
Set `enabled: false` on that same ID to disable it; simply deleting an override
restores the feature default. Keep unrelated entries and properties intact.

Restart to load the edit. Invalid or conflicting bindings are reported in the
Debug panel; the first configured binding wins a shared chord. Overlay selection
commands cannot be turned into hotkeys. To reassign Tab to map info, disable or
move `timers.hold` first, then add a binding for `mapinfo:hold` with a new unique ID.

## Privacy and local files

The app's network requests go only to poe.ninja for prices/leagues and GitHub for
updates/data packs. Price and pack requests are cached/throttled for at least an
hour. Item-menu links open separately in your default browser, including wiki,
crafting and trade-search sites. There are no in-app Path of Exile API requests.

Under `%APPDATA%\ExileSight`, the app writes settings/profile JSON, downloaded
packs and price caches, request timestamps, and `logs\actions.log` for action
audit results (including slash-command verbs, without message bodies). Feature folders under `poe1` hold equipped snapshots, map ratings,
map history/notes, shop history, guide progress/custom guides, timers and stash
calibration. Recovery can leave backup or corrupt-file copies. CSV and guide
exports go to paths you choose.

Diagnostic logging excludes copied item and chat bodies. Gear comparison persists
a character name only after confirmation, plus numeric equipped snapshots.
Shop history retains item names and price records. Map history and CSV omit
character names; your own notes and imported/custom text can still contain personal
information. Review files before sharing them. The app never edits your game log.

## Troubleshooting

- **Overlay missing:** focus the game and use Windowed or Windowed Fullscreen.
  Check that `overlay.enabled` is true. In the tray Debug panel, inspect game-window,
  overlay, settings and input status. Try a fresh hotkey press over a supported item.
- **Key seems ignored:** the Debug panel's input section shows timing details
  without copied item or chat text. Include these details with the key and steps
  when reporting a missed press.
- **Wrong size or position / DPI:** overlays follow the client area and monitor DPI.
  `overlay.scaleOverride` replaces automatic scaling; set it to `null` to restore
  automatic sizing, then restart. Recalibrate stash tags for a changed resolution.
- **Log missing or not updating:** run setup again and select the actual local,
  readable log. Start the game so it creates the file. Check log diagnostics for
  missing paths, unavailable drives or refused network locations. Reading starts
  at the end of the file, so old lines are not imported; change zones to test it.
- **Missing item details or notifications:** check data-pack diagnostics. Let packs
  load, then copy the item again. Unknown values are not guessed. Stash tags also
  require a supported measured layout, which is absent from this version.
- **Antivirus or SmartScreen warning:** CI and 1.0 release installers are unsigned
  (D-098), so Windows SmartScreen and “unknown publisher” prompts are expected.
  Check the build's origin before running it. Keep protection enabled; if it blocks
  the build, include the detection name and build version in a bug report instead
  of adding a blanket exclusion.

## Reporting bugs safely

Include the build/version, Windows version, display mode, resolution/DPI, feature,
keys pressed, steps to reproduce, and expected versus actual behavior. Review
screenshots and diagnostic excerpts for personal information before attaching them.

For a game-log problem, copy only the relevant lines into a separate excerpt, then
use **ExileSight-RedactLog-win-x64** from the build artifacts. In a terminal in its
extracted folder:

```powershell
.\ExileSight.RedactLog.exe "excerpt.txt" --output "redacted.txt"
```

Or, from the repository with the .NET SDK:

```text
dotnet run --project tools/ExileSight.RedactLog -- excerpt.txt --output redacted.txt
```

Use a new output filename: the tool refuses to overwrite an existing file.
It replaces recognized player names with pseudonyms, removes chat/whisper bodies
and unknown lines, and retains supported event details. Open and review the result
before sharing; automatic redaction cannot guarantee that arbitrary text is safe.
Avoid `--keep NAME`, which deliberately retains that name. Share the redacted
excerpt rather than your full `Client.txt` or settings folder.
