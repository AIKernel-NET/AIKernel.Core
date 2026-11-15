namespace AIKernel.Kernel.Inference;

/// <summary>Explicit Vulkan Q1_0/FP32 matrix offload with CPU attention, normalization, KV and sampling.</summary>
/// <remarks>Requires a Vulkan 1.1 driver with coherent host-visible storage memory. No CUDA or LibTorch is loaded.
/// Device ordinals are Vulkan-specific. Failures are reported rather than silently using CPU matrices.</remarks>
public sealed class Qwen3VulkanModel : IDisposable
{
    private readonly Qwen3CpuModel engine;
    internal object? Diagnostics=>engine.VulkanDiagnostics;
    /// <summary>Lists compute devices without allocating model memory.</summary>
    public static IReadOnlyList<VulkanComputeDeviceInfo> EnumerateDevices()=>VulkanQuantizedMatrix.Enumerate();
    /// <summary>Loads a bounded model onto the explicitly selected Vulkan physical device.</summary>
    public Qwen3VulkanModel(string modelPath,int deviceOrdinal,int cpuThreads=4,int contextTokens=1024,
        long maximumWeightBytes=1024L*1024*1024,bool quantizeActivations=false)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(deviceOrdinal);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumWeightBytes);
        engine=new(modelPath,cpuThreads,contextTokens,deviceOrdinal,maximumWeightBytes,vulkan:true,gpuQ8:quantizeActivations);
        ActivationPrecision=quantizeActivations?"q8-block128-fp32-accumulate":"fp32";
    }
    /// <summary>Actual physical device used by every matrix dispatch.</summary>
    public VulkanComputeDeviceInfo Device=>engine.VulkanDevice!;
    /// <summary>Encoded resident model size; additional scratch buffers are bounded to 16 MiB total.</summary>
    public long DeviceWeightBytes=>engine.DeviceWeightBytes;
    /// <summary>Activation rounding; the Vulkan shader uses FP32 arithmetic.</summary>
    public string ActivationPrecision {get;}
    /// <summary>Precomputes a trusted system prefix.</summary>
    public async Task WarmupAsync(string system,CancellationToken cancellationToken=default)
    {
        try{await engine.WarmupAsync(system,cancellationToken).ConfigureAwait(false);}
        finally{engine.ZeroGpuScratch();}
    }
    /// <summary>Generates a bounded reply, with serialized requests and private request state.</summary>
    public async Task<VulkanGeneration> GenerateAsync(string system,string user,int maxNewTokens=128,CancellationToken cancellationToken=default)
    {
        try{return Convert(await engine.GenerateAsync(system,user,maxNewTokens,cancellationToken).ConfigureAwait(false));}
        finally{engine.ZeroGpuScratch();}
    }
    /// <summary>Benchmark only: greedy generation for exactly tokenCount steps, ignoring EOS.</summary>
    public async Task<VulkanGeneration> GenerateBenchmarkAsync(string system,string user,int tokenCount=128,CancellationToken cancellationToken=default)
    {
        try{return Convert(await engine.GenerateBenchmarkAsync(system,user,tokenCount,cancellationToken).ConfigureAwait(false));}
        finally{engine.ZeroGpuScratch();}
    }
    /// <summary>Scores explicit single-token labels.</summary>
    public async Task<VulkanGeneration> ClassifyAsync(string system,string user,IReadOnlyList<string> labels,CancellationToken cancellationToken=default)
    {
        try{return Convert(await engine.ClassifyAsync(system,user,labels,cancellationToken).ConfigureAwait(false));}
        finally{engine.ZeroGpuScratch();}
    }
    private VulkanGeneration Convert(CpuGeneration r)=>new(r.Text,r.InputTokens,r.OutputTokens,r.FinishReason,r.ElapsedMilliseconds,
        r.PrefillMilliseconds,r.DecodeMilliseconds,r.PrefillForwardTokens,r.DecodeForwardTokens,r.PrefillThreads,r.DecodeThreads,Device,DeviceWeightBytes)
        {ActivationPrecision=ActivationPrecision,GeneratedTokenSha256=r.GeneratedTokenSha256};
    /// <summary>Releases resources after active requests have stopped.</summary>
    public void Dispose()=>engine.Dispose();
}

/// <summary>Measured Vulkan matrix offload result, including CPU work and transfers.</summary>
public sealed record VulkanGeneration(string Text,int InputTokens,int OutputTokens,string FinishReason,long ElapsedMilliseconds,
    double PrefillMilliseconds,double DecodeMilliseconds,int PrefillForwardTokens,int DecodeForwardTokens,
    int PrefillThreads,int DecodeThreads,VulkanComputeDeviceInfo Device,long DeviceWeightBytes)
{
    /// <summary>The actual matrix execution path.</summary>
    public string Backend=>"vulkan-matrix-offload";
    /// <summary>Input activation representation; Vulkan accumulation remains FP32.</summary>
    public string ActivationPrecision {get;init;}="fp32";
    /// <summary>Benchmark-only generated token hash; benchmark Text is empty.</summary>
    public string? GeneratedTokenSha256 {get;init;}
    /// <summary>Benchmark-only generated token hash; benchmark Text is empty.</summary>
    public string? GeneratedTokenSha256 {get;init;}
}
