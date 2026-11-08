# Equipped comparison

Provenance: Exile-UI (MIT), modules/item-checker.ahk, comparison at 865–1100
and Iteminfo_GearParse at 2354–2613. The UI uses explicit omni menu actions
instead of inventory pixels (D-006). No game actions or network access.

Long-press over equipment to set or clear its slot. Rings and one-handed
weapons offer explicit side choices. Bows preserve a quiver; quivers pair only with bows. Comparison and equip share
one replacement rule: deltas sum every overwritten or evicted item, including
incompatible items in the other hand. Preserved items are excluded. Other two-handed weapons replace both hands;
equipping an incompatible offhand replaces a stored two-handed weapon. Comparisons against
both hands sum their item stats; weapon DPS is not a character damage estimate. Chaos DPS has its own row and is
excluded from elemental DPS.

Level-up names are only candidates: the last five stay in memory until exit.
Use character confirms one; different level-up names never change the active
profile. Use default profile selects the visibly labeled fallback. Only the
confirmed active name is persisted. Profile filenames use a SHA-256 encoding
to avoid invalid Windows filenames and sanitization collisions.

Data lives under poe1/gearcmp. Versioned JSON is flushed to a same-directory
temporary file before atomic rename. Invalid JSON files are quarantined; newer schemas remain untouched and read-only,
and transient read failures disable writes for the session until restart; if that
fails, writes to the original are blocked. Restart restores the active profile
and equipped snapshots. Snapshots contain class and numeric stats, not copied
text, chat, item notes, or unconfirmed names.

Copied defence properties are authoritative final local values. ItemInfo's
current analysis exposes defence percentiles, not additional numeric defences.
Hybrid IDs were verified against a locally generated pinned pack at RePoE
`d0bc438357d849bb93ad22f3db0b12938aaf6da1` (not committed). Its all-stat IDs are
`additional_all_attributes` and `base_resist_all_elements_%`.
Global stats use the injected Core StatMatch contract via App composition,
so no feature references another feature. Exact stat-ID mappings keep leech/regeneration separate from flat life totals.
Unmatched modifiers conservatively
suppress global comparisons; ambiguous/range-only stats are omitted rather
than guessed. Re-mark gear after loading missing data packs or updating supported stats;
stored numeric snapshots cannot reconstruct modifiers omitted by an older version. All test copied
items under the owned test project's Fixtures directory are synthetic (D-044).

Windows owner check: verify the long-press menu scrolls, slot actions persist,
character confirmation remains explicit, and positive/negative tooltip rows
render in the existing High/Low theme tones.
