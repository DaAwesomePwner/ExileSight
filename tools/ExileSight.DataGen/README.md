# Data pack generation

Run `dotnet run --project tools/ExileSight.DataGen -- poe1 --repoe-sha <full-40-digit-sha> --out <directory> [--source-dir <local-export>]`.
Only base_items.json and mods.json are consumed. Local input hashes are recorded but local input provenance cannot be authenticated by the converter.
Never commit generated game data or publish release assets until the owner clears redistribution.

Schema 1 emits content, manifest, a local cache envelope, and SHA256SUMS. Content hashes cover exact emitted UTF-8 bytes. Pack version is the full source commit. `gamePatch: unknown` and Unix epoch `generatedAt` are explicit unknown sentinels: the export does not provide trustworthy patch/time metadata, and wall-clock timestamps would break reproducibility. Input bytes and SHA fully determine output.

Tier estimates group by domain, generation type, groups and stat IDs within an item class and complete base-tag set. The first matching spawn weight determines eligibility, including zero-weight exclusions. Only ordinary prefix/suffix mods are ranked. Descending required level gives dense ranks (equal levels tie); these are level-order estimates, not official tiers or guaranteed power rankings. Stat IDs serve as unresolved text-template lookup IDs; translated text is not bundled. Unique drop tiers remain null/unknown.

Loader startup calls `ReadLocalAsync` only. A caller may schedule and await `RefreshAsync` later; unavailable packs do not prevent startup. Cache and fallback directories are supplied by the host. Refresh checks are throttled for at least one hour, including failures; ETags are retained for the loader lifetime. Cache envelopes replace atomically. Use an injected HttpClient with `AllowAutoRedirect = false`: the transport follows and validates redirects itself. GitHub delivery hosts outside the two approved hosts fail closed.
