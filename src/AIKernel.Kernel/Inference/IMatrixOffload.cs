namespace AIKernel.Kernel.Inference;

internal interface IMatrixOffload : IDisposable
{
    long WeightBytes { get; }
    string? PciBusId { get; }
    void Multiply(GgufWeights.Tensor tensor, float[] input, float[] output, CancellationToken token,InferenceProfiler? profile=null,PreparedQ8Activation? prepared=null);
    void ZeroScratch();
}
