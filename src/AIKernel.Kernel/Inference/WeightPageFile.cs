using Microsoft.Win32.SafeHandles;

namespace AIKernel.Kernel.Inference;

/// <summary>Serves VM faults and optional reads from the same immutable backing handle.</summary>
internal sealed class WeightPageFile
{
    private readonly bool[] loaded;
    private readonly SafeFileHandle handle;
    private readonly List<(long Offset, int Bytes)> pages;
    private long demand;
    internal PageReadAhead? Ahead;
    internal long BackingBytesRead { get; private set; }
    internal long DistinctPagesRead { get; private set; }
    internal (long Bytes, long Distinct) Counts { get { lock (loaded) return (BackingBytesRead, DistinctPagesRead); } }

    internal WeightPageFile(SafeFileHandle handle, List<(long Offset, int Bytes)> pages)
    {
        this.handle = handle;
        this.pages = pages;
        loaded = new bool[pages.Count];
    }
    internal WeightPrefetchStatistics Statistics => Ahead?.Snapshot() ?? new(0, 0, 0, 0, 0, 0, Interlocked.Read(ref demand));

    internal void Demand(long page, Span<byte> destination)
    {
        if (Ahead?.Consume(page, destination) == true) return;
        Interlocked.Increment(ref demand);
        Read(page, destination, CancellationToken.None);
    }

    internal void Read(long index, Span<byte> destination, CancellationToken token)
    {
        var page = pages[checked((int)index)];
        Fill(destination[..page.Bytes], page.Offset, token);
        destination[page.Bytes..].Clear();
        lock (loaded)
        {
            BackingBytesRead += page.Bytes;
            if (!loaded[index]) { loaded[index] = true; DistinctPagesRead++; }
        }
    }

    private void Fill(Span<byte> target, long offset, CancellationToken token)
    {
        while (!target.IsEmpty)
        {
            token.ThrowIfCancellationRequested();
            int count = RandomAccess.Read(handle, token.CanBeCanceled ? target[..Math.Min(target.Length, 256 * 1024)] : target, offset);
            if (count == 0) throw new EndOfStreamException("Truncated encoded backing file.");
            target = target[count..];
            offset += count;
        }
    }
}
