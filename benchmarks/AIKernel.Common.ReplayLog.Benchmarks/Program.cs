using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using AIKernel.Common.Results;

var scenarios = new[] { "append", "prefix", "bind", "loop" };
var samples = 5;
var sizes = new[] { 1_000, 10_000 };
var batchSteps = 0;
var fingerprints = args.Contains("--fingerprints");
for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--samples": samples = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
        case "--scenario": scenarios = [args[++i]]; break;
        case "--sizes": sizes = args[++i].Split(',').Select(value => int.Parse(value, CultureInfo.InvariantCulture)).ToArray(); break;
        case "--batch-steps": batchSteps = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
        case "--fingerprints": break;
        default: throw new ArgumentException($"Unknown argument: {args[i]}");
    }
}
if (samples <= 0 || batchSteps < 0 || sizes.Any(size => size <= 0)) throw new ArgumentOutOfRangeException(nameof(args));

if (fingerprints)
{
    var results = new List<object>();
    foreach (var scenario in scenarios)
    foreach (var size in new[] { 0, 1, 7, 31 })
    {
        var step = Build(scenario, size);
        Capture(scenario, size, step);
        Capture(scenario + ".failure", size, step.Map<int>(_ => throw new IOException("fixture failure")));
        Capture(scenario + ".resume", size, PipelineStep.Resume(step.ReplayLog, 0, 1, "fixture resume"));
    }
    Console.WriteLine(JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
    return;

    void Capture(string scenario, int size, ResultStep<int, int> step) => results.Add(new
    {
        scenario, size, step.IsSuccess, step.State, step.Value, error = step.Error?.Code,
        step.StepId, step.ParentStepId, step.ReplayLogHash, step.ReplayLog
    });
}

Console.WriteLine($"# {RuntimeInformation.FrameworkDescription}; {RuntimeInformation.OSDescription}; CPUs={Environment.ProcessorCount}; TieredCompilation={Environment.GetEnvironmentVariable("DOTNET_TieredCompilation") ?? "default"}");
Console.WriteLine("scenario,steps,sample,elapsed_ms,allocated_bytes,replay_entries,replay_log_hash,iterations,cpu_ms,gc0,gc1,gc2");
using var process = Process.GetCurrentProcess();
foreach (var scenario in scenarios)
{
    // Warm up the same construction and final-hash path without measuring a unit-test deadline.
    _ = Build(scenario, 200).ReplayLogHash;
    for (var sample = 0; sample < samples; sample++)
    foreach (var size in sample % 2 == 0 ? sizes : sizes.Reverse())
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var iterations = Math.Max(1, batchSteps / size);
        var cpuStart = process.TotalProcessorTime;
        var gc0 = GC.CollectionCount(0);
        var gc1 = GC.CollectionCount(1);
        var gc2 = GC.CollectionCount(2);
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        var started = Stopwatch.GetTimestamp();
        ResultStep<int, int> step = default;
        string? hash = null;
        for (var iteration = 0; iteration < iterations; iteration++)
        {
            step = Build(scenario, size);
            var nextHash = step.ReplayLogHash;
            if (hash is not null && hash != nextHash) throw new InvalidOperationException("Hash changed within a batch.");
            hash = nextHash;
        }
        var elapsed = Stopwatch.GetElapsedTime(started);
        allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
        var cpu = process.TotalProcessorTime - cpuStart;
        Console.WriteLine(FormattableString.Invariant($"{scenario},{size},{sample},{elapsed.TotalMilliseconds / iterations:F6},{allocated / iterations},{step.ReplayLog.Count},{hash},{iterations},{cpu.TotalMilliseconds / iterations:F6},{GC.CollectionCount(0) - gc0},{GC.CollectionCount(1) - gc1},{GC.CollectionCount(2) - gc2}"));
        GC.KeepAlive(step);
    }
}

static ResultStep<int, int> Build(string scenario, int count)
{
    var step = ResultStep<int, int>.Success(0, 0);
    if (scenario == "loop")
        return PipelineStep.Loop(step, count, (i, value) => ResultStep<int, int>.Success(i + 1, value + 1));

    for (var i = 0; i < count; i++)
    {
        var delta = new SemanticDelta("benchmark.step", OriginStep.KernelFacade, SemanticSlot.T,
            ImmutableDictionary<string, string>.Empty.Add("iteration", i.ToString(CultureInfo.InvariantCulture)), "benchmark");
        step = scenario switch
        {
            "append" => step.WithSemanticDelta(delta),
            "prefix" => ResultStep<int, int>.Success(i + 1, i + 1)
                .WithReplayLogPrefix(step.ReplayLog)
                .WithSemanticDelta(delta, step.ReplayLog.Count == 0 ? null : step.ReplayLog[^1].StepId),
            "bind" => step.Bind(value => ResultStep<int, int>.Success(value + 1, value + 1).WithSemanticDelta(delta)),
            _ => throw new ArgumentException($"Unknown scenario: {scenario}")
        };
    }
    return step;
}
