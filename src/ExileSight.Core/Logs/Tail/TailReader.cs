using System.Text;
using System.Runtime.CompilerServices;
using ExileSight.Platform.Abstractions.Files;

namespace ExileSight.Core.Logs.Tail;

/// <summary>Enumeration-local state. All identity reads come from the open file handle.</summary>
internal sealed class TailReader(long? position, Action<FileTailCondition> warning, IFileTailIdentityProvider identityProvider)
{
    private const int MaxLineCharacters = 64 * 1024;
    private readonly Decoder decoder = Encoding.UTF8.GetDecoder();
    private readonly StringBuilder pending = new();
    private readonly byte[] bytes = new byte[8192];
    private readonly char[] chars = new char[Encoding.UTF8.GetMaxCharCount(8192)];
    private byte[] checkpoint = [];
    private FileTailIdentity identity;
    private byte[] prefix = [];
    private long offset;
    private bool initialized;
    private bool discardPartial;

    internal async IAsyncEnumerable<string> ReadAsync(string path, [EnumeratorCancellation] CancellationToken token)
    {
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete,
            1, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var currentIdentity = identityProvider.Read(file);
        var stableCreation = identity.HasStableCreationTime && currentIdentity.HasStableCreationTime;
        var reset = initialized && (file.Length < offset || stableCreation && currentIdentity.CreationUtc != identity.CreationUtc);
        var currentPrefix = new byte[(int)Math.Min(file.Length, 256)];
        await file.ReadExactlyAsync(currentPrefix, token).ConfigureAwait(false);
        if (initialized && (currentPrefix.Length < prefix.Length
            || !currentPrefix.AsSpan(0, Math.Min(prefix.Length, currentPrefix.Length)).SequenceEqual(prefix))) reset = true;
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
        identity = currentIdentity;
        prefix = currentPrefix;
        file.Position = offset;
        // The reader already owns its byte buffer. Do not allocate a FileStream buffer
        // or schedule an empty read on every quiet poll; later appends are read next poll.
        if (file.Length == offset) yield break;
        while (true)
        {
            token.ThrowIfCancellationRequested();
            var count = await file.ReadAsync(bytes, token).ConfigureAwait(false);
            if (count == 0) yield break;
            // Advance and yield each bounded batch before another file operation can fail.
            foreach (var line in Decode(count)) yield return line;
        }
    }

    private IReadOnlyList<string> Decode(int count)
    {
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
                warning(FileTailCondition.OversizedLine);
            }
            else pending.Append(chars[i]);
        }
        return lines;
    }
}
