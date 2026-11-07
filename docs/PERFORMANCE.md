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

| Measurement | Before (8ee74d4) | After |
| --- | --- | --- |
| Windows 60-second average CPU | Pending Windows CI | Pending Windows CI |
| Windows peak private MB | Pending Windows CI | Pending Windows CI |
| Empty events-view timer wakeups | 10/second | 1/second |
| Minimized map-tracker status wakeups | 1/second | 0/second |

The CPU/memory rows are intentionally pending: implementation occurred on macOS,
which cannot execute WPF. Read the two JSON reports before accepting the budget;
no before/after CPU or RAM figures have been fabricated. Expected numerical
budget margin is **unknown until that run**. A useful owner acceptance target is
at least 0.5 percentage points CPU and 40 MB memory headroom (CPU <= 0.5%, memory
<= 160 MB); this is a target, not a measurement or prediction. Reserve this
0.5-point/40-MB headroom for shared-runner variation until repeated Windows runs
establish an empirical range. Scheduler contention, JIT/GC timing and network
completion can move a 60-second result; there is no measured flakiness bound yet.
A result near either limit is fragile. Retain failed reports and compare repeated
runs; do not relax the strict gate or rerun until a failure disappears.

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
reads only its own launched child using test-only managed process APIs.

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
