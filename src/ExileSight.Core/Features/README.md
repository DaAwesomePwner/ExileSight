# Adding a compiled-in feature

Create a portable `ExileSight.Features.Name` project referencing Core and Abstractions only.
Expose exactly one public, parameterless `IFeature` implementation. Keep its constructor
side-effect free. Put `[DeclaredActions(ActionKind.CopyItem)]` on it (an empty attribute
for passive features), matching its immutable manifest. The architecture audit checks
that attribute and scans GameAction references as an additional best-effort check.

In `Register`, register names local to your feature: `RegisterCommand("copy",
ActionKind.CopyItem, token => context.Execute(token, new GameAction.CopyItem()))`.
The resulting persisted command ID is `manifest-id:copy`. A null action declares a
local-only command. Registration closes when `Register` returns. Keep the context
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
Log subscriptions are passive and run on the reader thread. Do not throw from them.
NetworkHosts declares in-process HTTP access only, not default-browser destinations.
Browser calls require exact HTTPS host allowlists; no wildcard/subdomain matching.
