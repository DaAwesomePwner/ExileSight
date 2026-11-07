# Idle performance budget

The 1.0 gate is **average CPU < 1% of one core** and **private bytes < 200,000,000
bytes (200 decimal MB)**. CPU is `100 × delta process CPU seconds / elapsed wall
seconds`, without division by processor count. A fully occupied core is 100%
on every machine. Memory uses the maximum of one-second samples, not managed
heap size, working set, or the average. Brief spikes between samples can escape
this measurement.

## Repeatable Windows gate

`.github/workflows/perf.yml` runs on pushes to `integration/1.0`, pull requests
targeting `integration/**`, and manual dispatch.
It publishes the self-contained win-x64 single-file app and bundles the same
verified PoE1 packs as shipping CI. The test launches the real app, including all
compiled feature modules, hooks, tray, log tail, prices, pack and update services.
It seeds a valid default profile through `ProfileStore` with first-run complete
and an empty readable test-owned Client.txt. Normal startup validation stays on;
there is no production test mode or safety bypass. No feature or network refresh
is disabled. Only the existing allowed network destinations are used.

Use a disposable Windows account with no game running and no existing
`%APPDATA%\ExileSight` directory. The test refuses existing app data. It waits for
the UI message loop, settles for 60 seconds, then samples for at least 60 seconds.
It fails if the app exits, exposes a top-level window (including setup/error
dialogs), or reaches either budget. Tray-only state is a startup smoke check,
not a proof that every asynchronous service has finished loading. Slow initial
network operations can overlap the sample; this deliberately does not suppress
real application costs. The harness terminates only its launched child in
`finally`, then removes only the test-created application data. Deletion checks
for an existing directory and retries I/O failures five times, 250 ms apart. A
final IOException is logged in test output without masking the budget result.
A persistent lock can still leave data behind; the next run refuses that folder
rather than deleting pre-existing data. Inspect the warning before removing it.

The artifact `ExileSight-idle-performance` contains `current.json`, `baseline.json`,
TRX results, and size manifests. Reports include raw samples, elapsed time,
revision, OS, core count, peak private bytes and remaining budget margins. The
GitHub job summary shows CPU %, private MB, budgets and margins for both runs,
including failed or missing measurements. The current measurement is a required
failing step. The pinned pre-hardening commit
`8ee74d4893ce4b46518bed2f8c98f65e848ce291` runs separately on the same runner with
the same packs and harness; only that comparison step permits failure. After
reviewing the first Windows comparison, record the numbers, run URL and revision
in the evidence table below. Then remove the contiguous workflow block marked
`BEGIN/END temporary baseline comparison`; retain the current budget gate and
report summary. The summary tolerates an absent baseline report.

On Windows, after publishing and bundling packs as in the workflow:

```powershell
$env:EXILESIGHT_PERF_APP = "$pwd\artifacts\publish\ExileSight.App.exe"
$env:EXILESIGHT_PERF_REPORT = "$pwd\artifacts\perf\local.json"
dotnet test tests/ExileSight.Perf.Tests -c Release
```

Without the executable environment variable, the published-app test is explicitly
skipped. Own-process counter tests still run on Windows. Windows-only tests skip
on macOS/Linux; the CPU normalization test runs everywhere. The workflow always
sets the executable and treats a missing executable as failure.

## Baseline, changes and evidence

