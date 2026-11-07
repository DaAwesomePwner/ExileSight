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
MapWorlds instance IDs identify maps, excluding Vaal side areas and Abyss.
Only recognized side areas (Vaal, Abyss, Incursion, Harvest) reached from an
active map/side area remain associated. A trip from hideout into another activity
cannot resume the map.
Generated hideout/town IDs, HeistHub and settlement IDs pause elapsed uptime;
"You have entered" names never change pause state. Returning from a safe area to the
same seed counts another portal; side-area returns do not. NPC mechanics are
counted only inside the original instance, avoiding hideout/side-area dialogue.

Each league has a hashed JSON-lines filename, capped at 2 MiB. At capacity the
writer becomes read-only with a visible warning; existing rows are never evicted
or overwritten by rotation. Schema-1 unknown fields survive updates. A corrupt
history moves to a single `.corrupt` file; newer schemas remain untouched.

State changes enqueue snapshots on a dedicated, coalescing writer (at most 256
pending runs). Ordinary log/chat events do not write; active duration checkpoints
occur at most once per 60 seconds of observed map time. No disk I/O holds the
tracker/omni lock or runs on log delivery. Exit drains pending writes. A crash
may lose the last minute of timer progress; recovered active rows are incomplete.
Write failure/queue saturation visibly disables recording rather than dropping
saved rows. The viewer reuses its existing window and updates storage status.

Confirm your own character in the viewer each session. Identity exists in memory
only; unknown identity means no deaths are counted (the viewer says untracked).
Other players' deaths and deaths outside the map/associated side areas are ignored.
Source changes, gaps, detected restarts and recognized logout/login diagnostics
clear identity and close the run incomplete at its last known timestamp. A new
map generation is required to start recording again. Logout diagnostics are
recognized only on client Unknown events, never chat. If the client produces no
logout/restart signal, the log alone cannot establish an offline boundary; real
Windows session recordings remain necessary to validate diagnostic coverage.
No log/chat bodies or player names are stored.
User notes are limited to 4096 characters. CSV exports only filtered rows, at a
path explicitly chosen in the viewer, and escapes spreadsheet formulas.

Behaviour provenance: Exile-UI (MIT), modules/map tracker.ahk and modules/client log.ahk.
The synthetic fixture maptracker-session.txt contains authored events and no
real player data (D-044). Windows UI/in-game verification is still required.
