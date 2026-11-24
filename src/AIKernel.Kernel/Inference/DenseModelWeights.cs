namespace AIKernel.Kernel.Inference;

/// <summary>A row view of encoded weights. Backing identifies the immutable allocation, including for tied weights.</summary>
internal readonly record struct ModelTensor(long Backing, int Rows, int Columns, int RowBytes, string Encoding, int BlockElements, int BlockBytes, int FirstRow = 0)
{
    internal long ByteLength => checked((long)Rows * RowBytes);
    internal bool UsesBinaryWeights => Encoding == "Q1_0";
    internal ModelTensor Row(int index) => this with { FirstRow = checked(FirstRow + index), Rows = 1 };
}

internal sealed record DenseLayer(ModelTensor Q, ModelTensor K, ModelTensor V, ModelTensor O,
    ModelTensor Gate, ModelTensor Up, ModelTensor Down, float[] Norm, float[] FfnNorm, float[] QNorm, float[] KNorm);

internal interface IDenseModelWeights : IDisposable
{
    long Length { get; }
    int VocabularySize { get; }
    Qwen3ModelDefinition Shape { get; }
    ModelTensor Embedding { get; }
    ModelTensor Output { get; }
    float[] FinalNorm { get; }
    DenseLayer[] Layers { get; }
    QwenTokenizer Tokenizer { get; }
    (float[] Frequencies, float Amplitude) Rotary { get; }
    long? BackingBytesRead { get; }
    long? CacheCapacityBytes { get; }
    IMatrixOffload? Device { get; }
    void ReadRow(ModelTensor tensor, int row, float[] destination);
    void Multiply(ModelTensor tensor, float[] input, float[] output, ParallelOptions options, string backend,
        InferenceProfiler? profile = null, PreparedQ8Activation? prepared = null);
    void MultiplyDevice(ModelTensor tensor, float[] input, float[] output, CancellationToken token,
        InferenceProfiler? profile, PreparedQ8Activation? prepared = null);
}


internal sealed record DenseCheckpoint(string Path, DenseModelRegistration? Registration = null)
{
    internal IDenseModelWeights Open(int context, int? device, long gpuBudget,
        bool vulkan, bool q8, CudaQ8Kernel? kernel, GgufMemoryOptions? memory)
        => SnapshotRegistration.Open(Path, context, device, gpuBudget, vulkan, q8, kernel, memory, Registration);
}
