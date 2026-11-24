using AIKernel.Kernel.Memory;

namespace AIKernel.Kernel.Inference;

/// <summary>A loaded text graph. Each request owns its generation state and observes the caller's cancellation.</summary>
public interface IModelSession : IDisposable
{
    /// <summary>The execution backend actually opened for this graph.</summary>
    string ExecutionBackend { get; }
    /// <summary>Generates a bounded greedy reply with the registered tokenizer and chat template.</summary>
    Task<CpuGeneration> GenerateAsync(string system, string user, int maxNewTokens = 128,
        CancellationToken cancellationToken = default);
}

/// <summary>A checked graph definition. Placement is resolved once when the session opens.</summary>
public interface IModelRegistration<out T> where T : IModelSession
{
    /// <summary>Opens the registered graph within the host and optional accelerator budgets.</summary>
    T OpenSession(string path, int threads = 4, int context = 1024,
        GgufMemoryOptions? memory = null, long? deviceBudgetBytes = null);
}

/// <summary>Built-in graph registrations validated against checkpoint metadata.</summary>
public static class ModelRegistration
{
    /// <summary>The tied-embedding E2B text graph with per-layer input and shared KV.</summary>
    public static IModelRegistration<GemmaTextSession> Gemma4E2B { get; } = new GemmaRegistration();

    /// <summary>The MXFP4 20B expert graph with Harmony final-channel generation.</summary>
    public static IModelRegistration<MixtureModelSession> GptOss20B { get; } = new ExpertRegistration();

    private sealed class GemmaRegistration : IModelRegistration<GemmaTextSession>
    {
        public GemmaTextSession OpenSession(string path, int threads, int context,
            GgufMemoryOptions? memory, long? deviceBudgetBytes) =>
            ModelPlacement.OpenGemma(path, threads, context, memory, PlacementBudget.Resolve(deviceBudgetBytes));
    }

    private sealed class ExpertRegistration : IModelRegistration<MixtureModelSession>
    {
        public MixtureModelSession OpenSession(string path, int threads, int context,
            GgufMemoryOptions? memory, long? deviceBudgetBytes)
        {
            _ = PlacementBudget.Resolve(deviceBudgetBytes);
            return new MixtureModelSession(path, threads, context, memory);
        }
    }
}

internal static class ModelPlacement
{
    internal static DenseModelSession OpenDense(DenseCheckpoint checkpoint, int threads, int context,
        GgufMemoryOptions? memory, long budget)
    {
        var placement = Select(checkpoint.Path, memory, budget, allowVulkan: true);
        return new(checkpoint, threads, context, placement.Ordinal, budget, vulkan: placement.Vulkan,
            instructionSet: "auto", memory: memory);
    }

    internal static GemmaTextSession OpenGemma(string path, int threads, int context,
        GgufMemoryOptions? memory, long budget)
    {
        var placement = Select(path, memory, budget, allowVulkan: false);
        var execution = new Gemma4ExecutionOptions
        {
            Backend = placement.Ordinal.HasValue ? Gemma4Backend.CudaResident : Gemma4Backend.Auto,
            MaximumResidentGpuBytes = budget, Memory = HostMemory(memory)
        };
        return new(path, threads, context, 4L << 30, execution);
    }

    private static Gemma4MemoryOptions? HostMemory(GgufMemoryOptions? memory) =>
        memory is null ? null : new()
        {
            MaximumResidentBytes = memory.MaximumResidentBytes, WeightCacheBytes = memory.WeightCacheBytes,
            InitialWeightCacheBytes = memory.InitialWeightCacheBytes, RuntimeReserveBytes = memory.RuntimeReserveBytes,
            SwapDirectory = memory.SwapDirectory
        };

    private static (int? Ordinal, bool Vulkan) Select(string path, GgufMemoryOptions? memory, long budget, bool allowVulkan)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(budget);
        if (memory is not null || budget == 0 || new FileInfo(path).Length > budget) return (null, false);
        var devices = CudaVirtualMemoryDevice.GetDeviceCount();
        if (devices.IsSuccess && devices.Value > 0) return (0, false);
        return allowVulkan ? FindVulkan() : (null, false);
    }

    private static (int? Ordinal, bool Vulkan) FindVulkan()
    {
        try
        {
            var device = VulkanQuantizedMatrix.Enumerate().FirstOrDefault();
            return (device?.Ordinal, device is not null);
        }
        catch (Exception error) when (error is DllNotFoundException or NotSupportedException)
        {
            return (null, false);
        }
    }
}