The first Windows run ([42417510247](https://github.com/DaAwesomePwner/ExileSight/actions/runs/42417510247),
PR #9 head `a46cd40`, tested merge `82ebad1`) **failed memory**:

| Windows metric | First gate | Budget / remaining margin |
| --- | ---: | --- |
| Average CPU, one core | 0.83% | < 1%; +0.17 percentage points |
| Peak private bytes | 1,541,304,320 (1541.30 MB) | < 200 MB; −1341.30 MB |
| After memory fix | Pending PR CI | Aim <= 150 MB; do not infer a Windows pass from Core tests |

Private memory stayed around 1.54 GB throughout the minute. The baseline build
failed before measurement because its nested checkout inherited the parent
`.editorconfig`. It now moves to `RUNNER_TEMP` before MSBuild runs, without any
analyzer exemption. Windows used build 26100, four logical processors and the
self-contained single-file app. CPU's 0.17-point margin is thin on shared runners;
retain failures and compare repeated runs instead of relaxing the gate.

### Root cause and measured correction

The verified packs from run `42414705210` reproduce excessive memory through
production `PackLoader`, `PackStartup` and `PackReader` on macOS ARM64/.NET 10.0.14,
without WPF, price services or bundle extraction. Probe values are approximate
managed heap readings, **not Windows private bytes**; Server GC was false.

| Controlled pack experiment | Managed bytes after passive feature reads |
| --- | ---: |
| Original eager startup, forced collections | 920,878,104 |
| Only interpreted stat regexes, forced collections | 451,998,696 |
| Deferred adapter + compressed snapshots + direct cache decoder, forced collections | 34,375,536 |
| Fixed path, 60 s without forced GC, default policy | 129,286,528; GC committed 224,460,800 |
| Same fixed path, 60 s without forced GC, ConserveMemory=9 | 72,720,728; GC committed 69,287,936 |

The dominant causes were eager compiled regexes (including per-line fragment
copies), retained mutable pack graphs plus serialized snapshots, and large
base64-decoding scratch buffers. A temporary reflection probe observed 64 MiB
and 16 MiB byte-pool buffers after loading the real envelopes; direct decoding
eliminated those large rentals. Local loading also deserialized/validated each
pack twice; it now returns the already validated result.

The fix retains compressed immutable snapshots and metadata, constructs the item
adapter only at first item use, and uses interpreted regexes cached on demand.
Streaming hash/compression avoids a full serialized JSON copy. Manifest-scoped
reads, shared lazy views parsed once per version, detached mutable copies and
ordered Changed notifications remain covered by existing tests. A new regression
also checks that a deferred adapter uses its captured version after replacement.
First item use now pays deserialization/matching cost; in-game latency and memory
after first use still need owner validation. This gate covers tray idle.

Only after reducing retained data did the no-forced-GC comparison justify
`System.GC.ConserveMemory=9`: GC committed memory fell from 224.46 to 69.29 MB.
The app sets this supported [.NET 10 runtime option](https://github.com/dotnet/runtime/blob/v10.0.0/src/coreclr/gc/gcconfig.h),
with no hard heap limit, forced collection or service bypass. The Windows gate
must confirm total private memory and CPU; 150 MB remains a target, not a claim.
Raw probe evidence is under `tests/ExileSight.Perf.Tests/evidence/`.

Reproduce the isolated probe with downloaded verified `.pack.json` files:

```sh
EXILESIGHT_REAL_PACKS=/path/to/packs EXILESIGHT_MEMORY_REPORT=/tmp/packs.json \
  dotnet test tests/ExileSight.Features.ItemInfo.Tests --filter FullyQualifiedName~RealPackMemoryTests
```

The default probe forces GC only in the test to isolate retained data and asserts
an < 80 MB heap increase. To compare natural idle GC, set
`EXILESIGHT_MEMORY_NO_FORCED_GC=1`, `EXILESIGHT_MEMORY_SETTLE_SECONDS=60`, and compare
unset `DOTNET_GCConserveMemory` with `DOTNET_GCConserveMemory=9`. CI also runs the
isolated pack probe and uploads `pack-memory.json` alongside the real app gate.

Localized fixes:

- `src/ExileSight.App/Features/Events/EventsView.cs`: empty queues poll at 1 Hz and
  skip redundant text updates; active toasts retain 100 ms expiry polling. A new
  toast can appear up to one second later. Settings still refresh while open.
- `src/ExileSight.App/Features/MapTracker/TrackerWindow.cs`: stop status polling
  while hidden or minimized; resume on visibility/state changes. Recording and
  history persistence are unaffected.

Audit: Client.txt polling uses a 350 ms interval with capped error backoff;
window detection retries missing windows every 1.5 s and uses event hooks once
found. The debug panel already stops its 500 ms timer when hidden/minimized.
Pack refresh sleeps an hour after setup; updates have a startup delay and a
six-hour cadence; price refresh is throttled to at least an hour. Map tracker
opens its viewer only on request. Guide checkpoints are every 60 seconds and
save changed state only. Two always-on safety timers are unchanged:

- `src/ExileSight.Platform.Windows/Input/HookForeground.cs:21`: a 10 Hz foreground
  heartbeat renews the UI-owned foreground lease. A stalled UI or changed native
  foreground must let keys pass through rather than block using stale state.
- `src/ExileSight.Platform.Windows/Input/HookHost.cs:62`: a 1 Hz keyboard-repeat
  settings refresh keeps repeat-silence thresholds aligned with Windows settings,
  off the hook callback, so held keys are classified safely.

Guide's 1 Hz view tick and Timers' 4 Hz tick are deferred to a follow-up; timer
completion must continue even with a hidden timer panel.
These audit observations are not proof of budget compliance. If CI fails, retain
the reports and profile the full published process before adjusting intervals.

## Owner diagnostics

Open **Debug panel** from the tray. **Own-process resources** shows CPU percentage
between panel refreshes and private memory in decimal MB. The first CPU read
says “Sampling…”. Close or minimize the panel to stop its sampling. Opening the
panel itself costs CPU, so its short-interval readout is for diagnosis; use the
closed-panel test above for the 60-second gate. Reopening can include the hidden
interval in its first CPU delta.

Memory is `GetProcessMemoryInfo(PROCESS_MEMORY_COUNTERS_EX)` on
`GetCurrentProcess()`'s pseudo-handle. CPU is `Environment.CpuUsage.TotalTime`.
The production reader accepts neither a PID nor a handle, opens no foreign
process, and introduces no analyzer suppression. The external test harness
reads only its own launched child using test-only managed process APIs. It sets
`EXILESIGHT_PERF_MEMORY_REPORT=1` on that child to enable a five-second own-process
snapshot under its application-data directory. The final snapshot is included as
`ownProcessMemory` in the report (null for the old baseline). It includes managed
bytes, last-GC heap/committed/fragmented bytes, collection counts and private bytes.
No GC is forced; normal installations do no background diagnostic sampling.

## Distribution size

The task baseline reports approximately **215 MB combined** for setup EXE, full
NuGet package and portable ZIP. Individual sizes were not supplied and have not
been measured on this macOS checkout. `distribution-sizes.json` records each
Velopack filename and exact byte length in Windows CI, separating these three
formats plus metadata. `publish-sizes.json` records the single-file executable,
symbols and bundled pack files. Sum each manifest separately; these are alternate
distributions of the same payload, not RAM consumption or one installed footprint.
The performance workflow uses package version 0.1.0 solely for size comparison.

Safe follow-ups: retain only the needed alternate distribution in user-facing
artifact downloads; move PDBs into a separate symbols artifact; compare compressed
single-file publication against startup time; consider an optional framework-
dependent build for users who already have the .NET Desktop Runtime. Keep the
self-contained build available. Do not enable trimming or NativeAOT for WPF or
remove packs/locales/runtime files without compatibility evidence. Packaging
changes are outside this task; the release workflow is untouched.
