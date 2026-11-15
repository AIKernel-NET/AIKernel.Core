namespace AIKernel.Kernel.Inference;

/// <summary>Execution contract for Qwen3 dense; intentionally separate from GGUF storage admission.</summary>
internal sealed record Qwen3ModelDefinition(int Width,int Hidden,int Heads,int KvHeads,int HeadSize,int LayerCount,float Epsilon)
{
    internal static Qwen3ModelDefinition Read(GgufWeights file,int context,bool gpu,bool q8)
    {
        if(file.Metadata.GetValueOrDefault("general.architecture") is not "qwen3")
            throw new NotSupportedException("Qwen3CpuModel executes only Qwen3 dense. Inspecting a Gemma/GPT-OSS GGUF does not enable its forward graph.");
        // Preserve the existing demo policy. A valid large file is not permission to allocate its KV/workspace.
        if(file.Parameters>4_100_000_000L)throw new NotSupportedException("CPU demo model exceeds the four-billion-parameter class.");
        foreach(var tensor in file.Tensors.Values)
        {
            if(tensor.Rank>2)throw new NotSupportedException("Qwen3 dense cannot execute expert or multimodal tensors.");
            if(!tensor.Encoding.CpuReference)throw new NotSupportedException($"No CPU kernel for {tensor.Encoding.Name}.");
            if((gpu||q8)&&tensor.Type is not (0 or 41))
                throw new NotSupportedException("GPU and Q8 activation kernels currently support only F32/Q1_0 weights.");
        }
        int width=file.Int("qwen3.embedding_length"),hidden=file.Int("qwen3.feed_forward_length");
        int heads=file.Int("qwen3.attention.head_count"),kvHeads=file.Int("qwen3.attention.head_count_kv");
        int headSize=file.Int("qwen3.attention.key_length",width/Math.Max(1,heads)),count=file.Int("qwen3.block_count");
        float epsilon=file.Float("qwen3.attention.layer_norm_rms_epsilon",1e-6f);
        if(width is <128 or >8192||hidden is <128 or >32768||heads is <1 or >64||kvHeads<1||heads%kvHeads!=0||
           headSize is <32 or >256||headSize%2!=0||count is <1 or >64||!float.IsFinite(epsilon)||epsilon<=0||
           context>file.Int("qwen3.context_length")||file.Int("qwen3.attention.value_length",headSize)!=headSize)
            throw new InvalidDataException("Unsupported Qwen3 dimensions.");
        return new(width,hidden,heads,kvHeads,headSize,count,epsilon);
    }
}
