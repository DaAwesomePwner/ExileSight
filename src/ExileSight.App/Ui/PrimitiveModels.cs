namespace ExileSight.App.Ui;

public enum CardTone { Best, High, Medium, Low, Crafted, Unique, Unknown }
public enum PillKind { Tier, Implicit, Crafted, Unique, Enchant, Unknown }
public enum SlotState { Used, Open, Unknown }
public sealed record TierPillModel(PillKind Kind, int? Tier = null, CardTone Tone = CardTone.Unknown);
/// <param name="Position">Fraction in [0, 1], or null when unknown.</param>
public sealed record RollBarModel(double? Position, CardTone Tone);
public sealed record StatTileModel(string Label, string? Value, CardTone? Tone = null);
public sealed record VerdictModel(int Wanted, int Unwanted);

