# Map info

Omni-copy a map or Expedition Logbook to open the cursor-adjacent panel. Quantity,
rarity and pack size are highlighted. Click a known modifier to cycle danger
0 (neutral), 1 (caution), 2 (danger), 3 (highest); highest ratings sort first.
Unmatched text is always shown as unknown at rank 3 and cannot be reclassified by
clicking. Esc, clicking outside, or losing the game foreground closes the panel.

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
