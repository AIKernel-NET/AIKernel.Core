namespace AIKernel.Kernel.Inference;

/// <summary>FP32 activation execution paths for the Gemma 4 E2B text graph.</summary>
public enum Gemma4Backend
{
    /// <summary>AVX-512 or AVX2 FP32 when available; otherwise portable reference.</summary>
    Auto,
    /// <summary>Portable reference implementation, useful for numerical comparisons.</summary>
    Reference,
    /// <summary>CUDA Driver API linear layers with CPU attention and private shared KV.</summary>
    Cuda,
    /// <summary>Full CUDA forward with resident encoded weights, shared KV and greedy selection.</summary>
    CudaResident
}

/// <summary>Gemma CPU kernels. Residual VNNI is explicit and never silently replaces FP32 auto.</summary>
public enum Gemma4InstructionSet
{
    /// <summary>AVX-512 FP32 if available, otherwise AVX2 or portable reference.</summary>
    Auto,
    /// <summary>Force the previous AVX2 FP32 kernel.</summary>
    Avx2,
    /// <summary>Force AVX-512 FP32.</summary>
    Avx512,
    /// <summary>AVX-VNNI Q4 dot with three residual Q8 activation planes; other tensors use FP32.</summary>
    AvxVnniResidual,
    /// <summary>AVX-512 VNNI Q4 dot with three residual Q8 activation planes; other tensors use FP32.</summary>
    Avx512VnniResidual
}

/// <summary>Explicit device selection and bounded encoded weight residency. No silent GPU fallback.</summary>
public sealed record Gemma4ExecutionOptions
{
    /// <summary>Auto selects CPU SIMD; GPU use requires explicit selection.</summary>
    public Gemma4Backend Backend{get;init;}=Gemma4Backend.Auto;
    /// <summary>Explicit CPU ISA override; unsupported forced choices fail before inference.</summary>
    public Gemma4InstructionSet InstructionSet{get;init;}=Gemma4InstructionSet.Auto;
    /// <summary>Zero-based CUDA device ordinal.</summary>
    public int CudaDeviceOrdinal{get;init;}
    /// <summary>VRAM budget for encoded GEMV weights, excluding scratch and driver allocations.</summary>
    public long MaximumGpuWeightBytes{get;init;}=2L*1024*1024*1024;
    /// <summary>Budget for resident CUDA allocations including weights, KV and RoPE tables; excludes driver overhead and diagnostic staging.</summary>
    public long MaximumResidentGpuBytes{get;init;}=4L*1024*1024*1024;
    /// <summary>Opt-in aggregate GEMV timing; disabled by default.</summary>
    public bool ProfileMatrices{get;init;}
    /// <summary>Optional CPU RAM budget and disk-backed weight paging. CUDA uses its separate VRAM budget.</summary>
    public Gemma4MemoryOptions? Memory{get;init;}
}
