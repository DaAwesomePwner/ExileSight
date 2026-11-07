using System.Runtime.CompilerServices;
using ExileSight.Platform.Abstractions.Files;

namespace ExileSight.Core.Logs;

/// <summary>One active enumeration per instance. Passive state only; never performs game actions.
/// Character identity is a level-up inference, not an authenticated identity. Party level-ups may be ambiguous.</summary>
public sealed class LogEventStream(IFileTail tail, LogParser parser)
{
    public event Action<FileTailDiagnostic>? Diagnostic;
    private void OnDiagnostic(FileTailDiagnostic diagnostic) => Diagnostic?.Invoke(diagnostic);

    private string? area;
    private string? character;
    private int reading;
    public string? LastKnownArea => Volatile.Read(ref area);
    public string? LastKnownCharacter => Volatile.Read(ref character);

    public async IAsyncEnumerable<LogEvent> ReadAsync(string path, long? position = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref reading, 1) != 0) throw new InvalidOperationException("Already reading this stream.");
        var diagnostics = tail as IFileTailDiagnostics;
        if (diagnostics is not null) diagnostics.Diagnostic += OnDiagnostic;
        try
        {
            await foreach (var line in tail.ReadLinesAsync(path, position, cancellationToken).ConfigureAwait(false))
            {
                var e = parser.Parse(line);
                if (e is AreaEntered entered) Volatile.Write(ref area, entered.Name);
                if (e is CharacterLevelUp leveled) Volatile.Write(ref character, leveled.Name);
                yield return e;
            }
        }
        finally
        {
            if (diagnostics is not null) diagnostics.Diagnostic -= OnDiagnostic;
            Volatile.Write(ref reading, 0);
        }
    }
}
