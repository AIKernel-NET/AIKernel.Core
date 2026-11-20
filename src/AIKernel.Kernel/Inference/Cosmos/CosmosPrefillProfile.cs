using System.Diagnostics;
using System.Runtime.Intrinsics.X86;

namespace AIKernel.Kernel.Inference;

/// <summary>Coordinator wall-clock intervals during text prefill. CPU time covers all threads in the process.</summary>
public sealed record CosmosPrefillProfile(DateTimeOffset StartUtc, double ProcessCpuMilliseconds,
    double ProjectionRowsMilliseconds, double ProjectionPageSetupMilliseconds, double AttentionMilliseconds,
    int ProjectionPageVisits, int TileTokens, string Bf16DotKernel);

// A single request owns this collector. Workers never read clocks or update counters.
internal sealed class CosmosPrefillTimer
{
    private readonly TimeSpan cpuStart = ProcessCpuTime();
    private readonly DateTimeOffset startUtc = DateTimeOffset.UtcNow;
    private long rowTicks, pageSetupTicks, attentionTicks;
    private int pageVisits;
    private bool usedBf16;
    internal int TileTokens { get; set; } = 1;

    internal long Start() => Stopwatch.GetTimestamp();
    internal void Matrix(string dataType) => usedBf16 |= dataType == "BF16";
    internal void RowsCompleted(long start) => rowTicks += Stopwatch.GetTimestamp() - start;
    internal void PageReady(long start)
    {
        pageSetupTicks += Stopwatch.GetTimestamp() - start;
        pageVisits++;
    }
    internal void AttentionCompleted(long start) => attentionTicks += Stopwatch.GetTimestamp() - start;

    internal CosmosPrefillProfile Snapshot() => new(startUtc, (ProcessCpuTime() - cpuStart).TotalMilliseconds,
        Milliseconds(rowTicks), Milliseconds(pageSetupTicks), Milliseconds(attentionTicks), pageVisits, TileTokens,
        usedBf16 ? (Avx2.IsSupported ? "avx2-fp64" : "scalar-fp64") : "unused");

    private static double Milliseconds(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;
    private static TimeSpan ProcessCpuTime()
    {
        using var process = Process.GetCurrentProcess();
        return process.TotalProcessorTime;
    }
}
