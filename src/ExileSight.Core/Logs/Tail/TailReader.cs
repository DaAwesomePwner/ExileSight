using System.Text;

namespace ExileSight.Core.Logs.Tail;

/// <summary>Enumeration-local state. All identity reads come from the open file handle.</summary>
internal sealed class TailReader(long? position, Action<string> warning)
{
    private const int MaxLineCharacters = 64 * 1024;
    private readonly Decoder decoder = Encoding.UTF8.GetDecoder();
    private readonly StringBuilder pending = new();
    private readonly byte[] bytes = new byte[8192];
    private readonly char[] chars = new char[Encoding.UTF8.GetMaxCharCount(8192)];
    private byte[] checkpoint = [];
    private DateTime created;
    private long offset;
    private bool initialized;
    private bool discardPartial;

    internal async Task<(IReadOnlyList<string> Lines, bool CaughtUp)> ReadAsync(string path, CancellationToken token)
    {
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete,
            bytes.Length, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var creation = File.GetCreationTimeUtc(file.SafeFileHandle);
        var reset = initialized && (creation != created || file.Length < offset);
        if (initialized && !reset && checkpoint.Length > 0)
        {
            file.Position = offset - checkpoint.Length;
            var check = new byte[checkpoint.Length];
            var read = await file.ReadAsync(check, token).ConfigureAwait(false);
            reset = read != check.Length || !check.AsSpan().SequenceEqual(checkpoint);
        }
        if (!initialized || reset)
        {
            var start = initialized ? 0 : Math.Min(position ?? file.Length, file.Length);
            // Read the initial checkpoint before committing state, so an I/O failure can safely retry.
            var initial = new byte[(int)Math.Min(start, 256)];
            file.Position = start - initial.Length;
            await file.ReadExactlyAsync(initial, token).ConfigureAwait(false);
            discardPartial = start > 0 && initial[^1] != '\n';
            checkpoint = initial;
            offset = start;
            decoder.Reset();
            pending.Clear();
            initialized = true;
        }
        created = creation;
        file.Position = offset;
        var end = file.Length;
        var count = await file.ReadAsync(bytes.AsMemory(0, (int)Math.Min(bytes.Length, Math.Max(0, end - offset))), token).ConfigureAwait(false);
        // No further file operations after advancing state: retries cannot lose a completed batch.
        var caughtUp = offset + count >= end || count == 0;
        if (count == 0) return ([], caughtUp);
        offset += count;
        checkpoint = checkpoint.Concat(bytes.Take(count)).TakeLast(256).ToArray();
        var length = decoder.GetChars(bytes, 0, count, chars, 0, false);
        var lines = new List<string>();
        for (var i = 0; i < length; i++)
        {
            if (discardPartial)
            {
                if (chars[i] == '\n') discardPartial = false;
                continue;
            }
            if (chars[i] == '\n')
            {
                lines.Add(pending.ToString().TrimEnd('\r').TrimStart('\uFEFF'));
                pending.Clear();
            }
            else if (pending.Length == MaxLineCharacters)
            {
                pending.Clear();
                discardPartial = true;
                warning("Discarded log line exceeding 65536 characters.");
            }
            else pending.Append(chars[i]);
        }
        return (lines, caughtUp);
    }
}
