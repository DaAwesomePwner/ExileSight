namespace ExileSight.App.Ui;

public enum CardTone { Best, High, Medium, Low, Crafted, Unique, Unknown }
public enum PillKind { Tier, Implicit, Crafted, Unique, Enchant, Unknown }
public enum SlotState { Used, Open, Unknown }
public sealed record TierPillModel(PillKind Kind, int? Tier = null, CardTone Tone = CardTone.Unknown);
/// <param name="Position">Fraction clamped to [0, 1] by the view; null or NaN means unknown.</param>
public sealed record RollBarModel(double? Position, CardTone Tone);
public sealed record StatTileModel(string Label, string? Value, CardTone? Tone = null);
public sealed record VerdictModel(int Wanted, int Unwanted);
/// <param name="AverageRoll">Mean roll percentage, or null when no ranged mods are known.</param>
public sealed record UniqueVerdictModel(double? AverageRoll);

