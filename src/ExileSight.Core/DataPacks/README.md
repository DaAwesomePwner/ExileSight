# Translation matching and cache lifetime

PackCodec.Validate validates/deserializes without preparing regexes. Matching uses
literal candidates and creates patterns lazily. The cache holds at most 512 variant
entries. Its keys and eviction queue are weak; neither roots an obsolete pack.
Item-pack version/content replacement or removal clears the cache before Changed
notifications. Source-only publication keeps it. In-flight callers may finish using
old patterns, which remain collectible when those callers release them.

A pattern array is constructed once per cached entry, including concurrent calls.
Eviction or publication invalidation can rebuild it with identical matching results.
This bounded matcher cache is separate from the shared pack view: the view remains
parsed once per version/content snapshot under D-086.

Do not call PrepareAsync over an entire shipping pack at startup. It churns the
bounded cache and evicts most of the prepared patterns. The compatibility warmup
APIs are only useful for a small known candidate set; they provide no residency
guarantee. ItemInfo matches on demand and prepares its compact adapter on the pack
loading thread pool. Treat loaded translation arrays as immutable.
