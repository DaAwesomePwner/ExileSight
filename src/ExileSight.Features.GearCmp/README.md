# Equipped comparison

Provenance: Exile-UI (MIT), modules/item-checker.ahk, comparison at 865–1100
and Iteminfo_GearParse at 2354–2613. The UI uses explicit omni menu actions
instead of inventory pixels (D-006). No game actions or network access.

Long-press over equipment to set or clear its slot. Rings and one-handed
weapons offer explicit side choices. Two-handed weapons replace both hands;
equipping an offhand replaces a stored two-handed weapon. Comparisons against
both hands sum their item stats; weapon DPS is not a character damage estimate.

Level-up names are only candidates: the last five stay in memory until exit.
Use character confirms one; different level-up names never change the active
profile. Use default profile selects the visibly labeled fallback. Only the
confirmed active name is persisted. Profile filenames use a SHA-256 encoding
to avoid invalid Windows filenames and sanitization collisions.

Data lives under poe1/gearcmp. Versioned JSON is flushed to a same-directory
temporary file before atomic rename. Invalid files are quarantined; if that
fails, writes to the original are blocked. Restart restores the active profile
and equipped snapshots. Snapshots contain class and numeric stats, not copied
text, chat, item notes, or unconfirmed names.

Copied defence properties are authoritative final local values. ItemInfo's
current analysis exposes defence percentiles, not additional numeric defences.
Global stats use the injected Core StatMatch contract via App composition,
so no feature references another feature. Unmatched modifiers conservatively
suppress global comparisons; ambiguous/range-only stats are omitted rather
than guessed. Re-mark gear after loading missing data packs. All test copied
items under the owned test project's Fixtures directory are synthetic (D-044).

Windows owner check: verify the long-press menu scrolls, slot actions persist,
character confirmation remains explicit, and positive/negative tooltip rows
render in the existing High/Low theme tones.
