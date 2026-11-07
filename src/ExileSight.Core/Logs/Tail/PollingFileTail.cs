using System.Runtime.CompilerServices;
using System.Text;
using ExileSight.Platform.Abstractions.Files;
using ExileSight.Platform.Abstractions.Time;

namespace ExileSight.Core.Logs.Tail;

/// <summary>Portable read-only polling. Reopens with delete sharing each poll, detecting replacement by
/// creation time and a checkpoint of the last 256 consumed bytes. An indistinguishable replacement
/// (same creation time and checkpoint) or truncate/regrow entirely between polls cannot be guaranteed
/// detectable without platform-specific file identities. Positions must be at UTF-8 boundaries.</summary>
public sealed class PollingFileTail(IClock clock, int pollingMilliseconds = 350) : IFileTail
{
    public async IAsyncEnumerable<string> ReadLinesAsync(string path, long? position = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(position ?? 0);
        ArgumentOutOfRangeException.ThrowIfLessThan(pollingMilliseconds, 1);
        long offset = 0;
        DateTime? created = null;
        byte[] checkpoint = [];
        var initialized = false;
        var discardPartial = false;
        var decoder = Encoding.UTF8.GetDecoder();
        var pending = new StringBuilder();
        var bytes = new byte[8192];
        var chars = new char[Encoding.UTF8.GetMaxCharCount(bytes.Length)];
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            FileStream? file = null;
            try
            {
                file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete,
                    bytes.Length, FileOptions.Asynchronous | FileOptions.SequentialScan);
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
            if (file is not null)
            {
                await using (file.ConfigureAwait(false))
                {
                    var creation = File.GetCreationTimeUtc(path);
                    var reset = initialized && (creation != created || file.Length < offset);
                    if (initialized && !reset && checkpoint.Length > 0)
                    {
                        file.Position = offset - checkpoint.Length;
                        var check = new byte[checkpoint.Length];
                        var count = await file.ReadAsync(check, cancellationToken).ConfigureAwait(false);
                        reset = count != check.Length || !check.AsSpan().SequenceEqual(checkpoint);
                    }
                    if (!initialized || reset)
                    {
                        offset = initialized ? 0 : Math.Min(position ?? file.Length, file.Length);
                        discardPartial = false;
                        if (!initialized && offset > 0)
                        {
                            file.Position = offset - 1;
                            discardPartial = file.ReadByte() != '\n';
                        }
                        decoder.Reset();
                        pending.Clear();
                        initialized = true;
                    }
                    created = creation;
                    file.Position = offset;
                    // Snapshot bounds each poll so a busy writer cannot starve cancellation or replacement checks.
                    var end = file.Length;
                    while (offset < end)
                    {
                        var count = await file.ReadAsync(bytes.AsMemory(0, (int)Math.Min(bytes.Length, end - offset)), cancellationToken).ConfigureAwait(false);
                        if (count == 0) break;
                        offset += count;
                        var length = decoder.GetChars(bytes, 0, count, chars, 0, false);
                        for (var i = 0; i < length; i++)
                        {
                            if (discardPartial)
                            {
                                if (chars[i] == '\n') discardPartial = false;
                                continue;
                            }
                            if (chars[i] == '\n')
                            {
                                yield return pending.ToString().TrimEnd('\r').TrimStart('\uFEFF');
                                pending.Clear();
                            }
                            else pending.Append(chars[i]);
                        }
                    }
                    checkpoint = new byte[(int)Math.Min(offset, 256)];
                    file.Position = offset - checkpoint.Length;
                    var checkpointCount = await file.ReadAsync(checkpoint, cancellationToken).ConfigureAwait(false);
                    if (checkpointCount != checkpoint.Length)
                    {
                        // A concurrent truncate will force a reset at the next poll.
                        created = DateTime.MinValue;
                        checkpoint = [];
                    }
                }
            }
            // IClock's current contract is synchronous; never block the caller/UI or leave a delay running.
            for (var remaining = pollingMilliseconds; remaining > 0; remaining -= 25)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var delay = Math.Min(remaining, 25);
                await Task.Run(() => clock.Delay(delay), cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
