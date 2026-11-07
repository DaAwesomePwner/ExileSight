# Translation preparation

PackCodec.Validate only validates/deserializes. It does not compile regexes on discarded
validation copies. The weak-key Lazy cache creates each variant's pattern array once,
even when concurrent consumers prepare or match it.

After ReadLocalAsync or RefreshAsync returns a pack, the App should await
StatText.PrepareAsync(pack.Content.Translations ?? [], cancellationToken) before
publishing it to interactive consumers. Preparation runs on the thread pool and is
cancellable between translations. Matching remains safe before preparation, but can
then incur first-use compilation latency. This method performs no IO or input actions.

Item-info's PackItemInfoData.PrepareAsync also prepares its split template fragments;
await that method when composing the feature adapter. Pack translation arrays must be
treated as immutable for the lifetime of a loaded pack.

Tests inspect cache absence before preparation and pattern object identity after
concurrent preparation and repeated matching; production contains no test counter.
