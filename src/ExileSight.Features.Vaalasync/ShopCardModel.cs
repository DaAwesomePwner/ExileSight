namespace ExileSight.Features.Vaalasync;

public sealed record ShopAmount(string Choice, string Adjustment, string Text, bool Available);

public sealed record ShopCardModel(string Name, string? ListedNow, TimeSpan? ListedFor,
    string Currency, bool Armed, bool NoteMode, IReadOnlyList<ShopAmount> Amounts,
    string? CustomAmount = null, string? Pasted = null, string? Notice = null);
