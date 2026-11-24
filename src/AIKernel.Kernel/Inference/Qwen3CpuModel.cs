namespace AIKernel.Kernel.Inference;

/// <summary>Compatibility entry point for direct model construction through the 0.3 series.</summary>
[Obsolete("Use DenseModelRegistration.Parse(...).OpenSession(...). This type will be removed in 0.4.", false)]
public sealed class Qwen3CpuModel : DenseModelSession
{
    public Qwen3CpuModel(string modelPath,int cpuThreads=4,int contextTokens=1024)
        : base(modelPath, cpuThreads, contextTokens) { }

    public Qwen3CpuModel(string modelPath,int cpuThreads,int contextTokens,string? instructionSet)
        : base(modelPath, cpuThreads, contextTokens, instructionSet) { }

    public Qwen3CpuModel(string modelPath, int cpuThreads, int contextTokens, string? instructionSet, GgufMemoryOptions memory)
        : base(modelPath, cpuThreads, contextTokens, instructionSet, memory) { }

    internal Qwen3CpuModel(string modelPath,int cpuThreads,int contextTokens,int? deviceOrdinal,long maximumWeightBytes,
        bool vulkan=false,bool gpuQ8=false,string? instructionSet=null,CudaQ8Kernel? gpuInt8=null,GgufMemoryOptions? memory=null)
        : base(modelPath, cpuThreads, contextTokens, deviceOrdinal, maximumWeightBytes, vulkan, gpuQ8, instructionSet, gpuInt8, memory) { }

    internal Qwen3CpuModel(DenseModelRegistration registration, string path, int threads, int context, GgufMemoryOptions? memory)
        : base(new DenseCheckpoint(path, registration), threads, context, null, 0, instructionSet: "auto", memory: memory) { }

    internal Qwen3CpuModel(DenseCheckpoint checkpoint, int cpuThreads, int contextTokens, int? deviceOrdinal, long maximumWeightBytes,
        bool vulkan=false, bool gpuQ8=false, string? instructionSet=null, CudaQ8Kernel? gpuInt8=null, GgufMemoryOptions? memory=null)
        : base(checkpoint, cpuThreads, contextTokens, deviceOrdinal, maximumWeightBytes, vulkan, gpuQ8, instructionSet, gpuInt8, memory) { }
}
/// <summary>Measured result of a completed CPU generation.</summary>
public sealed record CpuGeneration(string Text,int InputTokens,int OutputTokens,string FinishReason,long ElapsedMilliseconds,string CpuBackend)
{
    /// <summary>Actual activation precision, retained even when stage profiling is disabled for replay diagnostics.</summary>
    public string ActivationPrecision {get;init;}="fp32";
    /// <summary>Optional stage diagnostics enabled at model construction by AIKERNEL_INFERENCE_PROFILE=1.</summary>
    public InferenceProfile? Profile {get;init;}
    /// <summary>Benchmark-only SHA-256 of little-endian Int32 generated token IDs, including EOS. Text is empty in benchmark mode.</summary>
    public string? GeneratedTokenSha256 {get;init;}
    /// <summary>Prompt forward passes excluding already cached system-prefix work.</summary>
    public double PrefillMilliseconds {get;init;}
    /// <summary>Output projections and generated-token forward passes.</summary>
    public double DecodeMilliseconds {get;init;}
    /// <summary>Number of prompt tokens forwarded during this call.</summary>
    public int PrefillForwardTokens {get;init;}
    /// <summary>Generated tokens forwarded for subsequent predictions, excluding the final unforwarded token.</summary>
    public int DecodeForwardTokens {get;init;}
    /// <summary>Worker ceiling used during prompt processing.</summary>
    public int PrefillThreads {get;init;}
    /// <summary>Worker ceiling used during token generation.</summary>
    public int DecodeThreads {get;init;}
}
