# Map info

Omni-copy a map or Expedition Logbook to open the map card, beside the cursor or
at the configured docked position. Ratings have three states: unrated (never
clicked), explicitly rated 0–3, and unknown (no matching map-data ID). Unknown
mods cannot be rated. Clicking a known mod cycles unrated → 1 → 2 → 3 → 0 → 1 … .
Rows sort as rated 3, unknown, rated 2, rated 1, unrated, then rated 0, preserving
game order within each group. Only explicitly rated-0 rows fold; click the fold
row to reveal them. Unrated rows always show with dashed pips. The first-time
hint appears when the map has no explicit ratings; otherwise the danger band
counts only rated-3 and rated-2 mods.

Quantity, rarity and pack size turn teal at or above user thresholds, defaulting
to 70%, 40% and 25%. Unknown values show `?`. `MapInfoSettings.Load`/`Save` stores
nonnegative percentage thresholds in the mapinfo feature's `settings` section
of the active profile; missing or invalid settings use defaults. Esc, clicking
outside, or losing the game foreground closes the card.

Optional hold: bind `mapinfo:hold` in hotkey settings (prefer pass-through). It
shows the last omni-copied map/logbook until the matching key release. No default
binding is installed: timers already owns the default Tab hold. To use Tab for
mapinfo, disable/reassign `timers.hold` first. This command never copies an item
or sends game input. No map is persisted between application sessions.

Ratings live in the feature directory's `ratings.json`, keyed by stable pack ID.
Writes flush then atomically replace the file. Limits: 4096 entries, 128 KiB,
128 characters per ID. Unknown IDs are retained. Corrupt, oversized or unfamiliar
schemas/fields are preserved read-only with a panel notice; repair the file and
restart. Failed saves keep the previous rating.

Matching behaviour is derived from Lailloken/Exile-UI `modules/map-info.ahk`
(MIT), commit 67d1a3637e97b791072129f0cf890e22f385b237. Data comes exclusively
from the scoped `poe1/mapmods` reader. Indexes are reused while its immutable data
snapshot remains unchanged. Matching strips numeric ranges and suffix markers,
prefers complete multiline patterns, and respects advanced affix groups.
Maps match explicit affixes and enchantments against the pack; map
implicits and “also apply to rarity” informational lines are omitted. Normal and
unique maps have no ranked affix rows. Expedition faction labels retain their
immediately preceding area and NPC; explicit logbook modifiers remain global.
The test pack snapshot comes from that same commit's `data/english/map-info.txt`;
`synthetic-*` fixtures are authored examples under D-044, not client recordings.

Windows owner verification: open maps and multi-area logbooks; click through all
ratings and restart; verify foreground retention, Esc/click-away dismissal,
scrolling with long mod lists, hold/release, and monitor/DPI changes.
