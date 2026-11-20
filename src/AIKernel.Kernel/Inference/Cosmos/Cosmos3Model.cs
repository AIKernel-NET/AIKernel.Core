namespace AIKernel.Kernel.Inference;

/// <summary>Cosmos3 variants supported for local text/image/video understanding.</summary>
public enum Cosmos3Variant
{
    /// <summary>Cosmos3 Edge dense ReLU² reasoner and SigLIP2 vision encoder.</summary>
    Edge,
    /// <summary>Cosmos3 Nano Qwen3-VL reasoner and DeepStack vision encoder.</summary>
    Nano
}

/// <summary>A completed user turn, including its media, and the visible assistant reply.</summary>
public sealed record CosmosChatTurn(IReadOnlyList<CosmosContent> UserContent, string AssistantReply);

/// <summary>AIKernel's managed CPU Cosmos3 reasoner. Uses imported safetensors with
/// unchanged FP32/BF16/FP16 weights. No Python, LibTorch, CUDA or API is needed at runtime.
/// This API produces text; Cosmos diffusion, audio and action generation are not implemented.</summary>
public sealed class Cosmos3Model : IDisposable
{
    private readonly CosmosModelRuntime runtime;
    /// <summary>Loads a local checkpoint prepared with tools/models/import_cosmos3.py.</summary>
    public Cosmos3Model(string modelDirectory,Cosmos3Variant variant,CosmosExecutionOptions? execution=null)
        =>runtime=new(modelDirectory,execution,Family(variant));

    /// <summary>Checks the pinned manifest, index and required files without hashing model weights.
    /// Loading the model with the same pin verifies all declared file digests.</summary>
    public static bool IsCheckpointAvailable(string modelDirectory, Cosmos3Variant variant, string expectedManifestSha256)
    {
        if (string.IsNullOrEmpty(expectedManifestSha256)) return false;
        return CosmosArtifactVerifier.IsAvailable(modelDirectory, Family(variant), expectedManifestSha256);
    }

    private static string Family(Cosmos3Variant variant) => variant switch
    {
        Cosmos3Variant.Edge => "cosmos3_edge",
        Cosmos3Variant.Nano => "cosmos3_omni",
        _ => throw new ArgumentOutOfRangeException(nameof(variant))
    };
    /// <summary>Generates a greedy reply with bounded context, images and timestamped frames.
    /// RGB dimensions must be multiples of 32; callers explicitly resize/sample media before admission.</summary>
    public Task<CosmosGeneration> GenerateAsync(string system,IReadOnlyList<CosmosContent> content,int maxNewTokens=128,CancellationToken cancellationToken=default)
        =>runtime.GenerateAsync(system,content,maxNewTokens,cancellationToken);

    /// <summary>Continues a conversation using the model's role delimiters. History is private to this request.</summary>
    public Task<CosmosGeneration> GenerateChatAsync(string system, IReadOnlyList<CosmosChatTurn> history,
        IReadOnlyList<CosmosContent> content, int maxNewTokens=128, CancellationToken cancellationToken=default)
    {
        ArgumentNullException.ThrowIfNull(history);
        return runtime.GenerateAsync(system,content,maxNewTokens,cancellationToken,history);
    }
    /// <summary>Memory paging counters; null when paging is disabled. Read between requests.</summary>
    public CosmosMemoryStatistics? MemoryStatistics=>runtime.MemoryStatistics;
    /// <summary>Waits for active generation, then releases model mappings.</summary>
    public void Dispose()=>runtime.Dispose();
}
