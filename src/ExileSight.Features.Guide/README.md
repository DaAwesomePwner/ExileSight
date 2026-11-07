# Guide

Passive guide using the host's parsed `poe1/guide` snapshot and ordered Client.txt feed.
No game actions, network requests, or character names are stored.

Defaults (persisted user bindings win): Ctrl+Shift+Right = next, Ctrl+Shift+Left = back,
Ctrl+Shift+G = toggle. Buttons are local actions; the overlay uses the shared NOACTIVATE host.
Import/export use ordinary user-selected file dialogs. Imported content is always plain text.

One visibly labelled default profile is shared by all characters. Reset run before starting
another character. League-start defaults to yes and bandit to none; changing either resets
progress/timers. Custom import also resets. The selected custom guide survives restart.
Storage is atomic, capped at 1500 KiB; unreadable, unknown-schema or corrupt files remain
read-only and untouched. Up to five seconds of live timer updates can be lost on a crash.

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
within 30 seconds; otherwise names must resolve uniquely in the current act. Towns are
ordinary targets; hideouts never advance. Manual navigation suppresses auto-advance for 2 s.
Level hints remain display guidance. Hint lines `(hint)_` never define completion targets.

Behaviour reference: Lailloken/Exile-UI `modules/leveling tracker.ahk` at
67d1a3637e97b791072129f0cf890e22f385b237 (MIT); independent implementation.
Guide data comes from the attributed pack (Exile-UI / HeartofPhos exile-leveling, MIT;
RePoE world areas and gems). No upstream data is embedded here.

`guide-acts1-3.synthetic.txt` is authored synthetic log text (D-044), not a recorded game
session. Windows overlay/dialog and in-game validation remain a manual gate.
