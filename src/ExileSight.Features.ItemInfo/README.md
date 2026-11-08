# Item info

Compose ItemInfoFeature with PackItemInfoData using the loaded bases, mods and stats
pack contents. The parameterless feature supports missing packs. Registration publishes
an ItemInfoViewModel and a priority-100 omni handler; the router owns copying.
No independent hotkey or input action is registered.

Current changes notify the UI. The reusable card host calls Close for mouse movement
or Escape, and marshals notifications to its UI thread. No WPF dependency is needed here.

Settings live in the feature-scoped profiles section. ItemInfoSettings.Save validates
at most five profiles. Highlights and inversion share an exact, case-insensitive key:
numbers, ranges and known copy suffixes are removed and whitespace collapsed. Within
the selected profile, class rules precede global rules; first matching rule wins.
Explicit, implicit and enchant rules occupy separate categories.

Advanced-copy headers are authoritative for tiers and affix counts. Hybrid lines share
a count but each has a roll. Missing evidence remains nullable, including plain-copy
counts, unmatched affix requirements and unique drop tiers (D-037).
Matching requires the base name and normalized RePoE class, its domain (including
flask/jewel domains), quoted affix name, type, stat ranges and every template fragment.
The first matching spawn-weight tag decides eligibility, including zero exclusions.
Parsed influences add generic and RePoE slot tags (Redeemer=eyrie, Hunter=basilisk,
Warlord=adjudicator). Exactly one slot comes from the normalized item class, not
generic base tags: two-hand sword/axe/mace use 2h_*; Rune Daggers use rune_dagger;
Warstaves use warstaff. Pack order breaks ties.

Defence estimates reverse local flat defence and the sum of quality and local
increased defence. The inferred integer base must reproduce the rounded displayed
property; neighbours and range endpoints are tried before returning unknown.
When a multiplier at or below one permits multiple integer bases, the percentile
remains unknown instead of selecting one of those indistinguishable rolls.
Local translations take precedence over identical global text on equipment. Only
local defence candidates require exact values; unrelated rounded stats are ignored.
Unsupported local defence stats or unmatched defence text remain unknown.

Await PackItemInfoData.PrepareAsync before registering the adapter to keep compilation
off the first-copy path. Core's StatText.PrepareAsync warms loaded translations on a
worker thread. Neither method performs IO or game actions.

The review probe generates packs outside git with DataGen's default pinned SHA
d0bc438357d849bb93ad22f3db0b12938aaf6da1, then runs the LocalGlobalCollisions and FrenzyChargeResolvesTo84 tests with
EXILESIGHT_ITEMINFO_REVIEW_PACKS pointing to that output directory. These tests always
run with synthetic data in ordinary CI; the manual probe substitutes generated packs.
The export confirms local/global armour, evasion and ES text collisions, local_ward,
and the three-defence local hybrid increase stat.

Synthetic fixtures and unit tests are evidence of logic behavior, not recorded client
compatibility. Ported algorithms name Exile-UI source locations in their file headers.

The app registers the reusable card host through FeatureViews. Gear comparison
registers IItemCardComparisonProvider via IFeatureContext.RegisterItemCardComparison.
Providers contribute a WPF-free ItemCardComparison in registration order, receiving
a ParsedItem and no IntentToken. Null comparisons are omitted and provider failures
are logged and isolated. The old text-section contract has been removed.

App startup reads bundled/cache packs on a background worker and publishes the adapter
only after preparation. Refresh runs after local loading and a five-second delay, then
hourly after first-run setup. Attempts and their known results are persisted per
pack/tag before network access, including failures and interrupted attempts. Invalid
records use their last-write timestamp for a bounded one-hour retry; future attempt
timestamps are corrected and persisted. A failed pack refresh logs its exception type
and does not prevent the other packs or the local reload from running. Unchanged
pack versions and content hashes reuse the prepared adapter. The configurable composition argument defaults to poe1-packs-1.0.0; no releases
were published when this wiring was implemented. PackLoader masks download failures,
so diagnostics report an attempted check and retained local availability, not download
success. The app card shows pack availability notices until the next user copy
rebuilds it. A failing status provider does not discard the card. Each press
calculates item info once and builds an ItemCard from that result. The host uses
CardTheme and shared primitives; it has no feature-specific theme dictionary.
Dispatcher open and DPI callbacks close/release leases on failures.

### Structured card

`ItemCardBuilder.Build(result, data, settings, comparison)` produces the passive,
WPF-free card. The calculator supplies cached per-line matches on ItemInfoResult;
the optional data adapter is a fallback for results constructed without that context.
Settings default to the validated settings retained on ItemInfoResult; an explicit
settings argument can override card visibility. Recalculate first when changing
highlight rules. The registered comparison providers supply GearCardBuilder's Core
comparison contract without feature-to-feature references.

The App's pure ItemCardView builder renders the model. It measures each section
and mod row at the card's inner width and computes the height cap from those
measurements, dropping Compare, then Slots/base, then trailing rows with a count.
Base percentiles are right-aligned beside the slots; unidentified items show them
on the subtitle line. Placement and dismissal remain in the reusable window host.

Rows preserve advanced-copy order; `GameLineStart` is zero-based in the parsed
mod list and `GameLineCount` spans a whole advanced-header group. Labels combine
ordered stat IDs from all its lines. Ambiguous stat signatures use the stripped
text fallback. Hybrid roll position is the mean of its known line positions;
unique average roll is the mean of known mod-row positions. Fixed unique lines
are recognized only with advanced headers (plain copy cannot prove a fixed roll).
A hybrid counts once in the verdict; if its lines conflict, Avoid takes precedence.

`LevelKind` distinguishes blank/not-applicable from unknown. Null pip counts or
maxima must draw unknown pips; `Open` never invents an open slot. Defence, slots,
comparison and countable rows remain separate for view height handling. The four
`Show*` settings default on, including when loading older profile JSON.

The name model retains `FullName` for every rarity. Views use `DisplayName` (unique
name, otherwise base type, otherwise full copied name) so plain magic copies never
lose their title. `CardSlotKind.None` means a blank unique slot marker; `Unknown`
means a question mark. White items omit the slots section. Copies without any mod
rows cannot reveal their copy mode and never request advanced copy. Fixed unique
implicit/enchant rows retain their `imp`/`ench` kind pills.

`ItemInfoResult.Settings` and `Matches` are transient card-building context: they are ignored by JSON
serialization and by result equality/hashing. The six analysis fields retain their
original record comparison semantics (including list identity). This keeps frozen
calculator JSON comparisons independent of UI settings; recalculating highlights
still changes the analysis rows themselves. Enchants always use `CardSlotKind.None`
because they never consume a prefix or suffix slot.
