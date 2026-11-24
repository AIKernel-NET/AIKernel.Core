using AIKernel.Kernel.Inference;

namespace AIKernel.Core.Tests.Inference;

public sealed class WeightPrefetchTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task DemandJoinsTheSameReadAndQueuedDemandTakesPriority()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        int reads = 0;
        using var reader = new PageReadAhead(2, 16, (page, buffer, token) =>
        {
            Interlocked.Increment(ref reads);
            entered.Set();
            if (!release.Wait(Timeout, token)) throw new IOException("Test read timed out.");
            buffer.Fill((byte)page);
        });
        reader.Offer(17, 1, default);
        Assert.True(entered.Wait(Timeout));
        try
        {
            reader.Offer(17, 1, default);
            reader.Offer(18, 1, default);
            Assert.False(reader.Consume(18, new byte[16]));
            byte[] result = new byte[16];
            var demand = Task.Run(() => reader.Consume(17, result));
            Assert.True(SpinWait.SpinUntil(() => reader.Snapshot().WaitedForInFlight == 1, Timeout));
            release.Set();
            Assert.True(await demand.WaitAsync(Timeout));
            Assert.All(result, value => Assert.Equal((byte)17, value));
            Assert.Equal(new WeightPrefetchStatistics(2, 1, 0, 1, 0, 1, 1), reader.Snapshot());
            Assert.Equal(1, reads);
        }
        finally { release.Set(); }
    }

    [Fact]
    public void MispredictionReleasesItsBudgetAndCancellationAllowsReuse()
    {
        using var started = new ManualResetEventSlim();
        using var stopped = new ManualResetEventSlim();
        using var nextStarted = new ManualResetEventSlim();
        using var reader = new PageReadAhead(2, 32, (page, buffer, token) =>
        {
            if (page != 2) { buffer.Fill(91); nextStarted.Set(); return; }
            started.Set();
            token.WaitHandle.WaitOne(Timeout);
            stopped.Set();
            token.ThrowIfCancellationRequested();
        });
        using var request = new CancellationTokenSource();
        reader.Offer(2, 4, request.Token, expert: 4);
        Assert.True(started.Wait(Timeout));
        reader.RejectUnselected(4, [3, 9]);
        Assert.True(stopped.Wait(Timeout));
        request.Cancel();
        reader.Relinquish();
        reader.Offer(3, 5, default, expert: 9);
        Assert.True(nextStarted.Wait(Timeout));
        var result = new byte[32];
        Assert.True(reader.Consume(3, result));
        Assert.All(result, value => Assert.Equal((byte)91, value));
        Assert.Equal(1, reader.Snapshot().Misses);
        Assert.Equal(0, reader.Snapshot().DemandReads);
    }

    [Fact]
    public void ConsecutiveExpertMissesBackOffWithoutBlockingDenseReads()
    {
        var history = new ExpertReadBackoff();
        int probes = 0;
        for (int layer = 0; layer < 256; layer++)
        {
            Assert.False(history.Pauses(-1, layer));
            if (history.Pauses(2, layer)) continue;
            history.Missed(2, layer);
            probes++;
        }
        Assert.InRange(probes, 2, 6);
        history.Reset();
        Assert.False(history.Pauses(2, 1));
    }

    [Fact]
    public void FailedReadFallsBackAndDoesNotPoisonThePage()
    {
        int calls = 0;
        using var completed = new ManualResetEventSlim();
        using var reader = new PageReadAhead(1, 8, (_, buffer, _) =>
        {
            if (Interlocked.Increment(ref calls) == 1) throw new IOException("Optional read failed.");
            buffer.Fill(42);
            completed.Set();
        });
        reader.Offer(8, 1, default);
        Assert.True(SpinWait.SpinUntil(() => reader.Snapshot().Failures == 1, Timeout));
        Assert.False(reader.Consume(8, new byte[8]));
        reader.Offer(8, 2, default);
        Assert.True(completed.Wait(Timeout));
        Assert.True(reader.Consume(8, new byte[8]));
        Assert.Equal(1, reader.Snapshot().DemandReads);
    }

    [Fact]
    public void StagingAndObservationLeaveDemandResidencyAndEvictionOrderIntact()
    {
        var directory = Directory.CreateTempSubdirectory();
        var path = Path.Combine(directory.FullName, "pages");
        const int size = 4096;
        try
        {
            File.WriteAllBytes(path, new byte[3 * size]);
            var tensors = Enumerable.Range(0, 3).Select(i => new ModelTensor(i * size, 1, size / 4, size, "F32", 1, 4)).ToArray();
            var memory = new GgufMemoryOptions { SwapDirectory = directory.FullName, WeightCacheBytes = 2 * size, Prefetch = new() { Enabled = true } };
            using var store = new TensorPageStore(path, tensors, GgufDenseWeights.PageBudget(memory), size).Configure(memory);
            store.VisitRow(tensors[0], 0, _ => { });
            var hints = Assert.IsType<WeightPageHints>(store.Hints);
            hints.Selected(1, [1]);
            hints.Experts(1, 0, size, default);
            Assert.True(SpinWait.SpinUntil(() => store.BackingBytesRead == 2 * size, Timeout));
            Assert.Equal(0, store.Observe(size).ResidentPages);
            Assert.Equal(1, store.Statistics.BackingReads);
            store.VisitRow(tensors[1], 0, _ => { });
            Assert.Equal(1, store.PrefetchStatistics.Hits);
            for (int i = 0; i < 4; i++) Assert.Equal(1, store.Observe(0).ResidentPages);
            store.VisitRow(tensors[2], 0, _ => { });
            Assert.Equal(0, store.Observe(0).ResidentPages);
            Assert.Equal(1, store.Observe(size).ResidentPages);
        }
        finally { directory.Delete(true); }
    }

    [Fact]
    public void HostBoundsApplyEvenBeforeEnablingReadAhead()
    {
        Assert.False(new GgufMemoryOptions { SwapDirectory = Path.GetTempPath() }.Prefetch.Enabled);
        Assert.Throws<ArgumentOutOfRangeException>(() => new WeightPrefetchOptions { Depth = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new WeightPrefetchOptions { Depth = 5 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new WeightPrefetchOptions { MaximumBytes = (1L << 20) - 1 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new WeightPrefetchOptions { MaximumBytes = (64L << 20) + 1 });
    }
}
