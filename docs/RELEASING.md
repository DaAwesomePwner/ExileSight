# Releasing ExileSight

Use **Actions → Release app → Run workflow**, selecting **main**. The workflow
refuses to run from another branch and uses the protected `release` environment.
Only the publish job has repository write permission. Never publish from a local
checkout. The owner must complete the Phase 5 Windows play-session gate first.

## Stable and beta

1. Choose an unused version: `1.0.0` with channel `stable`, or
   `1.0.0-beta.1` with channel `beta`. Do not include `v`. Versions have three
   numeric components; beta versions require `-beta.N`. Leading zeroes are rejected.
2. Leave `dry_run` enabled. Download the workflow artifact and review the size
   table in the job summary and `Releases/ASSET-SIZES.md`. Test the Setup executable
   on Windows, including install, update from the previous release and normal exit.
3. Run the same version/channel from the same reviewed main commit with
   `dry_run` disabled. The workflow creates `v<version>` as a draft, uploads and
   verifies the prepared assets before exposing the release. Beta is marked as a
   GitHub prerelease. Existing tags/releases are never overwritten. Both kinds
   use `latest=false`; the updater reads release feeds, not GitHub's Latest badge.

The workflow builds/tests Release, generates pinned data packs, verifies their
provenance and bytes, and bundles the verified fallback packs and ATTRIBUTION in
its self-contained win-x64 installer. Release notes and a separate attribution
asset retain upstream notices, including GGG ownership (D-047). No data packs
are committed to git. `SHA256SUMS` covers all prepared release assets except itself.
A failed upload leaves a draft: inspect it before removing it and its tag, or use
an unused higher version. Never expose a partially uploaded release.

## Update channels and switching

The stored/user-facing setting **`prerelease` means the beta channel**. Keep the
existing JSON values: `general.updateChannel` is `stable` (default) or
`prerelease`; do not write `beta`. Unknown values fall back to stable with the
existing settings warning. There is no migration.

The app explicitly selects Velopack `stable` or `beta` and only checks
[ExileSight's GitHub Releases](https://github.com/DaAwesomePwner/ExileSight/releases).
Stable excludes GitHub prereleases; beta includes them. Velopack 1.2.161 reads
only the requested `releases.<channel>.json`, even when prereleases are enabled.
Therefore each stable release also publishes an identical `releases.beta.json`
pointing to the same stable package. Beta releases publish only their beta feed.
Beta clients can consequently receive a newer stable version without changing
their saved preference, and subsequently receive newer betas. Stable clients
never consume the beta feed. This follows the pinned
[GitBase feed reader](https://github.com/velopack/velopack/blob/1.2.161/src/lib-csharp/Sources/GitBase.cs).

The pinned [GitHub source](https://github.com/velopack/velopack/blob/1.2.161/src/lib-csharp/Sources/GithubSource.cs)
considers the ten most recent GitHub releases (including data-pack releases in
that window). Keep a current app release within that window. The highest eligible
package version wins; publication date does not authorize a version downgrade.

Change the existing setting in the active profile under `%APPDATA%\ExileSight\poe1`.
Preferences are reread for checks, downloads and normal exit. Changing the channel
invalidates pending application of a package from the old channel. The next
scheduled check uses the new feed (5 seconds after startup, then every 6 hours);
checks remain rate-limited. Automatic downgrades are disabled: switching from a
higher beta to stable waits for a newer stable version. Profile files, feature
state and other user data are untouched. Unpackaged development installs do not
update. Downloaded updates apply on normal exit without restarting the app.

## Signing and SmartScreen

No certificate has been chosen. The owner decides the certificate/provider,
cost and key custody at the Phase 5 game-testing gate. In the GitHub `release`
environment, set both secrets to enable signing automatically:

- `SIGNING_CERT_PFX_BASE64`: base64-encoded PFX containing the signing private key.
- `SIGNING_CERT_PASSWORD`: password protecting that PFX.

The workflow temporarily imports the certificate into the runner's current-user
certificate store. Velopack `--signParams` selects its thumbprint for signtool,
so the password never appears in command arguments. Both the decoded file and
imported certificates are removed in `finally`; an always-run step also removes
the file. Missing/incomplete secrets produce a visible unsigned-build warning;
invalid configured credentials or signing failures fail the build. Dry runs
exercise signing too. Never put secrets or decoded certificates in artifacts.
The CLI options are pinned to
[Velopack 1.2.161](https://github.com/velopack/velopack/blob/1.2.161/src/vpk/Velopack.Vpk/Commands/Packaging/WindowsPackCommand.cs).

Unsigned builds can show an unknown publisher and SmartScreen warnings. Signing
does not guarantee immediate reputation or removal of warnings. Test the actual
installer on Windows; do not tell users to disable Windows protection. No external
timestamp service is configured because the network policy authorizes GitHub and
poe.ninja only. Signatures are therefore not timestamped; certificate expiry and
a policy-approved timestamp provider remain owner decisions before signed GA.

## Rollback and size budget

Withdraw a bad release from public visibility to prevent future checks finding
it. This cannot revoke copies already downloaded or guarantee that a running app
will recheck before exit. Publish the last known-good code as a **higher** patch
version through this workflow; for beta, use a higher eligible beta or stable
version. Do not replace published assets or reuse versions. Before any manual
older-version installation, close the app and back up `%APPDATA%\ExileSight`;
older code may not understand newer data schemas.

The existing CI bundle was reported at about 215 MB; that is a baseline, not a
measurement of this new workflow. Exact Setup, portable ZIP and full-package
sizes are reported in bytes and MiB on every dry run, in the summary and downloadable
`ASSET-SIZES.md`. No Windows packaging run has been triggered by this change, so
new release asset sizes remain unmeasured. Consider separately measuring bundled
runtime, debug symbols and pack contributions before reducing size. Do not enable
trimming for WPF without dedicated compatibility work; this pipeline makes no
trimming or dependency changes.
