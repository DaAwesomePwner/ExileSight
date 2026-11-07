# Item info

Compose ItemInfoFeature with PackItemInfoData using the loaded bases, mods and stats
pack contents. The parameterless feature supports missing packs. Registration publishes
an ItemInfoViewModel and a priority-100 omni handler; the router owns copying.
No independent hotkey or input action is registered.

Current changes notify the UI. The future tooltip host calls Close for mouse movement
or Escape, and marshals notifications to its UI thread. No WPF dependency is needed here.

Settings live in the feature-scoped profiles section. ItemInfoSettings.Save validates
at most five profiles. Within the selected profile, literal case-insensitive substring
rules for the item class precede global rules; first matching rule wins. Implicit rules
are separate. InvertedLines selects exact copied lines for reversed roll endpoints.

Advanced-copy headers are authoritative for tiers and affix counts. Hybrid lines share
a count but each has a roll. Missing evidence remains nullable, including plain-copy
counts, unmatched affix requirements and unique drop tiers (D-037).
Matching requires the exact base class/name, its domain (including flask/jewel domains),
positive spawn-tag overlap, quoted affix name, type, stat ranges and every template
fragment. Pack order breaks ties.

Defence estimates reverse multiplicative quality, additive local increased defence and
local flat defence. Unidentified items, ambiguous or unmatched lines, unsupported
defence stats, or inferred percentiles outside 0–100 remain unknown. This conservative
policy can hide a percentile for an unrelated unknown modifier. Display rounding may
produce fractional estimates. In-game validation remains a Windows owner check.

Synthetic fixtures and unit tests are evidence of logic behavior, not recorded client
compatibility. Ported algorithms name Exile-UI source locations in their file headers.
