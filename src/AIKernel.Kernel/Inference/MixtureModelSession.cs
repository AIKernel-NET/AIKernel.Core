using static AIKernel.Kernel.Inference.MixtureModelSession.GptOss20BProfile;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace AIKernel.Kernel.Inference;

/// <summary>Greedy, final-channel text inference for the GPT-OSS-20B MXFP4 GGUF.</summary>
public partial class MixtureModelSession : IModelSession
{
    private readonly GgufWeights file;
    private readonly GgufPagedWeights? pages;
    /// <summary>Page residency and page-ins for a model opened with an explicit RAM budget.</summary>
    public AIKernel.Kernel.Memory.VirtualMemoryStatistics? MemoryStatistics => pages?.Statistics;
    /// <summary>Allocated and reserved host page buffers for paged CPU weights.</summary>
    public AIKernel.Kernel.Memory.VirtualMemoryRamUsage? WeightRamUsage => pages?.RamUsage;
    /// <inheritdoc />
    public string ExecutionBackend => pages is null ? "gpt-oss-mxfp4-cpu" : "gpt-oss-mxfp4-cpu-paged";
    internal long BackingBytesRead => pages?.BackingBytesRead ?? 0;
    internal long DistinctPagesRead => pages?.DistinctPagesRead ?? 0;
    internal long WeightCacheCapacityBytes => pages?.CacheCapacityBytes ?? 0;
    private readonly TextGraph graph = new(Layers, new RoutingPlan(Experts, ActiveExperts));
    private RoutingPlan Routing => graph.Routing!;
    /// <summary>Number of dispatches to each resource in registration order during this session.</summary>
    public IReadOnlyList<long> ResourceDispatchCounts => Routing.Snapshot();
    /// <summary>Observes the three weight projections for one routed resource. OS-managed mappings return null.</summary>
    public ResourceResidency? GetResourceResidency(int layerIndex, int resourceIndex)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)layerIndex, (uint)layers.Length);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)resourceIndex, (uint)Experts);
        gate.Wait();
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (pages is null) return null;
            var layer = layers[layerIndex];
            ResourceResidency Read(Linear projection) => pages.Observe(projection.Weight.MatrixSlice(resourceIndex).Offset);
            return Read(layer.Gate).Include(Read(layer.Up)).Include(Read(layer.Down));
        }
        finally { gate.Release(); }
    }
    private readonly HarmonyChatTemplate chat;
    private readonly GgufWeights.Tensor embedding, output;
    private readonly float[] outputNorm, frequencies;
    private readonly Layer[] layers;
    private readonly int threads, context;
    private readonly SemaphoreSlim gate = new(1);
    private bool disposed;
    private sealed record Linear(GgufWeights.Tensor Weight, float[] Bias);
    private sealed record Layer(float[] Norm, Linear Q, Linear K, Linear V, Linear O, float[] Sinks,
        float[] MlpNorm, Linear Router, Linear Gate, Linear Up, Linear Down);

    /// <summary>Maps one checkpoint and validates its architecture, tensor shapes and Harmony controls before use.</summary>
    internal MixtureModelSession(string modelPath, int cpuThreads = 4, int contextTokens = 512)
        : this(modelPath,cpuThreads,contextTokens,null) { }

    /// <summary>Uses row-aligned virtual-memory pages from the GGUF when a memory budget is supplied.</summary>
    internal MixtureModelSession(string modelPath, int cpuThreads, int contextTokens, GgufMemoryOptions? memory)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(cpuThreads,1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(cpuThreads,64);
        if (contextTokens is < 8 or > 2048) throw new ArgumentOutOfRangeException(nameof(contextTokens));
        threads = cpuThreads;
        context = contextTokens;
        file = new(modelPath,mapTensorData: memory is null);
        try
        {
            Validate(file);
            frequencies = Rotary();
            embedding = Weight("token_embd.weight",Vocab,Width,8);
            output = Weight("output.weight",Vocab,Width,8);
            outputNorm = Vector("output_norm.weight",Width);
            layers = Enumerable.Range(0,Layers).Select(i => LoadLayer(i, Width, Experts, AttentionWidth, KvWidth, Heads)).ToArray();
            chat = new(file);
            if (memory is not null)
                pages = new(modelPath,MatrixTensors(),memory,pageSize:1<<20,backingHandle:file.BackingHandle);
        }
        catch { file.Dispose(); throw; }
    }

    private GgufWeights.Tensor Weight(string name, int rows, int columns, int type)
    {
        var tensor = file.Require(name,rows,columns);
        if (tensor.Type != type) throw new InvalidDataException("Unexpected storage format: "+name);
        return tensor;
    }
    private float[] Vector(string name, int columns) => file.ReadFloat32(Weight(name,1,columns,0));
    private IEnumerable<GgufWeights.Tensor> MatrixTensors()
    {
        yield return embedding;
        yield return output;
        foreach (var layer in layers)
        {
            foreach (var projection in new[]{layer.Q,layer.K,layer.V,layer.O,layer.Router})
                yield return projection.Weight;
            foreach (var bank in new[]{layer.Gate,layer.Up,layer.Down})
                for (int expert=0;expert<bank.Weight.Depth;expert++) yield return bank.Weight.MatrixSlice(expert);
        }
    }
    private Layer LoadLayer(int index, int width, int experts, int attentionWidth, int kvWidth, int heads)
    {
        string prefix = $"blk.{index}.";
        Linear Projection(string key, int rows, int columns, int type = 8) =>
            new(Weight(prefix+key+".weight",rows,columns,type), Vector(prefix+key+".bias",rows));
        Linear Expert(string key)
        {
            if (!file.Tensors.TryGetValue(prefix+key+".weight",out var tensor) || tensor.Rank != 3 || tensor.Type != 39 ||
                tensor.Rows != width || tensor.Columns != width || tensor.Depth != experts)
                throw new InvalidDataException("Invalid expert bank: "+key);
            var bias = Weight(prefix+key+".bias",experts,width,0);
            return new(tensor,file.ReadFloat32(bias));
        }
        return new(Vector(prefix+"attn_norm.weight",width),Projection("attn_q",attentionWidth,width),
            Projection("attn_k",kvWidth,width),Projection("attn_v",kvWidth,width),Projection("attn_output",width,attentionWidth),
            Vector(prefix+"attn_sinks.weight",heads),Vector(prefix+"post_attention_norm.weight",width),
            Projection("ffn_gate_inp",experts,width,0), Expert("ffn_gate_exps"),Expert("ffn_up_exps"),Expert("ffn_down_exps"));
    }

    internal List<int> EncodeChat(string system, string user) => chat.Encode(system, user);

    /// <summary>Generates a single final-channel answer. Requests on this model are serialized.</summary>
    public Task<CpuGeneration> GenerateAsync(string system, string user, int maxNewTokens = 128, CancellationToken cancellationToken = default)
        => GenerateAsync(system,user,maxNewTokens,cancellationToken,null);
    internal async Task<CpuGeneration> GenerateAsync(string system,string user,int maxNewTokens,CancellationToken cancellationToken,Action<int>? observeToken)
    {
        ArgumentNullException.ThrowIfNull(system);
        ArgumentException.ThrowIfNullOrWhiteSpace(user);
        if (system.Length > 8192 || user.Length > 8192) throw new ArgumentException("GPT-OSS text exceeds the input limit.");
        if (maxNewTokens < 1 || maxNewTokens >= context) throw new ArgumentOutOfRangeException(nameof(maxNewTokens));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(disposed,this);
            var prompt = EncodeChat(system,user);
            if (prompt.Count > context-maxNewTokens) throw new ArgumentException("Prompt and generation exceed the context.");
            pages?.CheckBudget();
            return await Task.Run(() => Run(prompt,maxNewTokens,cancellationToken,observeToken),cancellationToken).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }
    private CpuGeneration Run(List<int> prompt, int maximum, CancellationToken cancellation, Action<int>? observeToken)
    {
        var watch=Stopwatch.StartNew();
        var options=new ParallelOptions { MaxDegreeOfParallelism=threads, CancellationToken=cancellation };
        var state=new State(prompt.Count+maximum);
        var ids=new List<int>();
        int position=0;
        foreach (int token in prompt) Forward(token,position++,state,options);
        long prefill = watch.ElapsedMilliseconds;
        string finish="length";
        while (ids.Count < maximum)
        {
            Normalize(state.X,outputNorm,state.Norm);
            GptOssMatrix.Multiply(file,output,state.Norm,state.Logits,null,0,options,pages);
            int best=0;
            for (int i=0;i<state.Logits.Length;i++)
            {
                if (!float.IsFinite(state.Logits[i])) throw new InvalidDataException("Non-finite GPT-OSS output.");
                if (state.Logits[i]>state.Logits[best]) best=i;
            }
            observeToken?.Invoke(best);
            if (chat.IsStop(best)) { finish="stop"; break; }
            HarmonyChatTemplate.RequireText(best);
            ids.Add(best);
            if (ids.Count < maximum) Forward(best,position++,state,options);
        }
        return new(chat.Decode(ids),prompt.Count,ids.Count,finish,watch.ElapsedMilliseconds,ExecutionBackend)
        { GeneratedTokenSha256=GeneratedTokenDigest.Compute(ids),
          PrefillMilliseconds=prefill,DecodeMilliseconds=watch.ElapsedMilliseconds-prefill,PrefillForwardTokens=prompt.Count,
          DecodeForwardTokens=position-prompt.Count,PrefillThreads=threads,DecodeThreads=threads };
    }

    private sealed class State(int length)
    {
        public readonly float[] X=new float[Width],Norm=new float[Width],Q=new float[AttentionWidth],K=new float[KvWidth],
            V=new float[KvWidth],Attention=new float[AttentionWidth],Projection=new float[Width],Router=new float[Experts],
            Gate=new float[Width],Up=new float[Width],Mlp=new float[Width],Down=new float[Width],Logits=new float[Vocab],
            Scores=new float[length],Cos=new float[Head/2],Sin=new float[Head/2];
        public readonly float[][] Keys=Enumerable.Range(0,Layers).Select(_=>new float[length*KvWidth]).ToArray();
        public readonly float[][] Values=Enumerable.Range(0,Layers).Select(_=>new float[length*KvWidth]).ToArray();
    }

    private unsafe void Forward(int token, int pos, State s, ParallelOptions options)
    {
        options.CancellationToken.ThrowIfCancellationRequested();
        ReadEmbedding(token, s.X);
        float amplitude=1+0.1f*MathF.Log(32);
        for (int i=0;i<s.Cos.Length;i++)
        {
            var (sin, cos)=MathF.SinCos(pos*frequencies[i]);
            s.Cos[i]=cos*amplitude; s.Sin[i]=sin*amplitude;
        }
        void Project(Linear linear,float[] x,float[] dest,int expert=-1) =>
            GptOssMatrix.Multiply(file,expert<0?linear.Weight:linear.Weight.MatrixSlice(expert),x,dest,linear.Bias,expert<0?0:expert*Width,options,pages);
        Span<int> order=stackalloc int[Experts];
        foreach (int layer in graph.Layers(options.CancellationToken))
        {
            var w=layers[layer];
            Normalize(s.X,w.Norm,s.Norm);
            Project(w.Q,s.Norm,s.Q); Project(w.K,s.Norm,s.K); Project(w.V,s.Norm,s.V);
            Rotate(s.Q,s.Cos,s.Sin); Rotate(s.K,s.Cos,s.Sin);
            s.K.CopyTo(s.Keys[layer],pos*KvWidth); s.V.CopyTo(s.Values[layer],pos*KvWidth);
            Attend(s,layer,pos,w.Sinks,Head,KvWidth,KvHeads);
            Project(w.O,s.Attention,s.Projection);
            for (int i=0;i<Width;i++) s.X[i]+=s.Projection[i];
            Normalize(s.X,w.MlpNorm,s.Norm); Project(w.Router,s.Norm,s.Router);
            float total = Routing.Select(s.Router, order);
            Array.Clear(s.Mlp);
            for (int i=0;i<Routing.Active;i++)
            {
                int e=order[i];
                Project(w.Gate,s.Norm,s.Gate,e); Project(w.Up,s.Norm,s.Up,e);
                for (int j=0;j<Width;j++)
                {
                    float g=Math.Min(s.Gate[j],7);
                    s.Gate[j]=g/(1+MathF.Exp(-1.702f*g))*(1+Math.Clamp(s.Up[j],-7,7));
                }
                Project(w.Down,s.Gate,s.Down,e);
                float routing=s.Router[e]/total;
                for (int j=0;j<Width;j++) s.Mlp[j]+=routing*s.Down[j];
            }
            for (int j=0;j<Width;j++) s.X[j]+=s.Mlp[j];
        }
    }
    private unsafe void ReadEmbedding(int token, float[] output)
    {
        if (pages is not null) pages.ReadRow(embedding,token,output);
        else embedding.Encoding.Decode(new ReadOnlySpan<byte>(file.Pointer(embedding)+(long)token*embedding.RowBytes,embedding.RowBytes),output);
    }

    private static void Rotate(float[] values,float[] cos,float[] sin)
    {
        for (int offset=0;offset<values.Length;offset+=2*cos.Length)
            for (int j=0;j<cos.Length;j++)
            {
                int a=offset+j,b=a+cos.Length;
                (values[a],values[b])=(values[a]*cos[j]-values[b]*sin[j],values[b]*cos[j]+values[a]*sin[j]);
            }
    }
    private static void Normalize(float[] input,float[] weight,float[] result)
    {
        float factor=1/MathF.Sqrt(GptOssMatrix.Dot(input,input)/input.Length+1e-5f);
        for (int i=0;i<input.Length;i++) result[i]=input[i]*factor*weight[i];
    }
    private static void Attend(State s, int layer, int pos, float[] sinks, int headWidth, int kvWidth, int kvHeads)
    {
        Array.Clear(s.Attention);
        int heads = sinks.Length;
        int first=layer%2==0?Math.Max(0,pos-127):0;
        for (int h=0;h<heads;h++)
        {
            int kv=(h/(heads/kvHeads))*headWidth;
            float peak=sinks[h];
            var q=s.Q.AsSpan(h*headWidth,headWidth);
            for (int t=first;t<=pos;t++)
            {
                s.Scores[t]=GptOssMatrix.Dot(q,s.Keys[layer].AsSpan(t*kvWidth+kv,headWidth))/MathF.Sqrt(headWidth);
                peak=Math.Max(peak,s.Scores[t]);
            }
            float total = SumWithSink(s.Scores, first, pos, peak, sinks[h]);
            for (int t=first;t<=pos;t++)
            {
                float scale=s.Scores[t]/total;
                for (int d=0;d<headWidth;d++) s.Attention[h*headWidth+d]+=scale*s.Values[layer][t*kvWidth+kv+d];
            }
        }
    }
    private static float SumWithSink(float[] scores, int first, int last, float peak, float sink)
    {
        float total = MathF.Exp(sink - peak);
        for (int t = first; t <= last; t++)
        {
            scores[t] = MathF.Exp(scores[t] - peak);
            total += scores[t];
        }
        return total;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        gate.Wait();
        try { if (!disposed) { disposed=true; try { pages?.Dispose(); } finally { file.Dispose(); } } }
        finally { gate.Release(); }
    }
}
