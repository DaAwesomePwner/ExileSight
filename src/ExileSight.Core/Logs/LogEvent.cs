namespace ExileSight.Core.Logs;

/// <summary>Client timestamps are local wall time, without a recorded timezone.</summary>
public sealed record LogEnvelope(DateTime Timestamp, long UptimeMilliseconds, string Id, string Level, string Source, string Message);
public abstract record LogEvent(string Raw, LogEnvelope? Envelope);
public sealed record Unknown(string Raw, LogEnvelope? Envelope) : LogEvent(Raw, Envelope);
public sealed record AreaGenerated(string Raw, LogEnvelope? Envelope, int Level, string AreaId, long Seed) : LogEvent(Raw, Envelope);
public sealed record AreaEntered(string Raw, LogEnvelope? Envelope, string Name) : LogEvent(Raw, Envelope);
public sealed record CharacterLevelUp(string Raw, LogEnvelope? Envelope, string Name, string Class, int Level) : LogEvent(Raw, Envelope);
public record PlayerDied(string Raw, LogEnvelope? Envelope, string Name) : LogEvent(Raw, Envelope);
public sealed record PlayerSlain(string Raw, LogEnvelope? Envelope, string Name) : PlayerDied(Raw, Envelope, Name);
public record NpcDialogue(string Raw, LogEnvelope? Envelope, string Npc, string Text) : LogEvent(Raw, Envelope);
public sealed record MercenaryEncounter(string Raw, LogEnvelope? Envelope, string Npc, string Text) : NpcDialogue(Raw, Envelope, Npc, Text);
public sealed record MapEvent(string Raw, LogEnvelope? Envelope, string Kind) : LogEvent(Raw, Envelope);
public sealed record FilterReloaded(string Raw, LogEnvelope? Envelope, string Filter) : LogEvent(Raw, Envelope);
public sealed record AfkModeChanged(string Raw, LogEnvelope? Envelope, bool Enabled) : LogEvent(Raw, Envelope);
public sealed record DndChanged(string Raw, LogEnvelope? Envelope, bool Enabled) : LogEvent(Raw, Envelope);
public record ChatMessage(string Raw, LogEnvelope? Envelope, string Channel, string Sender, string Text) : LogEvent(Raw, Envelope);
/// <summary>A trade request is display data, never proof that a sale completed.</summary>
public sealed record TradeWhisper(string Raw, LogEnvelope? Envelope, string Direction, string Peer, string Text,
    string Item, string Price, string League, string? Stash) : ChatMessage(Raw, Envelope, Direction, Peer, Text);
