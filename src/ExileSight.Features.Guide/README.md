# Guide

Passive guide using the host's parsed `poe1/guide` snapshot and ordered Client.txt feed.
No game actions, network requests, or character names are stored.

Defaults (persisted user bindings win): Ctrl+Shift+F12 = next, Ctrl+Shift+F11 = back,
Ctrl+Shift+F10 = toggle. These function-key chords avoid chat word-selection shortcuts
and the other features’ reserved defaults. Buttons are local actions; the overlay uses the shared NOACTIVATE host.
Import/export use ordinary user-selected file dialogs. Imported content is always plain text.

One visibly labelled default profile is shared by all characters. Reset run before starting
another character. League-start defaults to yes and bandit to none; changing either resets
progress/timers. Custom import also resets. The selected custom guide survives restart.
Schema-2 progress is a small atomic document (8 KiB read cap); custom JSON lives in a
separate SHA-256-named file capped at 1 MiB, written only on import. A legacy schema-1
embedded guide migrates once (legacy read cap 1500 KiB). Commit the custom file before
its progress reference; remove obsolete custom files only after progress commits.
Unreadable, unknown-schema or corrupt progress remains read-only and untouched.
An incompatible custom guide is disabled with a visible notice; default-guide progress
remains writable and uses the saved league/bandit choices. Remove custom guide discards
the disabled file. Import can replace it. Neither recovery requires editing files manually.

Only changed state is saved. Timer-only changes are checkpointed coarsely (60-second
background checks, minimum 60 seconds between timer-only writes). The visible panel
refreshes elapsed time without saving; hidden panels skip rendering and ticking except
when pending elapsed time needs checkpointing. Idle checkpoints do not rewrite files.
A crash can lose up to roughly two minutes of timer updates between checkpoints.

Timers use monotonic time, count only this session, pause after 120 seconds without a zone,
level-up or manual navigation, and pause on AFK. Restart starts paused. This is an activity
heuristic, not keyboard surveillance: a long fight can be counted as idle. Log loss/source
changes preserve the current step, clear area evidence, and mark totals uncertain. No replay
is requested at startup, avoiding double advancement of persisted progress. Pack updates
are adopted on restart so an in-session update cannot reinterpret page indexes; a version
change resets saved progress and timing with a visible notice.

Custom JSON uses Exile-UI's acts → pages → lines / {condition, lines} format. Imports are
limited to 1 MiB, 2,000 pages, 100 lines/page, 4,096 characters/line and depth 12. Conditions
are league-start and bandit. Area and quest references must resolve against the pack;
existing display placeholders are accepted. Dominant referenced area act identifies each
chapter, matching the pack generator. AreaGenerated IDs are used for the next AreaEntered
within 30 seconds; otherwise names resolve among the current page’s non-hint area
references, including cross-act travel. Current act breaks remaining name ties. Towns are
ordinary targets; hideouts never advance. Manual navigation suppresses auto-advance for 2 s.
Level hints remain display guidance. Hint lines `(hint)_` never define completion targets.

Behaviour reference: Lailloken/Exile-UI `modules/leveling tracker.ahk` at
67d1a3637e97b791072129f0cf890e22f385b237 (MIT); independent implementation.
Guide data comes from the attributed pack (Exile-UI / HeartofPhos exile-leveling, MIT;
RePoE world areas and gems). No upstream data is embedded here.

`guide-acts1-3.synthetic.txt` and `guide-cross-act.synthetic.txt` are authored synthetic log text (D-044), not a recorded game
session. Windows overlay/dialog and in-game validation remain a manual gate.
