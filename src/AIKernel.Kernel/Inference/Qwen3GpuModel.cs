namespace AIKernel.Kernel.Inference;

/// <summary>Qwen3 dense inference with resident quantized CUDA matrices and CPU attention, normalization and KV state.</summary>
/// <remarks>Requires AIKernel's native CUDA Driver library and an NVIDIA driver. No Toolkit or LibTorch is loaded.
/// GPU failures are reported; this class never silently falls back to CPU matrix multiplication.</remarks>
public sealed class Qwen3GpuModel : IDisposable
{
    private readonly Qwen3CpuModel engine;
    /// <summary>Loads a bounded model and uploads its encoded weights to a private device context.</summary>
    /// <param name="modelPath">A supported Qwen3 dense GGUF with Q1_0 or FP32 matrices.</param>
    /// <param name="deviceOrdinal">CUDA device ordinal, starting at zero.</param>
    /// <param name="cpuThreads">Worker ceiling for the CPU operations, from one to six.</param>
    /// <param name="contextTokens">Private request context, from 64 to 2048 tokens.</param>
    /// <param name="maximumWeightBytes">Encoded model allocation limit. Driver context and matrix working buffers are additional; upload staging is bounded to one MiB.</param>
    public Qwen3GpuModel(string modelPath,int deviceOrdinal=0,int cpuThreads=4,int contextTokens=1024,
        long maximumWeightBytes=1024L*1024*1024)
        :this(modelPath,deviceOrdinal,cpuThreads,contextTokens,maximumWeightBytes,false) { }
    /// <summary>Optionally rounds Q1_0-matrix activations to Q8 block128 before the existing FP32 CUDA arithmetic.
    /// This does not use GPU INT8 instructions or Tensor Cores. Original constructor behavior is unchanged.</summary>
    public Qwen3GpuModel(string modelPath,int deviceOrdinal,int cpuThreads,int contextTokens,
        long maximumWeightBytes,bool quantizeActivations)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(deviceOrdinal);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumWeightBytes);
        engine=new(modelPath,cpuThreads,contextTokens,deviceOrdinal,maximumWeightBytes,gpuQ8:quantizeActivations);
        DeviceOrdinal=deviceOrdinal;
        ActivationPrecision=quantizeActivations?"q8-block128-fp32-accumulate":"fp32";
    }
    /// <summary>Uses native INT8 accumulation, optionally measured per shape to select DP4A or Tensor Cores.
    /// Activations are Q8 block128, with FP32 scaling of integer block sums. Unsupported forced kernels fail.</summary>
    public Qwen3GpuModel(string modelPath,int deviceOrdinal,int cpuThreads,int contextTokens,
        long maximumWeightBytes,CudaQ8Kernel kernel)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(deviceOrdinal);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumWeightBytes);
        if(!Enum.IsDefined(kernel))throw new ArgumentOutOfRangeException(nameof(kernel));
        engine=new(modelPath,cpuThreads,contextTokens,deviceOrdinal,maximumWeightBytes,gpuInt8:kernel);
        DeviceOrdinal=deviceOrdinal;ActivationPrecision="q8-block128-int32-accumulate";
    }
    /// <summary>Actual integer kernel call counts and calibrated choices, or zero counts for legacy precision.</summary>
    public CudaQ8Statistics? Int8Statistics=>engine.CudaQ8Statistics;
    /// <summary>The CUDA ordinal used for every matrix multiplication.</summary>
    public int DeviceOrdinal {get;}
    /// <summary>Activation rounding and arithmetic used for Q1_0 matrices.</summary>
    public string ActivationPrecision {get;}
    /// <summary>PCI identity of the device used, for matching telemetry.</summary>
    public string? DevicePciBusId=>engine.DevicePciBusId;
    /// <summary>Resident encoded model bytes, excluding driver context and scratch buffers.</summary>
    public long DeviceWeightBytes=>engine.DeviceWeightBytes;
    /// <summary>Precomputes a trusted system-prefix KV cache.</summary>
    public async Task WarmupAsync(string system,CancellationToken cancellationToken=default)
    {
        try{await engine.WarmupAsync(system,cancellationToken).ConfigureAwait(false);}
        finally{engine.ZeroGpuScratch();}
    }
    /// <summary>Generates a bounded reply; concurrent requests are serialized.</summary>
    public async Task<GpuGeneration> GenerateAsync(string system,string user,int maxNewTokens=128,CancellationToken cancellationToken=default)
    {
        try{return Convert(await engine.GenerateAsync(system,user,maxNewTokens,cancellationToken).ConfigureAwait(false));}
        finally{engine.ZeroGpuScratch();}
    }
    /// <summary>Benchmark only: greedy generation for exactly tokenCount steps, ignoring EOS.</summary>
    public async Task<GpuGeneration> GenerateBenchmarkAsync(string system,string user,int tokenCount=128,CancellationToken cancellationToken=default)
    {
        try{return Convert(await engine.GenerateBenchmarkAsync(system,user,tokenCount,cancellationToken).ConfigureAwait(false));}
        finally{engine.ZeroGpuScratch();}
    }
    /// <summary>Scores explicit single-token labels.</summary>
    public async Task<GpuGeneration> ClassifyAsync(string system,string user,IReadOnlyList<string> labels,CancellationToken cancellationToken=default)
    {
        try{return Convert(await engine.ClassifyAsync(system,user,labels,cancellationToken).ConfigureAwait(false));}
        finally{engine.ZeroGpuScratch();}
    }
    private GpuGeneration Convert(CpuGeneration result)=>new(result.Text,result.InputTokens,result.OutputTokens,
        result.FinishReason,result.ElapsedMilliseconds,result.PrefillMilliseconds,result.DecodeMilliseconds,
        result.PrefillForwardTokens,result.DecodeForwardTokens,result.PrefillThreads,result.DecodeThreads,
        DeviceOrdinal,DeviceWeightBytes){ActivationPrecision=ActivationPrecision,GeneratedTokenSha256=result.GeneratedTokenSha256,Profile=result.Profile};
    /// <summary>Releases model mappings, KV prefix and device context after active requests have stopped.</summary>
    public void Dispose()=>engine.Dispose();
}

/// <summary>Measured hybrid inference result. Timings cover CPU operations, transfers and CUDA matrix work.</summary>
public sealed record GpuGeneration(string Text,int InputTokens,int OutputTokens,string FinishReason,long ElapsedMilliseconds,
    double PrefillMilliseconds,double DecodeMilliseconds,int PrefillForwardTokens,int DecodeForwardTokens,
    int PrefillThreads,int DecodeThreads,int DeviceOrdinal,long DeviceWeightBytes)
{
    /// <summary>Actual execution path; attention, normalization, RoPE and KV remain on the CPU.</summary>
    public string Backend=>"cuda-matrix-offload";
    /// <summary>Activation and accumulation precision of Q1_0 matrices; FP32 matrices are unchanged.</summary>
    public string ActivationPrecision {get;init;}="fp32";
    /// <summary>Benchmark-only generated token hash; benchmark Text is empty.</summary>
    public string? GeneratedTokenSha256 {get;init;}
    /// <summary>Optional CPU-stage diagnostics; Matrix includes device transfers and synchronization.</summary>
    public InferenceProfile? Profile {get;init;}
}
