namespace AIKernel.Kernel.Inference;

internal sealed class SnapshotWeights : IDenseModelWeights
{
    private readonly SnapshotMatrixStore store;
    public Qwen3ModelDefinition Shape { get; }
    public ModelTensor Embedding { get; }
    public ModelTensor Output => Embedding;
    public int VocabularySize => Embedding.Rows;
    public float[] FinalNorm { get; }
    public DenseLayer[] Layers { get; }
    public QwenTokenizer Tokenizer { get; }
    public (float[] Frequencies, float Amplitude) Rotary { get; }
    public long Length => store.Length;
    public long? BackingBytesRead => store.Reads;
    public long? CacheCapacityBytes => store.Capacity;
    public IMatrixOffload? Device => null;

    internal static SnapshotWeights Open(string path, DenseRegistrationDocument profile, int context, GgufMemoryOptions? budget)
    {
        using var files = SnapshotFiles.Open(path, profile);
        var tensors = profile.ReaderId == "onnx" ? SnapshotTensors.ReadOnnx(files) : SnapshotTensors.ReadSafe(files);
        var storage = new SnapshotMatrixStore();
        try
        {
            var roles = new SnapshotRoles(tensors, storage);
            var result = new SnapshotWeights(files, roles, SnapshotShape.Read(files, profile.Shape, context), storage);
            storage.Seal(budget);
            return result;
        }
        catch { storage.Dispose(); throw; }
    }

    private SnapshotWeights(SnapshotFiles files, SnapshotRoles binder, SnapshotShape snapshot, SnapshotMatrixStore storage)
    {
        store = storage;
        Shape = snapshot.Shape;
        Tokenizer = SnapshotTokenizer.Create(files);
        Rotary = snapshot.Rotary();
        Embedding = binder.Embedding(snapshot.Vocabulary, Shape.Width);
        FinalNorm = binder.Vector("model.norm.weight",Shape.Width);
        Layers = binder.Layers(Shape);
        binder.Complete();
    }
    public void ReadRow(ModelTensor tensor, int row, float[] destination) => store.ReadRow(tensor,row,destination);
    public void Multiply(ModelTensor tensor,float[] input,float[] output,ParallelOptions options,string backend,InferenceProfiler? profile=null,PreparedQ8Activation? prepared=null)
        => store.Multiply(tensor,input,output,options);
    public void MultiplyDevice(ModelTensor tensor,float[] input,float[] output,CancellationToken token,InferenceProfiler? profile,PreparedQ8Activation? prepared)
        => throw new NotSupportedException("This snapshot registration uses host kernels.");
    public void Dispose() => store.Dispose();
}

internal sealed class SnapshotRoles(Dictionary<string, TensorSlice> tensors, SnapshotMatrixStore store)
{
    internal DenseLayer[] Layers(Qwen3ModelDefinition shape) => Enumerable.Range(0,shape.LayerCount).Select(i=>Layer(i,shape)).ToArray();
    private TensorSlice Take(string name,int rows,int columns)
    {
        if (!tensors.Remove(name,out var slice) || slice.Rows != rows || slice.Columns != columns)
            throw new InvalidDataException("Missing tensor or invalid dimensions: " + name);
        return slice;
    }
    internal float[] Vector(string name,int width) => Take(name,1,width).Materialize();
    internal ModelTensor Embedding(int rows,int cols)
    {
        var embedding = Take("model.embed_tokens.weight",rows,cols);
        if (tensors.ContainsKey("lm_head.weight")) RequireTied(embedding,Take("lm_head.weight",rows,cols));
        return store.Add(embedding);
    }
    private static void RequireTied(TensorSlice a, TensorSlice b)
    {
        if (a.Bfloat != b.Bfloat) throw new InvalidDataException("Tied tensor type mismatch.");
        var left = new byte[65536]; var right = new byte[left.Length];
        for (long pos=0;pos<a.Bytes;pos+=left.Length)
        {
            int n=(int)Math.Min(left.Length,a.Bytes-pos);
            a.ReadExactly(left.AsSpan(0,n),a.Offset+pos); b.ReadExactly(right.AsSpan(0,n),b.Offset+pos);
            if (!left.AsSpan(0,n).SequenceEqual(right.AsSpan(0,n))) throw new InvalidDataException("Tied weights differ.");
        }
    }
    internal DenseLayer Layer(int layer,Qwen3ModelDefinition s)
    {
        string prefix = $"model.layers.{layer}.";
        ModelTensor Matrix(string role,int r,int c) => store.Add(Take(prefix+role+".weight",r,c));
        float[] Norm(string role,int width) => Vector(prefix+role+".weight",width);
        int q=s.Heads*s.HeadSize, kv=s.KvHeads*s.HeadSize;
        return new(Matrix("self_attn.q_proj",q,s.Width), Matrix("self_attn.k_proj",kv,s.Width),
            Matrix("self_attn.v_proj",kv,s.Width),Matrix("self_attn.o_proj",s.Width,q),
            Matrix("mlp.gate_proj",s.Hidden,s.Width),Matrix("mlp.up_proj",s.Hidden,s.Width),Matrix("mlp.down_proj",s.Width,s.Hidden),
            Norm("input_layernorm",s.Width),Norm("post_attention_layernorm",s.Width),Norm("self_attn.q_norm",s.HeadSize),Norm("self_attn.k_norm",s.HeadSize));
    }
    internal void Complete() { if(tensors.Count != 0) throw new InvalidDataException("Unexpected tensors in registered graph."); }
}
