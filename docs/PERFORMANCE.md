# Idle performance budget

The 1.0 gate applies to **idle during a normal session, including after item
checks and feature use**: average CPU **<1% of one core**, private memory
**<200,000,000 bytes**. The <=0.5% CPU and <=150 MB memory figures are headroom
goals, not additional gates. Windows private bytes remain authoritative.

CPU is `100 × delta process CPU seconds / elapsed wall seconds`, without division
by processor count. One occupied core is 100%. Memory is the maximum one-second
private-byte sample in decimal MB, not managed heap or working set. Sampling can
miss shorter spikes.

## Windows evidence

| Revision / run | CPU | Peak private MB | Result |
| --- | ---: | ---: | --- |
| a46cd40 / 42417510247 | 0.829% | 1541.30 | Memory failed |
| Baseline 8ee74d4 / 42419392581 | 0.285% | 1633.94 | Memory failed |
| 4ca85cc / 42419392581 | 0.960% | 246.35 | Memory failed |
| a76bed6 / 42422827112 | 0.908% | 222.66 | Memory failed; CPU margin 0.092 pp |
| Current round, one-shot report after measurement | Pending CI | Pending CI | Unverified |

Sources: [first run](https://github.com/DaAwesomePwner/ExileSight/actions/runs/42417510247),
[second run](https://github.com/DaAwesomePwner/ExileSight/actions/runs/42419392581),
[third run](https://github.com/DaAwesomePwner/ExileSight/actions/runs/42422827112).
These used Windows build 26100, four logical processors and a self-contained
single-file app. The third run's steady private memory was approximately 217 MB.

The second run had about 61 MB managed, 126 MB heap, 157 MB GC committed and
87 MB fragmentation. Its gen0/1/2 counts 15/14/12 were cumulative since startup.
The third run proves there were **zero collections during the idle minute**, with
989,096 allocated bytes. Its own snapshots showed 25 MB managed, 24.3 MB heap,
138.9 MB committed and only 0.02 MB fragmentation: about 115 MB committed but
empty, plus approximately 78 MB non-GC private memory. This explains the remaining
memory failure; ongoing GC does not explain the CPU regression.

## Retention and collection fixes

Real shipping packs reproduce large allocations without WPF, prices or bundle
extraction. Earlier fixes removed full JSON/base64 scratch buffers, repeated
validation and eagerly prepared regexes. Deferring full DTO copies only moved
retention to first item use and did not satisfy the session budget.

The current item adapter is compacted once during background pack loading using
already validated DTOs. It shares repeated strings, weights, stats and arrays,
drops unused wire fields, and keeps only prefix/suffix mods in loaded base domains
(the only mods FindMod can return). Construction pools are released afterward.
Guide views share identical immutable gem requirement maps and levels.

Literal candidates reject unrelated stat templates before regex construction;
regex/value/condition matching remains authoritative. Covers fragments are lazy.
The 512-entry matcher cache now has weak keys **and a weak eviction queue**.
Item-pack version/content replacement or removal clears both before Changed
notifications; source-only publication keeps them. Late old-version calls cannot
root obsolete variants. Tests cover eviction, invalidation and collectibility.
Shared pack views still parse once per version; scoped access, hashes and Changed
semantics are unchanged. Failed adapter construction retries on the next load.

The new real-pack equivalence test compares every fixture's FindMod results and
complete ItemInfoCalculator output against the frozen **4ca85cc adapter, calculator,
identity and defence helpers**. All 172 fixture files (plus the synthetic item)
match, including five successful mod groups. Successful
real-pack matches are required, including a synthetic Fecund Glorious Plate.
StatText has a separate frozen exhaustive-oracle comparison. No game packs are
committed. See also [matching/preparation guidance](../src/ExileSight.Core/DataPacks/README.md).

After a bulk load allocating >=32 MB, PackStartup performs one
`GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true)`.
[Aggressive explicitly requests decommit](https://github.com/dotnet/runtime/blob/v10.0.0/src/coreclr/System.Private.CoreLib/src/System/GC.CoreCLR.cs),
unlike the earlier Forced collection that left empty committed memory on Windows.
Both loading and reclamation explicitly use Task.Run; the collection action has
no awaits, so it cannot execute on the hook/UI thread even if a read delegate
completes there. GC can still pause managed threads globally during collection.
There is no periodic GC timer and no collection on an item check.

ConserveMemory=9 remains: earlier natural-GC probes ended at 193.46 MB committed
with policy 5 versus 105.41 MB with policy 9. No hard heap limit, RetainVM or
GC-affinity override is added. A prior 96 MiB hard-limit experiment failed to
load every pack. Policy changes are not used to conceal live retention.

## Real-pack session measurements and enforced ceilings

The production PackLoader/PackStartup/MatchLine/FindMod/ItemInfoCalculator path
runs with verified packs from run 42414705210. Local measurements below are
macOS ARM64/.NET 10.0.14, **not Windows private-memory results**.

| Measurement | Earlier evidence | Current Aggressive/background path |
| --- | ---: | ---: |
| First item latency | 534.0 ms at 4ca85cc | 22.0 ms |
| Full-GC live heap after session | 244.48 MB after first item at 4ca85cc | 39.88 MB |
| Committed immediately after startup load | 71.53 MB, background Forced comparison | 71.20 MB |
| Startup collection pause | 15.8 ms, background Forced comparison | 15.9 ms |
| Natural committed after fixture items | 37.32 MB, background Forced comparison | 37.32 MB |
| Natural fragmentation after fixture items | 8.50 MB, background Forced comparison | 0.108 MB |
| Committed immediately after refresh load | Not recorded in comparison | 91.03 MB |
| Refresh collection pause | 13.1 ms, background Forced comparison | 11.9 ms |
| Natural committed after refresh and more items | 31.83 MB, background Forced comparison | 34.50 MB |
| Natural fragmentation after refresh and more items | 0.032 MB, background Forced comparison | 0.029 MB |

The local comparison does not establish a decommit advantage for Aggressive on
macOS. Its Windows effect and CPU without diagnostic sampling require the next
CI run. Raw observations are in `tests/ExileSight.Perf.Tests/evidence/`;
`round3-background-forced.json` and `round3-aggressive.json` record that Debug comparison.
A Release repeat also passed: 37.34/31.82 MB committed after items/refresh,
70.22/91.10 MB immediately after loads, 16.9/11.8 ms collection pauses and
22.0 ms first use (`round3-release.json`).

The enforced probe now runs all fixture items, performs one same-version pack
refresh through the production coordinator (verified local fallback reads, no
network), runs the items again, and records committed bytes and fragmentation.
**No diagnostic forced GC occurs until after both natural session observations.**
Production bulk-load reclamation remains enabled because it is normal behavior.
The final full-GC sample still enforces <50 MB live heap increase; first use must
remain <500 ms. Both collection actions assert thread-pool execution and report
pause duration and committed-after-load bytes.

Immediate post-load and post-refresh committed bytes, and both natural session
committed readings, must be **<110,000,000 bytes**:
`200 MB private budget − 90 MB non-GC allowance = 110 MB GC ceiling`.
The 90 MB allowance conservatively covers the earlier 86–90 MB breakdown and the
latest ~78 MB. GC counters describe the last collection and are not current total
private memory. At 37.32 MB committed plus 90 MB allowance, the local session
estimate is 127.32 MB (72.68 MB margin); it is an inference, not a Windows pass.

## Idle CPU and diagnostic isolation

The new gate disables counter sampling, serialization and diagnostic writes
throughout the CPU interval. It requests **one** own-process snapshot only after
saving the final CPU/private-byte samples. The env-gated service waits on a
filtered FileSystemWatcher in the app's own directory; it has no polling timer.
No request can synthesize input, collect GC, alter settings or disable features.
Request/wait/cancellation behavior has portable tests. Ordinary installations
have no sampler. EXILESIGHT_PERF_MEMORY_REPORT=1 still enables optional periodic
owner diagnostics; the gate uses `after-measurement` instead.

Compared with baseline, new resource diagnostics are sampled only by a visible
debug panel, which is closed during the gate. The old gate's 15-second report
sampler is now excluded from the measured interval. WindowSource's matching-title
precheck avoids GeForceNOW process snapshots (third run: zero queries); TailReader
avoids redundant FileStream buffers and EOF reads. Quiet populated-log allocation
fell from 9,652,816 to 1,256,256 bytes per 1000 polls (87% less); empty logs fell
from 973,200 to 533,792. These allocation savings do not prove CPU causation.
Window title prechecks add enumeration work relative to baseline; their net CPU
impact has not been isolated. No new production periodic timer is added here.

The 10 Hz foreground heartbeat (HookForeground.cs:21) and 1 Hz repeat refresh
(HookHost.cs:62) remain safety timers: stalled UI must pass keys through and held
keys must respect Windows repeat settings. Guide 1 Hz and Timers 4 Hz view ticks
are deferred to a follow-up. Existing fixes retain 1 Hz empty-event polling and
pause hidden/minimized tracker status. CPU <=0.5% remains a goal; the latest
measured 0.908% included periodic diagnostics, and the new Windows result is pending.

## CI, local reproduction and owner checks

perf.yml runs on integration/1.0 pushes, PRs targeting integration/** and manual
dispatch. It publishes and bundles verified packs, seeds valid settings using
ProfileStore, creates an empty readable Client.txt and keeps all safety checks,
features and refresh services enabled. It settles 60 seconds, then measures at
least 60 seconds. Process exit, setup/error windows or either budget fail the gate.
The first-use/session probe and both equivalence checks run afterward with
`if: always()`, so their failure cannot hide the Windows report.

There is no external test item-check trigger without a real game/physical intent.
The Core/app-pack-path session probe is the authorized after-use fallback; full-app
post-use Windows private bytes still require owner validation. The gate makes no
Action Gate token and adds no safety bypass. Near-limit failures should be retained
rather than rerun until green; shared-runner variability is not yet quantified.

Reports, margins, session stages, GC pauses, TRX and distribution manifests are
uploaded as ExileSight-idle-performance and summarized in GITHUB_STEP_SUMMARY.
The temporary baseline is isolated under RUNNER_TEMP to preserve compiler
boundaries. Remove its marked workflow block after reviewed Windows comparisons
are recorded. Keep the current gates and summaries.

Use a disposable Windows account without existing ExileSight data; the harness
refuses to overwrite it, kills only its child and retries cleanup five times.
Persistent file locks are logged and can leave data requiring manual removal.

```powershell
$env:EXILESIGHT_PERF_APP = "$pwd\artifacts\publish\ExileSight.App.exe"
$env:EXILESIGHT_PERF_REPORT = "$pwd\artifacts\perf\local.json"
$env:EXILESIGHT_PERF_REQUEST_REPORT = '1'
dotnet test tests/ExileSight.Perf.Tests -c Release
```

```sh
DOTNET_GCConserveMemory=9 EXILESIGHT_REAL_PACKS=/path/to/verified/packs \
  EXILESIGHT_MEMORY_REPORT=/tmp/packs.json dotnet test tests/ExileSight.Features.ItemInfo.Tests \
  --filter FullyQualifiedName~RealPackMemoryTests
EXILESIGHT_REAL_PACKS=/path/to/verified/packs dotnet test tests/ExileSight.Features.ItemInfo.Tests \
  --filter FullyQualifiedName~RealPackItemEquivalenceTests
```

EXILESIGHT_MEMORY_SETTLE_SECONDS optionally extends each Core idle interval.
The old NO_FORCED_GC probe switch is retired: natural measurements now always
precede the final live-data collection. Real-pack tests skip when packs are absent;
the published gate skips off Windows or without an executable. CI supplies both.
Run equivalence separately from the memory probe to exclude the frozen oracle's
intentionally retained DTOs. Debug panel shows own-process CPU/private MB and GC
committed memory; close it when measuring a quiet minute after real item checks.

## Distribution sizes

Run 42419392581 reported alternate downloads, not RAM:

| Artifact | Decimal MB |
| --- | ---: |
| ExileSight-win-Setup.exe | 86.39 |
| ExileSight-0.1.0-full.nupkg | 78.88 |
| ExileSight-win-Portable.zip | 78.88 |
| Total (plus <1 KB metadata) | 244.14 |

The earlier ~215 MB estimate predates bundled packs; the single-file executable
is 159.99 MB. Exact bytes are in distribution-sizes.json and publish-sizes.json.
Safe follow-ups: separate symbols, avoid redundant download formats, optionally
offer a framework-dependent build. Do not trim WPF, remove packs or enable NativeAOT
without compatibility evidence. Release packaging is unchanged.
