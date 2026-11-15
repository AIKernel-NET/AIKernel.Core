namespace AIKernel.Kernel.Inference;

/// <summary>Native integer Q1_0 × Q8 arithmetic. Activation quantization is opt-in.</summary>
public enum CudaQ8Kernel
{
    /// <summary>Calibrate supported integer kernels per matrix shape; use Tensor Cores only with a consistent paired advantage.</summary>
    Auto,
    /// <summary>Force packed INT8 DP4A on compute capability 6.1 or newer.</summary>
    Dp4a,
    /// <summary>Force integer Tensor Core MMA. This implementation requires compute capability 8.0 or newer.</summary>
    TensorCore
}

/// <summary>Actual native integer work, including calibration calls. No prompts or activations are retained.</summary>
public sealed record CudaQ8Statistics(long Dp4aCalls,long TensorCoreCalls,IReadOnlyDictionary<string,string> ShapeKernels)
{
    /// <summary>Repeated paired timing evidence for automatic choices, captured during warmup.</summary>
    public IReadOnlyDictionary<string,CudaQ8Calibration> Calibration {get;init;}=new Dictionary<string,CudaQ8Calibration>();
}
