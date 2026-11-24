using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AIKernel.Kernel.Inference;

internal static class PrefetchValidation
{
    internal static string TextHash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    internal static string Hash(float[] values) => Convert.ToHexStringLower(SHA256.HashData(MemoryMarshal.AsBytes(values.AsSpan())));

    internal static GgufMemoryOptions Options(string swap, string depth) => new()
    {
        SwapDirectory = swap,
        Prefetch = new() { Enabled = depth != "off", Depth = depth == "off" ? 1 : int.Parse(depth) }
    };

    internal static async Task Bonsai(string path, string profile, string output, GgufMemoryOptions memory)
    {
        var registration = DenseModelRegistration.Parse(File.ReadAllText(profile), "bonsai-1.7b");
        using var model = registration.OpenSession(path, 4, 256, memory with { WeightCacheBytes = 128L << 20 }, deviceBudgetBytes: 0);
        var rows = new List<object>();
        var prompts = new[] { ("Follow the user's formatting request.", "Reply with exactly the word BLUE."),
            ("You are a helpful assistant. Answer in the user's language.", "「こんにちは」とだけ答えてください。") };
        bool? recoveredAfterCancel = null;
        if (memory.Prefetch.Enabled)
        {
            using var cancelled = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            var run = model.GenerateBenchmarkAsync("Cancellation probe.", "Write a long story.", 128, cancelled.Token);
            while (model.GetWeightPrefetchStatistics()!.Value.Issued == 0 && !run.IsCompleted)
                await Task.Delay(2, cancelled.Token);
            cancelled.Cancel();
            try { await run; throw new InvalidDataException("Expected running generation to cancel."); }
            catch (OperationCanceledException) { recoveredAfterCancel = true; }
        }
        var start = model.GetWeightPrefetchStatistics();
        foreach (var (system, user) in prompts)
        {
            var logits = new List<string>();
            model.LogitsObserver = v => logits.Add(Hash(v));
            var normal = await model.GenerateAsync(system, user, 16);
            var fixedLength = await model.GenerateBenchmarkAsync(system, user, 16);
            rows.Add(new { user, normal.Text, textHash = TextHash(normal.Text), fixedLength.GeneratedTokenSha256, normalMilliseconds = normal.ElapsedMilliseconds, fixedMilliseconds = fixedLength.ElapsedMilliseconds, logits });
        }
        await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new { rows, recoveredAfterCancel, start, prefetch = model.GetWeightPrefetchStatistics() }));
    }
}
