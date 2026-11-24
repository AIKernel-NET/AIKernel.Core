namespace AIKernel.Kernel.Inference;

/// <summary>One low-priority reader, bounded scratch slots, and a single owner for each pending page.</summary>
internal sealed class PageReadAhead : IDisposable
{
    internal delegate void ReadPage(long page, Span<byte> destination, CancellationToken cancellation);
    private readonly object gate = new();
    private readonly PrefetchSlot[] slots;
    private readonly ReadPage read;
    private readonly int pageSize;
    private readonly Thread worker;
    private readonly ExpertReadBackoff prediction = new();
    private bool closed;
    private long issued, hits, misses, cancelled, failures, waited, demandReads;

    internal PageReadAhead(int capacity, int pageSize, ReadPage read)
    {
        this.pageSize = pageSize;
        this.read = read;
        slots = Enumerable.Range(0, capacity).Select(_ => new PrefetchSlot()).ToArray();
        worker = new Thread(Work) { IsBackground = true, Name = "AIKernel weight read-ahead", Priority = ThreadPriority.BelowNormal };
        worker.Start();
    }

    internal WeightPrefetchStatistics Snapshot()
    {
        lock (gate) return new(issued, hits, misses, cancelled, failures, waited, demandReads);
    }

    internal void Offer(long page, long expires, CancellationToken token, int expert = -1)
    {
        lock (gate)
        {
            if (closed || token.IsCancellationRequested || prediction.Pauses(expert, expires) || Find(page) != null) return;
            var slot = Array.Find(slots, x => x.State == PrefetchState.Free);
            if (slot == null) return;
            slot.Queue(page, expires, token);
            slot.Expert = expert;
            issued++;
            Monitor.PulseAll(gate);
        }
    }

    private PrefetchSlot? Find(long page) => Array.Find(slots, x => x.State != PrefetchState.Free && x.Page == page);

    internal void Expire(long epoch)
    {
        lock (gate)
            foreach (var slot in slots)
                if (slot.State != PrefetchState.Free && slot.Expires < epoch) Drop(slot, false);
    }

    internal void RejectUnselected(long epoch, ReadOnlySpan<int> selected)
    {
        lock (gate)
            foreach (var slot in slots)
                if (slot.Expires == epoch && slot.Expert >= 0 && !selected.Contains(slot.Expert)) Drop(slot, false);
    }

    internal void Relinquish()
    {
        lock (gate)
        {
            Cancel();
            foreach (var slot in slots) slot.Relinquish();
        }
    }

    internal void Cancel()
    {
        lock (gate)
        {
            foreach (var slot in slots) Drop(slot, true);
            prediction.Reset();
        }
    }

    private void Drop(PrefetchSlot slot, bool cancellation)
    {
        if (slot.State == PrefetchState.Free || slot.Discard) return;
        if (cancellation) cancelled++;
        else
        {
            misses++;
            prediction.Missed(slot.Expert, slot.Expires);
        }
        slot.Abandon();
        Monitor.PulseAll(gate);
    }

    internal bool Consume(long page, Span<byte> destination)
    {
        lock (gate)
        {
            var slot = Find(page);
            if (slot == null) { demandReads++; return false; }
            if (slot.State == PrefetchState.Reading) waited++;
            while (slot.State == PrefetchState.Reading && slot.Page == page) Monitor.Wait(gate);
            return Claim(slot, page, destination);
        }
    }

    private bool Claim(PrefetchSlot slot, long page, Span<byte> destination)
    {
        if (slot.Page == page && slot.State == PrefetchState.Ready)
        {
            slot.Buffer.AsSpan().CopyTo(destination);
            if (slot.Expert >= 0) prediction.Reset();
            slot.Release();
            hits++;
            return true;
        }
        if (slot.Page == page && slot.State == PrefetchState.Queued) Drop(slot, true);
        demandReads++;
        return false;
    }

    private void Work()
    {
        while (Next() is { } slot)
        {
            bool success = false;
            try
            {
                slot.Buffer ??= GC.AllocateUninitializedArray<byte>(pageSize);
                read(slot.Page, slot.Buffer, slot.Cancellation!.Token);
                success = true;
            }
            catch (Exception error) when (error is IOException or OperationCanceledException or OutOfMemoryException or UnauthorizedAccessException)
            { /* Demand uses its normal failure semantics and retries an optional read failure. */ }
            lock (gate) Complete(slot, success);
        }
    }

    private PrefetchSlot? Next()
    {
        lock (gate)
        {
            while (!closed)
            {
                var slot = Array.Find(slots, x => x.State == PrefetchState.Queued);
                if (slot != null) { slot.State = PrefetchState.Reading; return slot; }
                Monitor.Wait(gate);
            }
            return null;
        }
    }

    private void Complete(PrefetchSlot slot, bool success)
    {
        if (slot.Discard) slot.Release();
        else if (slot.Cancellation!.IsCancellationRequested) { cancelled++; slot.Release(); }
        else if (success) slot.State = PrefetchState.Ready;
        else { failures++; slot.Release(); }
        Monitor.PulseAll(gate);
    }

    public void Dispose()
    {
        lock (gate) { closed = true; Cancel(); Monitor.PulseAll(gate); }
        worker.Join();
    }
}

internal enum PrefetchState { Free, Queued, Reading, Ready }

/// <summary>Limit wasted expert I/O after a miss. Successful demand and the end of a request reset the history.</summary>
internal sealed class ExpertReadBackoff
{
    private int interval = 16;
    private long resume;
    internal bool Pauses(int expert, long epoch) => expert >= 0 && epoch < resume;

    internal void Missed(int expert, long epoch)
    {
        if (expert < 0 || epoch < resume) return;
        resume = epoch + interval;
        interval = Math.Min(128, interval * 2);
    }

    internal void Reset()
    {
        interval = 16;
        resume = 0;
    }
}

internal sealed class PrefetchSlot
{
    internal PrefetchState State;
    internal long Page, Expires;
    internal byte[]? Buffer;
    internal int Expert;
    private bool relinquishing;
    internal bool Discard;
    internal void Relinquish()
    {
        if (State == PrefetchState.Reading) relinquishing = true;
        else Buffer = null;
    }
    internal CancellationTokenSource? Cancellation;
    internal void Queue(long page, long expires, CancellationToken token)
    {
        Page = page;
        Expires = expires;
        Cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        Discard = false;
        State = PrefetchState.Queued;
    }
    internal void Abandon()
    {
        if (State != PrefetchState.Reading) { Release(); return; }
        Discard = true;
        Cancellation!.Cancel();
    }
    internal void Release()
    {
        if (relinquishing) Buffer = null;
        relinquishing = false;
        Cancellation?.Dispose();
        Cancellation = null;
        State = PrefetchState.Free;
        Discard = false;
    }
}
