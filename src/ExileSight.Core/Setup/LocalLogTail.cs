using System.Runtime.CompilerServices;
using ExileSight.Core.Logs.Tail;
using ExileSight.Platform.Abstractions.Files;
using ExileSight.Platform.Abstractions.Install;

namespace ExileSight.Core.Setup;

/// <summary>Checks locality before the first open and after every polling delay, before reopen.
/// The polling reader retains its offset while a drive is unavailable or no longer local.</summary>
public sealed class LocalLogTail(ILocalPathResolver? resolver, IFileTailTimer? timer = null,
    IFileTailIdentityProvider? identityProvider = null) : IFileTail, IFileTailDiagnostics
{
    public event Action<FileTailDiagnostic>? Diagnostic;

    public async IAsyncEnumerable<string> ReadLinesAsync(string path, long? position = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var gate = new OpenGate(path, resolver, timer, message =>
            Diagnostic?.Invoke(new(FileTailCondition.Unavailable, message)));
        await gate.WaitUntilLocal(cancellationToken).ConfigureAwait(false);
        var tail = new PollingFileTail(gate, identityProvider: identityProvider);
        void Report(FileTailDiagnostic value) => Diagnostic?.Invoke(value);
        tail.Diagnostic += Report;
        try
        {
            await foreach (var line in tail.ReadLinesAsync(path, position, cancellationToken).ConfigureAwait(false))
                yield return line;
        }
        finally { tail.Diagnostic -= Report; }
    }

    private sealed class OpenGate(string path, ILocalPathResolver? resolver, IFileTailTimer? timer,
        Action<string> report) : IFileTailTimer
    {
        private string? previousRejection;
        private Task Delay(int milliseconds, CancellationToken token) =>
            timer?.DelayAsync(milliseconds, token) ?? Task.Delay(milliseconds, token);

        public async Task WaitUntilLocal(CancellationToken token)
        {
            while (true)
            {
                token.ThrowIfCancellationRequested();
                var rejection = ClientLogDiscovery.PathRejection(path, resolver);
                if (rejection is null)
                {
                    if (previousRejection is not null) report("Following new entries");
                    previousRejection = null;
                    return;
                }
                if (rejection != previousRejection) report(rejection);
                previousRejection = rejection;
                await Delay(350, token).ConfigureAwait(false);
            }
        }

        public async Task DelayAsync(int milliseconds, CancellationToken cancellationToken)
        {
            await Delay(milliseconds, cancellationToken).ConfigureAwait(false);
            await WaitUntilLocal(cancellationToken).ConfigureAwait(false);
        }
    }
}
