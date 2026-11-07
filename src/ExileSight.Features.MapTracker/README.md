# Map tracker

Ctrl+Shift+M opens the local history window. Select/confirm the recording league
before mapping; the list comes from poe1/leagues, sorted by pack start date.
You can enter a league missing from the historical pack. No unknown end date is
interpreted as evidence that a league is current. The selection persists.

Ctrl+Shift+K sends exactly one `/kills` through the Action Gate. Defaults yield
to persisted bindings. No log, timer, copy, export or viewer click sends input.
Take a start and end sample within each run; their difference is recorded.
Without both samples kills remain unknown, and character-count resets clear
the baseline. These are sampled kills, not an estimate of the whole map.

Short omni-copy of a map captures its mod text for the next new map instance.
The immutable host observation carries neither input authority nor raw clipboard
text. Long omni menus do not route and therefore do not produce this observation.
MapWorlds instance IDs identify maps; other areas remain associated side areas.
Hideout/town instance IDs pause elapsed uptime. Returning from a safe area to the
same seed counts another portal; side-area returns do not. NPC mechanics are
counted only inside the original instance, avoiding hideout/side-area dialogue.

Each league has a hashed JSON-lines filename, capped at 2 MiB with one bounded
`.previous` rotation. Atomic rewrites retain active runs as incomplete for crash
recovery. A corrupt history moves to a single `.corrupt` file. Newer schemas and
oversized histories remain untouched and read-only. Gaps/source changes close
runs at their last known timestamp. No log/chat bodies or player names are stored.
User notes are limited to 4096 characters. CSV exports only filtered rows, at a
path explicitly chosen in the viewer, and escapes spreadsheet formulas.

Behaviour provenance: Exile-UI (MIT), modules/map tracker.ahk and modules/client log.ahk.
The synthetic fixture maptracker-session.txt contains authored events and no
real player data (D-044). Windows UI/in-game verification is still required.
