namespace ExileSight.Core.Pricing;

/// <summary>Bounds streamed JSON even when Content-Length is absent or dishonest.</summary>
internal sealed class LimitedReadStream(Stream inner, long limit) : Stream
{
    private long read;
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => read; set => throw new NotSupportedException(); }
    public override int Read(byte[] buffer, int offset, int count)
    {
        var size = inner.Read(buffer, offset, (int)Math.Min(count, limit - read + 1));
        Count(size);
        return size;
    }
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var size = await inner.ReadAsync(buffer[..(int)Math.Min(buffer.Length, limit - read + 1)], cancellationToken).ConfigureAwait(false);
        Count(size);
        return size;
    }
    private void Count(int size)
    {
        read += size;
        if (read > limit) throw new HttpRequestException("Price response too large.");
    }
    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
