namespace AIKernel.Kernel.Inference;

/// <summary>Binds GGUF storage and header names to the roles consumed by the dense graph.</summary>
internal sealed class GgufDenseWeights : IDenseModelWeights
{
    private readonly GgufWeights _file;
    private readonly Dictionary<long, GgufWeights.Tensor> _storage = [];
    internal TensorPageStore? _pages;

    internal static IDenseModelWeights Open(string path, int context, int? device, long gpuBudget,
        bool vulkan, bool q8, CudaQ8Kernel? kernel, GgufMemoryOptions? memory, DenseModelRegistration? registration)
    {
        var file = OpenFile(path, memory is null, registration);
        try
        {
            var shape = GgufDenseShape.Read(file, context, device.HasValue, q8, memory is not null);
            shape = registration?.BindShape(shape) ?? shape;
            registration?.CheckTokenizer(file);
            var weights = new GgufDenseWeights(file, shape);
            weights.ConfigureStorage(path, device, gpuBudget, vulkan, kernel, memory);
            return weights;
        }
        catch
        {
            file.Dispose();
            throw;
        }
    }

    private static GgufWeights OpenFile(string path, bool map, DenseModelRegistration? registration)
    {
        var stream = File.OpenRead(path);
        try
        {
            registration?.CheckWeights(stream);
            return new GgufWeights(stream, map);
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    private GgufDenseWeights(GgufWeights file, Qwen3ModelDefinition shape)
    {
        _file = file;
        Shape = shape;
        Tokenizer = new(file);
        Rotary = ReadRotary(file, shape.HeadSize);
        Embedding = Matrix("token_embd.weight", VocabularySize, shape.Width);
        Output = file.ContainsOutput() ? Matrix("output.weight", VocabularySize, shape.Width) : Embedding;
        FinalNorm = Vector("output_norm.weight", shape.Width);
        Layers = Enumerable.Range(0, shape.LayerCount).Select(ReadLayer).ToArray();
    }

    private void ConfigureStorage(string path, int? device, long gpuBudget, bool vulkan, CudaQ8Kernel? kernel, GgufMemoryOptions? memory)
    {
        if (memory is not null)
            _pages = OpenPages(path, memory);
        if (device.HasValue)
            Device = OpenDevice(device.Value, gpuBudget, vulkan, kernel);
    }

    private IEnumerable<ModelTensor> Backings => _storage.Values.Select(Describe);

    internal static TensorPageBudget PageBudget(GgufMemoryOptions options)
        => new(options.MaximumResidentBytes, options.WeightCacheBytes, options.InitialWeightCacheBytes,
            options.RuntimeReserveBytes, options.SwapDirectory);

    private TensorPageStore OpenPages(string path, GgufMemoryOptions memory)
        => memory.OpenDense(path, Backings, _file);

    private IMatrixOffload OpenDevice(int device, long budget, bool vulkan, CudaQ8Kernel? kernel)
        => vulkan ? new VulkanQuantizedMatrix(_file, device, budget) : new CudaQuantizedMatrix(_file, device, budget, kernel);

    public Qwen3ModelDefinition Shape { get; }
    public QwenTokenizer Tokenizer { get; }
    public ModelTensor Embedding { get; }
    public ModelTensor Output { get; }
    public DenseLayer[] Layers { get; }
    public float[] FinalNorm { get; }
    public (float[] Frequencies, float Amplitude) Rotary { get; }
    public IMatrixOffload? Device { get; private set; }
    public long Length => _file.Length;
    public int VocabularySize => _file.Tokens.Length;
    public long? CacheCapacityBytes => _pages?.CacheCapacityBytes;
    public long? BackingBytesRead => _pages?.BackingBytesRead;

    internal static ModelTensor Describe(GgufWeights.Tensor tensor)
    {
        if (tensor.Rank > 2) throw new NotSupportedException("Expected a matrix or vector view.");
        var encoding = tensor.Encoding;
        return new(tensor.Offset, tensor.Rows, tensor.Columns, tensor.RowBytes, encoding.Name, encoding.BlockElements, encoding.BlockBytes);
    }

    private ModelTensor Matrix(string name, int rows, int columns)
    {
        var tensor = _file.Require(name, rows, columns);
        _storage.TryAdd(tensor.Offset, tensor);
        return Describe(tensor);
    }

    private float[] Vector(string name, int width) => _file.Vector(_file.Require(name, 1, width));

    private DenseLayer ReadLayer(int layer)
    {
        string prefix = $"blk.{layer}.";
        var width = Shape.Width;
        var query = Shape.Heads * Shape.HeadSize;
        var kv = Shape.KvHeads * Shape.HeadSize;
        return new(Matrix(prefix + "attn_q.weight", query, width), Matrix(prefix + "attn_k.weight", kv, width),
            Matrix(prefix + "attn_v.weight", kv, width), Matrix(prefix + "attn_output.weight", width, query),
            Matrix(prefix + "ffn_gate.weight", Shape.Hidden, width), Matrix(prefix + "ffn_up.weight", Shape.Hidden, width),
            Matrix(prefix + "ffn_down.weight", width, Shape.Hidden), Vector(prefix + "attn_norm.weight", width),
            Vector(prefix + "ffn_norm.weight", width), Vector(prefix + "attn_q_norm.weight", Shape.HeadSize),
            Vector(prefix + "attn_k_norm.weight", Shape.HeadSize));
    }

    private static (float[], float) ReadRotary(GgufWeights file, int headSize)
    {
        string? scaling = file.Metadata.GetValueOrDefault("qwen3.rope.scaling.type") as string;
        if (scaling is not (null or "none" or "yarn")) throw new NotSupportedException("Unsupported rotary scaling.");
        float theta = file.Float("qwen3.rope.freq_base", 1000000);
        float factor = scaling == "yarn" ? file.Float("qwen3.rope.scaling.factor", 1) : 1;
        int original = file.Int("qwen3.rope.scaling.original_context_length", 8192);
        return DenseRotary.Create(headSize, theta, factor, original);
    }

    private GgufWeights.Tensor Resolve(ModelTensor view)
    {
        var tensor = _storage[view.Backing];
        if (view.Rows == tensor.Rows && view.FirstRow == 0) return tensor;
        return tensor with { Offset = checked(tensor.Offset + view.FirstRow * tensor.RowByteLength), Rows = view.Rows };
    }

    public void ReadRow(ModelTensor tensor, int row, float[] destination)
    {
        var source = Resolve(tensor);
        if (_pages is null) QuantizedCpuMatrix.Row(_file, source, row, destination);
        else _pages.VisitRow(tensor, row, bytes => source.Encoding.Decode(bytes, destination.AsSpan(0, tensor.Columns)));
    }

    public void Multiply(ModelTensor tensor, float[] input, float[] output, ParallelOptions options, string backend,
        InferenceProfiler? profile = null, PreparedQ8Activation? prepared = null)
    {
        var source = Resolve(tensor);
        if (_pages is null) QuantizedCpuMatrix.Multiply(_file, source, input, output, options, backend, profile, prepared);
        else _pages.VisitRows(tensor, options.CancellationToken, (address, first, count) =>
            QuantizedCpuMatrix.MultiplyPage(address, source, input, output, options, backend, first, count));
    }

    public void MultiplyDevice(ModelTensor tensor, float[] input, float[] output, CancellationToken token,
        InferenceProfiler? profile, PreparedQ8Activation? prepared = null) =>
        Device!.Multiply(Resolve(tensor), input, output, token, profile, prepared);

    public void Dispose()
    {
        try { Device?.Dispose(); }
        finally { try { _pages?.Dispose(); } finally { _file.Dispose(); } }
    }
}

internal static class DenseRotary
{
    internal static (float[], float) Create(int headSize, float theta, float factor, int original)
    {
        Validate(theta, factor, original);
        return Frequencies(headSize, theta, factor, original);
    }

    private static void Validate(float theta, float factor, int original)
    {
        if (!float.IsFinite(theta) || theta <= 1 || !float.IsFinite(factor) || factor < 1 || factor > 32 || original < 1)
            throw new InvalidDataException("Invalid rotary metadata.");
    }

    private static (float[], float) Frequencies(int headSize, float theta, float factor, int original)
    {
        var frequencies = new float[headSize / 2];
        float low = Math.Max(0, MathF.Floor(headSize * MathF.Log(original / (32 * 2 * MathF.PI)) / (2 * MathF.Log(theta))));
        float high = Math.Min(headSize - 1, MathF.Ceiling(headSize * MathF.Log(original / (2 * MathF.PI)) / (2 * MathF.Log(theta))));
        for (int i = 0; i < frequencies.Length; i++)
        {
            float freq = MathF.Pow(theta, -2f * i / headSize);
            float extrapolation = 1 - Math.Clamp((i - low) / Math.Max(0.001f, high - low), 0, 1);
            frequencies[i] = freq * (extrapolation + (1 - extrapolation) / factor);
        }
        return (frequencies, factor == 1 ? 1 : 1 + 0.1f * MathF.Log(factor));
    }
}
