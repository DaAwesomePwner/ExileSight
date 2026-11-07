# Idle performance budget

The 1.0 budget applies to **idle during a normal session, including after item
checks and other feature use**, not just an untouched tray process. Average CPU
must be **< 1% of one core** and private memory **< 200,000,000 bytes**. The targets
for headroom are <= 0.5% CPU and <= 150 MB private memory. Neither target is a
claim about an unmeasured build.

CPU is `100 × delta process CPU seconds / elapsed wall seconds`, without division
by processor count. One fully occupied core is 100%. Memory is the maximum of
one-second private-byte samples in decimal MB, not managed heap or working set.
Spikes between samples can escape this measurement.

## Windows evidence

| Revision / run | CPU | Peak private MB | Result |
| --- | ---: | ---: | --- |
| a46cd40, PR merge 82ebad1 / 42417510247 | 0.829% | 1541.30 | Memory failed |
| Baseline 8ee74d4 / 42419392581 | 0.285% | 1633.94 | Memory failed |
| 4ca85cc, PR merge 352da9d / 42419392581 | 0.960% | 246.35 | Memory failed; CPU margin only 0.04 pp |
| Current session-memory fix | Pending PR CI | Pending PR CI | Not yet verified on Windows |

Reports: [first run](https://github.com/DaAwesomePwner/ExileSight/actions/runs/42417510247),
[second run](https://github.com/DaAwesomePwner/ExileSight/actions/runs/42419392581).
Windows build 26100 had four logical processors and a self-contained single-file
app. At the end of the second run, its own report showed approximately 61 MB
managed bytes, 126 MB last-GC heap, 157 MB committed, and 87 MB fragmentation.
Non-GC private memory was approximately 86–90 MB. These different counters have
different sampling semantics; managed bytes are not total process memory.

The gen0/1/2 values 15/14/12 were **cumulative since process startup**, not twelve
gen2 collections proven to occur in the idle minute. The harness now saves
snapshots at measurement start/end and reports `observedIdleGc`: allocation-byte
and collection-count deltas with their actual sample interval. Diagnostic reports
also include process-query counts/skips and their managed allocations. This will
separate startup collections, ongoing allocation and GC policy effects in CI.

## Root causes and corrections

The real shipping packs reproduce excessive allocation without WPF, prices or
bundle extraction. Earlier experiments found eager compiled regex preparation,
retained wire graphs, duplicate validation, and 64 MiB/16 MiB base64 scratch
buffers. Round 1 removed those buffers and compressed snapshots, but deferring
large DTO copies and exhaustive regex construction merely moved costs to first
use. Its initial tray-only claim was insufficient for the session budget.

The current implementation:

- Builds one compact item adapter on the background pack-loading path while
  validated DTOs are already available; first use no longer deserializes three
  full copies. Unused pack fields are released. Only prefix/suffix mods whose
  domains occur in loaded bases are retained: these are the only entries
  `FindMod` can return. Repeated strings, weights, stats and arrays are shared
  within the version; construction pools are then released.
- Uses a necessary literal from each template line to reject unrelated variants
  before constructing regexes. Empty literals fall back to matching; the existing
  regex/value/condition logic remains authoritative. Fragment patterns are built
  only for candidate mods, with at most 512 stat variants retaining regexes.
  Evicted patterns rebuild identically. Fixture lines are compared against the
  frozen exhaustive matcher; existing parser/ItemInfo fixtures remain unchanged.
- Shares identical immutable guide gem requirement maps and levels rather than
  retaining tens of thousands of equivalent dictionaries. Source detachment and
  immutability have regression coverage.
- Removes the deferred Lazy entirely. Failed preparation does not publish an
  adapter or cache an exception; the next load retries even at the same version.
  Successful unchanged versions reuse the same adapter. Manifest scoping, shared
  lazy pack views, hash identity and Changed notifications remain unchanged.

### First use and session measurements

Production `PackLoader`, `PackStartup`, `MatchLine`, `FindMod` and
`ItemInfoCalculator` were measured with verified packs from run `42414705210` on
macOS ARM64/.NET 10.0.14. The first item is a synthetic advanced-copy Glorious
Plate with the real Fecund life affix. The session stage checks every repository
item fixture. Values below include the test host's managed baseline and are
**not Windows private-memory measurements**.

| Probe stage | 4ca85cc | Current fix |
| --- | ---: | ---: |
| First item latency | 534.0 ms | 26.8 ms |
| Live managed bytes after first item, full GC | 244.48 MB | 38.68 MB |
| Tray live managed bytes, full GC | 34 MB in prior probe | 35.50 MB |
| Live managed bytes after fixture session, full GC | Not measured | 39.21 MB |

The enforced Core-path limits are < 50 MB retained heap increase after the
session and < 500 ms for first item use. Full GC at probe sample points measures
live data. Raw evidence is in `tests/ExileSight.Perf.Tests/evidence/`.

After reducing live retention, bulk-load LOH fragmentation still left 108.82 MB
committed in the probe. Compacting once after pack preparation reduced that to
31.69 MB after the session; live data stayed approximately 39 MB. Production now
performs one blocking compacting collection after a load allocating >=32 MB,
once transient loader DTOs are out of scope. Small loads, idle polling and item
checks do not trigger this cleanup. The measured bulk-load pause was 23.8 ms;
Windows startup/hourly-refresh pause and committed bytes need CI confirmation.

Before bulk-load reclamation, 60-second probes without diagnostic forced GC
ended at 193.46 MB GC committed with ConserveMemory=5 and 105.41 MB with 9.
With reclamation and policy 9, committed memory was 84.71 MB tray-idle and
91.18 MB after the fixture session; first use was 24.7 ms and cleanup 24.7 ms.
No gen2 collection occurred during the final idle minute. These natural-GC
experiments retain uncollected garbage and fail the probe's stricter 50 MB live
assertion; they are policy comparisons, not the enforced full-GC live check.
Raw evidence includes `session-natural-reclaim.json`. Keep ConserveMemory=9;
a 96 MiB hard-limit experiment could not load all packs. No hard limit, RetainVM
or GC-affinity override is shipped. Adding the earlier Windows native footprint
to 91.18 MB suggests 177–181 MB total (19–23 MB budget margin), **an inference,
not a Windows result**. The <=150 MB headroom target remains unverified.

### Idle work

`TailReader` used a fresh buffered FileStream at each 350 ms poll despite owning
its own read buffer. The regression probe measures 1000 quiet polls: the old
populated-log path allocated 9,652,816 bytes versus 1,256,256 after the fix
(87% less). Empty-log polling dropped from 973,200 to 533,792 bytes. The new path
disables redundant buffering and skips an empty EOF read. This is a measured
allocator, not proof it caused the Windows CPU regression (the CI log is empty).

Missing-game detection previously requested a GeForceNOW process snapshot every
1.5 seconds. The [.NET implementation](https://github.com/dotnet/runtime/blob/v10.0.0/src/libraries/System.Diagnostics.Process/src/System/Diagnostics/ProcessManager.Win32.cs)
allocates a native system-information buffer and managed metadata for that query.
`WindowSource` now first checks for a visible matching title using stack buffers;
it queries and verifies the process only when a streaming match is possible.
No title is retained or logged. Native-client detection, foreground checks and
GFN process validation remain in place. The new query/skip counters verify this
path on Windows; CPU savings are not yet measured there.

Diagnostic file sampling is opt-in and reduced from 5 s to 15 s. The report
measures allocations of the counter reads themselves; it does not assume that
GC.GetGCMemoryInfo caused the regression. The 10 Hz foreground lease heartbeat
and 1 Hz keyboard-repeat refresh are unchanged safety timers: stalled UI must
pass keys through, and held keys must respect Windows repeat settings.
The Guide 1 Hz and Timers 4 Hz view ticks remain deferred to a follow-up. Earlier
fixes retain 1 Hz empty-event polling and pause hidden/minimized tracker status.

## CI and local measurement

`perf.yml` runs on integration/1.0 pushes, PRs targeting integration/** and manual
dispatch. It publishes and bundles verified packs, then runs the real Windows
tray gate: valid settings are seeded through ProfileStore, an empty readable
Client.txt is created, startup safety checks stay enabled, and all features and
refresh services remain enabled. After 60 s settling it measures at least 60 s.
The test fails on process exit, visible setup/error windows, or either budget.
Tray-only visibility is a startup smoke check, not proof all background IO ended.

The enforced first-use/session Core probe runs **after** the Windows gate with
`if: always()`, followed by real-pack equivalence tests. A failing probe cannot
hide Windows private-memory numbers. There is no existing external test trigger
for an item check in the published app without a real game/physical intent.
We do not add a production safety bypass or fake an Action Gate token. The Core
session probe is therefore the enforced after-use fallback authorized for CI;
full-app post-use Windows private bytes still require owner validation.

The job summary and `ExileSight-idle-performance` artifact contain current and
baseline CPU/private-memory margins, the managed session stages/latency, JSON,
TRX and distribution-size manifests. Baseline checkout moves to RUNNER_TEMP
before MSBuild to avoid inherited analyzer configuration. Once the reviewed
before/after Windows numbers are recorded, remove the marked temporary baseline
block; keep both current gates and summaries.

Use a disposable Windows account with no game and no existing ExileSight app
data; the harness refuses existing data. It kills only its child, retries cleanup
five times 250 ms apart, and logs a final IOException without masking the budget
result. Persistent locks can leave data behind; later runs refuse to overwrite it.

```powershell
$env:EXILESIGHT_PERF_APP = "$pwd\artifacts\publish\ExileSight.App.exe"
$env:EXILESIGHT_PERF_REPORT = "$pwd\artifacts\perf\local.json"
dotnet test tests/ExileSight.Perf.Tests -c Release
```

```sh
EXILESIGHT_REAL_PACKS=/path/to/verified/packs EXILESIGHT_MEMORY_REPORT=/tmp/packs.json \
  dotnet test tests/ExileSight.Features.ItemInfo.Tests --filter FullyQualifiedName~RealPackMemoryTests
```

For natural-GC comparisons additionally set EXILESIGHT_MEMORY_NO_FORCED_GC=1,
EXILESIGHT_MEMORY_SETTLE_SECONDS=60 and DOTNET_GCConserveMemory. Without pack files
the real-pack probes explicitly skip; without a published executable the Windows
gate skips. CI supplies both. Portable regression tests still run on macOS.

## Owner checks and distribution

Open Debug panel from the tray for CPU, private MB, approximate managed heap and
last-GC committed memory. Close it during idle measurement. Own-process memory
uses GetProcessMemoryInfo on the current-process pseudo-handle; CPU uses
Environment.CpuUsage. No foreign process handles or analyzer suppressions were
added. The opt-in EXILESIGHT_PERF_MEMORY_REPORT=1 records only resource counters
under the app's own directory; normal installations have no diagnostic sampler.

On Windows, also measure a quiet minute after several item checks and feature
uses, with the debug panel closed. Check first-use responsiveness and the <=150 MB
headroom target; preserve near-limit failures rather than rerunning until green.
Shared-runner variance has no established bound; CPU <=0.5% is the target.

Run 42419392581 reported these distribution sizes (alternate downloads, not RAM):

| Artifact | Decimal MB |
| --- | ---: |
| ExileSight-win-Setup.exe | 86.39 |
| ExileSight-0.1.0-full.nupkg | 78.88 |
| ExileSight-win-Portable.zip | 78.88 |
| Total (plus <1 KB metadata) | 244.14 |

The earlier ~215 MB estimate predates bundled packs. The single-file executable
is 159.99 MB. Exact bytes are in distribution-sizes.json and publish-sizes.json.
Safe follow-ups: distribute symbols separately, avoid redundant download formats,
and optionally offer a framework-dependent build. Do not trim WPF, remove packs
or enable NativeAOT without compatibility evidence. Release packaging is unchanged.
