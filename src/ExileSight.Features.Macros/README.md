# Macros

Default bindings: blocked F5 (`macros.travel`) and F6 (`macros.macro`).
Persisted bindings with those IDs win, including disabled bindings.

A release with the same press ID and down timestamp, within 200 ms, spends the original key-down token on /hideout (F5)
or the separate command 0 (`Command0`, F6). The Action Gate still enforces its 1.5 s expiry.
The release never mints or authorizes another action. A longer press opens the wheel
**on release**, without spending that token. The wheel is centred on the cursor captured
at release and clamped to the game client.
Holding, repeats and missing releases never send commands. A physical click
selects one slot using the host's paired overlay-click token. Esc, click-away,
a new press or loss of overlay visibility dismisses the wheel. No retries.

The wheel has eight positions, starting at the top and proceeding clockwise.
Command 0 is separate from the eight macro-wheel slots (nine commands total). Defaults are /dnd in command 0 and macro wheel slot 0,
and /hideout, /menagerie in travel slots; other slots are disabled.
Travel names are taken from Exile-UI modules/macros.ahk (MIT).
No unverified optional destinations are shipped.

Choose **Edit commands** in the wheel center to edit both lists.
Settings are stored in Features.Sections.macros.commands as an object with
eight-entry Commands and Destinations arrays plus a separate Command0 string.
Legacy settings without Command0 copy Commands[0] into Command0, retaining all
eight wheel slots. Explicit Command0 values, including blank, are preserved. Empty strings disable slots.
Every entry is a single line of at most 400 UTF-16 code units, with no control
characters or Unicode line separators. Destinations require a leading slash;
macro slots also accept user-authored chat text. Malformed persisted settings
disable slots instead of substituting commands. Short F5 always means /hideout.

Accepted key-downs use PressRouter.DeferUntilRelease; they do not report an
action success. The host reports the actual short-release gate result, preserving
failure, expiry and clipboard/audit metadata. Long releases and blank command 0
produce no action report. Physical wheel clicks report their actual gate result,
including click-authentication failure. Ordinary Omni diagnostics are unchanged.

Only SendChatCommand is declared. There are no network calls, log subscriptions,
timers or input synthesis in this feature. Presentation and settings windows
belong to App; native overlay styling/scaling and click authentication remain
host-owned. Wheel versions prevent delayed clicks from selecting a new wheel.

Windows manual validation: verify F5/F6 short/held releases, each slot, empty
slots, Esc/click-away, focus loss, overlapping overlays and mixed-DPI monitors.
Confirm exactly one audit entry per short press or successful physical selection,
and none for held/lost releases, keyboard/programmatic clicks or dismissals.
