namespace AIKernel.Kernel.Inference;

/// <summary>FP32 activation execution paths for the Gemma 4 E2B text graph.</summary>
public enum Gemma4Backend
{
    /// <summary>AVX2 when available; otherwise the portable reference path.</summary>
    Auto,
    /// <summary>Portable reference implementation, useful for numerical comparisons.</summary>
    Reference,
    /// <summary>CUDA Driver API linear layers with CPU attention and private shared KV.</summary>
    Cuda
}

/// <summary>Explicit device selection and bounded encoded weight residency. No silent GPU fallback.</summary>
public sealed record Gemma4ExecutionOptions
{
    /// <summary>Auto selects CPU SIMD; GPU use requires explicit selection.</summary>
    public Gemma4Backend Backend{get;init;}=Gemma4Backend.Auto;
    /// <summary>Zero-based CUDA device ordinal.</summary>
    public int CudaDeviceOrdinal{get;init;}
    /// <summary>VRAM budget for encoded GEMV weights, excluding scratch and driver allocations.</summary>
    public long MaximumGpuWeightBytes{get;init;}=2L*1024*1024*1024;
    /// <summary>Opt-in aggregate GEMV timing; disabled by default.</summary>
    public bool ProfileMatrices{get;init;}
    /// <summary>Optional CPU RAM budget and disk-backed weight paging. CUDA uses its separate VRAM budget.</summary>
    public Gemma4MemoryOptions? Memory{get;init;}
}
