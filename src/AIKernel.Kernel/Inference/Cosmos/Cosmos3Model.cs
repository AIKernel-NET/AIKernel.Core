namespace AIKernel.Kernel.Inference;

/// <summary>Cosmos3 variants supported for local text/image/video understanding.</summary>
public enum Cosmos3Variant
{
    /// <summary>Cosmos3 Edge dense ReLU² reasoner and SigLIP2 vision encoder.</summary>
    Edge,
    /// <summary>Cosmos3 Nano Qwen3-VL reasoner and DeepStack vision encoder.</summary>
    Nano
}

/// <summary>AIKernel's managed CPU Cosmos3 reasoner. Uses imported safetensors with
/// unchanged FP32/BF16/FP16 weights. No Python, LibTorch, CUDA or API is needed at runtime.
/// This API produces text; Cosmos diffusion, audio and action generation are not implemented.</summary>
public sealed class Cosmos3Model : IDisposable
{
    private readonly CosmosModelRuntime runtime;
    /// <summary>Loads a local checkpoint prepared with tools/models/import_cosmos3.py.</summary>
    public Cosmos3Model(string modelDirectory,Cosmos3Variant variant,CosmosExecutionOptions? execution=null)
        =>runtime=new(modelDirectory,execution,variant switch
        {Cosmos3Variant.Edge=>"cosmos3_edge",Cosmos3Variant.Nano=>"cosmos3_omni",_=>throw new ArgumentOutOfRangeException(nameof(variant))});
    /// <summary>Generates a greedy reply with bounded context, images and timestamped frames.
    /// RGB dimensions must be multiples of 32; callers explicitly resize/sample media before admission.</summary>
    public Task<CosmosGeneration> GenerateAsync(string system,IReadOnlyList<CosmosContent> content,int maxNewTokens=128,CancellationToken cancellationToken=default)
        =>runtime.GenerateAsync(system,content,maxNewTokens,cancellationToken);
    /// <summary>Waits for active generation, then releases model mappings.</summary>
    public void Dispose()=>runtime.Dispose();
}
