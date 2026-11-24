using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using AIKernel.Kernel.Inference;
using Microsoft.Win32.SafeHandles;

internal static class PrefetchMissBenchmark
{
    private const int PageSize = 1 << 20;
    private const int Pages = 64;
    private const int Iterations = 256;

    private sealed record Trial(bool Enabled, double Milliseconds, long BackingBytesRead, WeightPrefetchStatistics Counts, string Digest);

    internal static void Run(string output)
    {
        using var file = new FileStream(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()),
            FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.DeleteOnClose | FileOptions.RandomAccess);
        byte[] buffer = new byte[PageSize];
        var random = new Random(555);
        for (int i = 0; i < Pages; i++) { random.NextBytes(buffer); file.Write(buffer); }
        file.Flush();
        var pages = Enumerable.Range(0, Pages).Select(i => ((long)i * PageSize, PageSize)).ToList();
        RunTrial(false, file.SafeFileHandle, pages);
        RunTrial(true, file.SafeFileHandle, pages);
        var runs = new List<Trial>();
        foreach (bool enabled in new[] { false, true, true, false, true, false, false, true })
            runs.Add(RunTrial(enabled, file.SafeFileHandle, pages));
        if (runs.Select(r => r.Digest).Distinct().Count() != 1) throw new InvalidDataException("Demand output changed.");
        File.WriteAllText(output, JsonSerializer.Serialize(new { PageSize, Pages, Iterations, runs }));
    }

    private static Trial RunTrial(bool enabled, SafeFileHandle handle, List<(long, int)> pages)
    {
        var backing = new WeightPageFile(handle, pages);
        var reader = enabled ? new PageReadAhead(2, PageSize, backing.Read) : null;
        backing.Ahead = reader;
        var buffer = new byte[PageSize];
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var watch = Stopwatch.StartNew();
        try
        {
            for (int i = 0; i < Iterations; i++)
            {
                int page = i % (Pages / 2);
                reader?.Offer(page + Pages / 2, i, default, expert: 1);
                backing.Demand(page, buffer);
                hash.AppendData(buffer, 0, 4096);
                reader?.RejectUnselected(i, [0]);
            }
        }
        finally { reader?.Dispose(); }
        watch.Stop();
        var stats = backing.Statistics;
        if (stats.DemandReads != Iterations || stats.Hits != 0 || (enabled && stats.Misses == 0))
            throw new InvalidDataException("The wrong-prediction workload was not exercised.");
        return new(enabled, watch.Elapsed.TotalMilliseconds, backing.Counts.Bytes, stats, Convert.ToHexStringLower(hash.GetHashAndReset()));
    }
}
