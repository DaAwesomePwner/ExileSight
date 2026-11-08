# Adding a compiled-in feature

Create a portable `ExileSight.Features.Name` project referencing Core and Abstractions only.
Expose exactly one public, parameterless `IFeature` implementation. Keep its constructor
side-effect free. Put `[DeclaredActions(ActionKind.CopyItem)]` on it (an empty attribute
for passive features), matching its immutable manifest. The architecture audit checks
that attribute and scans GameAction references as an additional best-effort check.

In `Register`, register names local to your feature: `RegisterCommand("copy",
ActionKind.CopyItem, token => context.Execute(token, new GameAction.CopyItem()))`.
The resulting persisted command ID is `manifest-id:copy`. A null action declares a
local-only command. `RegisterCommand` defaults to `CommandKind.Bindable`; pass
`CommandKind.OverlayOnly` for physical overlay selections. BindingMapper rejects
hotkey bindings to overlay-only commands, including persisted bindings. The policy
does not mint a token: action-bearing selections still need a paired physical click
and the Action Gate. Registration closes when `Register` returns. Keep the context
for scoped settings and execution; never construct tokens yourself.

Add the module explicitly to App's `FeatureRegistration.CompiledModules` and register
its view-model factory with `FeatureViews.Register<T>` before StartFeatureViews.
Factories may wire buttons through `FeatureViews.Bind(button, "feature:command")`.
Views are created before input starts, never from a hotkey callback. View models
and feature logic must marshal their own UI notifications appropriately.

Omni handlers run highest priority first, registration order breaking ties. They
receive parsed text and short/long press information, not a fresh token: the omni
copy already spent the press. A later action needs its own physical overlay click.

Settings live under Features.Sections[manifest-id]; values are cloned JSON elements.
Log subscriptions are passive: production shares one Client.txt tailer and delivers
callbacks on an independent serial queue per feature, in registration order, with
failure isolation. `SubscribeLog` receives `ClientLogEvent` envelopes; the bounded
live queue drops oldest and reports losses in `DroppedBefore`. Optional `LogReplay`
selects a separate bounded snapshot of this session's retained events, delivered
before live events; it never reads pre-start log contents. `LogSourceChanged`
marks a selected-path change. Neither replay nor live delivery grants a token.
NetworkHosts declares in-process HTTP access only, not default-browser destinations.
Browser calls require exact HTTPS host allowlists; no wildcard/subdomain matching.

For a duration-sensitive command use RegisterPressCommand. With a declared ActionKind,
the down callback receives the original intent and a PressStarted ID/timestamp. Pass
null for an action-free hold command: it never receives or holds a token, and its
binding can pass the key through with Block=false. Release receives only the same ID
and hook down/up timestamps; release never authorizes an action. The queue drops
releases whose down was refused or lost, and drops delivery while the game is unfocused.
A feature may retain the down token for the same press's short release (D-081):
match press ID and down timestamp, require 0–200 ms duration, then spend it once
through `context.Execute`. The gate still checks its 1.5 s lifetime at send time.
This narrow release path does not permit timer-driven or other deferred actions.
Action-free holds receive no token on either callback (D-083).
D-059 silence recovery replaces the old pending press, dropping its missing release.
Omni copies on down and only routes the captured item on release (>200 ms means long).

RegisterDefaultBinding declares a portable default. HotkeySettings appends it only
when no persisted binding has that ID, including disabled persisted bindings. The
omni binding ID is omni.press; its command ID follows the existing omni:press convention.
BrowserHosts describes external browser destinations separately from NetworkHosts.

The host owns the sole OmniRouter. A feature's OmniHandlers view contains only that
feature's registrations; DispatchOmni asks the host to route across all registrations.
DispatchOmni posts token-free work to a separate serial feature queue. Slow or failing
handlers cannot stall the action worker or UI dismissal. Handlers must marshal their
own view changes to the UI dispatcher. Omni handlers receive no intent to retain.

PassiveInputChannel is a separate hook-to-UI path with no action authority. The menu
opens a timestamped visibility epoch and drains it at background dispatcher priority
every 16 ms only while visible. Mouse moves share one latest-only slot; clicks and
Esc use a separate lossless queue. No passive observation wakes the action worker.
Closing clears interest; reopening discards the old epoch and rejects older hook
timestamps. Observer exceptions are isolated per subscriber.

Omni reads Prices.League directly. App composition shares a PriceService and passively
requests its cached league discovery result, using DefaultLeague once available; until
then (or when discovery is unavailable) it falls back to Standard. The feature performs no HTTP calls and does not duplicate league heuristics.

`ObserveCopiedItems` registers a token-free observer during feature registration.
The host delivers an immutable `CopiedItem` summary before omni routing, isolating
and attributing observer exceptions per feature. Observers cannot mutate the routed
item and receive no authority to send game input.

`Packs` exposes a manifest-scoped `IPackReader`. Declare every pack ID in DataPacks;
`TryGet<T>(packId)` returns null for undeclared, missing or incompatible data.
A `PackSnapshot<T>` supplies version/source metadata and shared immutable `Data`,
parsed lazily once per published version/content snapshot. Reads perform no I/O;
`CreateCopy` explicitly deserializes mutable wire data at O(pack size).
Subscribe to `Changed` before reading initial state. The host posts declared-pack
notifications to its work queue in publication order and isolates subscriber
failures; data may already reflect a later version when a callback runs. Marshal
UI work as needed. Pack notifications carry no input authority.
