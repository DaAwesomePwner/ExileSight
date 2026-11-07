namespace ExileSight.Core.DataPacks;

public sealed record StatCondition(decimal? Min = null, decimal? Max = null, bool Negated = false)
{
    public bool Accepts(decimal value) => ((Min is null || value >= Min) && (Max is null || value <= Max)) != Negated;
}
public sealed record StatVariant(string Text, StatCondition[] Conditions, string[] Formats, string[][] IndexHandlers);
public sealed record StatTranslation(string Id, string[] StatIds, StatVariant[] English, bool Hidden = false);
// A range-only copy cannot identify one rolled value; preserve its endpoints instead of guessing.
public sealed record StatValue(string StatId, decimal Min, decimal Max);
public sealed record StatMatch(string TranslationId, StatValue[] Values);
