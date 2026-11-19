using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace AIKernel.Kernel.Inference;

/// <summary>Greedy, final-channel text inference for the GPT-OSS-20B MXFP4 GGUF.</summary>
public sealed class GptOss20BCpuModel : IDisposable
{
    private const int Width = 2880, Head = 64, Heads = 64, KvHeads = 8, Experts = 32, Layers = 24;
    private const int KvWidth = Head * KvHeads, AttentionWidth = Head * Heads;
    private const int Vocab = 201088;
    private readonly GgufWeights file;
    private readonly GgufByteBpe bpe;
    private readonly GgufWeights.Tensor embedding, output;
    private readonly float[] outputNorm, frequencies = new float[Head/2];
    private readonly Layer[] layers;
    private readonly int threads, context;
    private readonly int start, end, message, channel, stop;
    private readonly SemaphoreSlim gate = new(1);
    private bool disposed;
    private const string Upper = @"[\p{Lu}\p{Lt}\p{Lm}\p{Lo}\p{M}]";
    private const string Lower = @"[\p{Ll}\p{Lm}\p{Lo}\p{M}]";
    private const string Contraction = @"(?i:'s|'t|'re|'ve|'m|'ll|'d)?";
    private const string Lead = @"[^\r\n\p{L}\p{N}]?";
    private static readonly Regex Pieces = new(Lead+Upper+"*"+Lower+"+"+Contraction+"|"+Lead+Upper+"+"+Lower+"*"+Contraction+
        @"|\p{N}{1,3}| ?[^\s\p{L}\p{N}]+[\r\n/]*|\s*[\r\n]+|\s+(?!\S)|\s+", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private sealed record Linear(GgufWeights.Tensor Weight, float[] Bias);
    private sealed record Layer(float[] Norm, Linear Q, Linear K, Linear V, Linear O, float[] Sinks,
        float[] MlpNorm, Linear Router, Linear Gate, Linear Up, Linear Down);

    /// <summary>Maps one checkpoint and validates its architecture, tensor shapes and Harmony controls before use.</summary>
    public GptOss20BCpuModel(string modelPath, int cpuThreads = 4, int contextTokens = 512)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(cpuThreads,1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(cpuThreads,64);
        if (contextTokens is < 8 or > 2048) throw new ArgumentOutOfRangeException(nameof(contextTokens));
        threads = cpuThreads;
        context = contextTokens;
        file = new(modelPath);
        try
        {
            if (file.Metadata.GetValueOrDefault("general.architecture") is not "gpt-oss" ||
                file.Metadata.GetValueOrDefault("tokenizer.ggml.pre") is not "gpt-4o" ||
                file.Metadata.GetValueOrDefault("gpt-oss.rope.scaling.type") is not "yarn" || file.Tokens.Length != Vocab)
                throw new NotSupportedException("Expected GPT-OSS-20B with an o200k Harmony tokenizer.");
            void RequireInt(string name, int expected)
            {
                if (file.Int("gpt-oss."+name) != expected) throw new InvalidDataException("Unsupported GPT-OSS "+name);
            }
            RequireInt("block_count",Layers); RequireInt("embedding_length",Width);
            RequireInt("expert_feed_forward_length",Width); RequireInt("expert_count",Experts); RequireInt("expert_used_count",4);
            RequireInt("attention.head_count",Heads); RequireInt("attention.head_count_kv",KvHeads);
            RequireInt("attention.key_length",Head); RequireInt("attention.value_length",Head); RequireInt("attention.sliding_window",128);
            RequireInt("rope.scaling.original_context_length",4096);
            if (file.Float("gpt-oss.rope.freq_base",0) != 150000 || file.Float("gpt-oss.rope.scaling.factor",0) != 32 ||
                file.Float("gpt-oss.attention.layer_norm_rms_epsilon",0) != 1e-5f ||
                file.Float("gpt-oss.rope.scaling.yarn_beta_fast",32) != 32 || file.Float("gpt-oss.rope.scaling.yarn_beta_slow",1) != 1)
                throw new InvalidDataException("Unsupported GPT-OSS rotary or normalization parameters.");
            double boundary(double rotations) => (Head/2) * Math.Log(4096/(rotations*2*Math.PI))/Math.Log(150000);
            double low = boundary(32), high = boundary(1);
            for (int i=0; i<frequencies.Length; i++)
            {
                double ramp = Math.Clamp((i-low)/(high-low),0,1);
                frequencies[i] = (float)((1-ramp+ramp/32)/Math.Pow(150000,2d*i/Head));
            }
            embedding = Weight("token_embd.weight",Vocab,Width,8);
            output = Weight("output.weight",Vocab,Width,8);
            outputNorm = Vector("output_norm.weight",Width);
            layers = Enumerable.Range(0,Layers).Select(LoadLayer).ToArray();
            bpe = new(file);
            start=bpe.SpecialToken("<|start|>"); end=bpe.SpecialToken("<|end|>"); message=bpe.SpecialToken("<|message|>");
            channel=bpe.SpecialToken("<|channel|>"); stop=bpe.SpecialToken("<|return|>");
            if (stop != file.Int("tokenizer.ggml.eos_token_id")) throw new InvalidDataException("Invalid Harmony terminator.");
        }
        catch { file.Dispose(); throw; }
    }

    private GgufWeights.Tensor Weight(string name, int rows, int columns, int type)
    {
        var tensor = file.Require(name,rows,columns);
        if (tensor.Type != type) throw new InvalidDataException("Unexpected storage format: "+name);
        return tensor;
    }
    private float[] Vector(string name, int columns) => file.Vector(Weight(name,1,columns,0));
    private unsafe Layer LoadLayer(int index)
    {
        string prefix = $"blk.{index}.";
        Linear Projection(string key, int rows, int columns, int type = 8) =>
            new(Weight(prefix+key+".weight",rows,columns,type), Vector(prefix+key+".bias",rows));
        Linear Expert(string key)
        {
            if (!file.Tensors.TryGetValue(prefix+key+".weight",out var tensor) || tensor.Rank != 3 || tensor.Type != 39 ||
                tensor.Rows != Width || tensor.Columns != Width || tensor.Depth != Experts)
                throw new InvalidDataException("Invalid expert bank: "+key);
            var bias = Weight(prefix+key+".bias",Experts,Width,0);
            return new(tensor,new ReadOnlySpan<float>(file.Pointer(bias),Experts*Width).ToArray());
        }
        return new(Vector(prefix+"attn_norm.weight",Width),Projection("attn_q",AttentionWidth,Width),
            Projection("attn_k",KvWidth,Width),Projection("attn_v",KvWidth,Width),Projection("attn_output",Width,AttentionWidth),
            Vector(prefix+"attn_sinks.weight",Heads),Vector(prefix+"post_attention_norm.weight",Width),
            Projection("ffn_gate_inp",Experts,Width,0), Expert("ffn_gate_exps"),Expert("ffn_up_exps"),Expert("ffn_down_exps"));
    }

    internal List<int> EncodeChat(string system, string user)
    {
        var ids = new List<int>();
        void AddMessage(string role, string text)
        {
            ids.Add(start); ids.AddRange(bpe.Encode(role,Pieces)); ids.Add(message);
            ids.AddRange(bpe.Encode(text,Pieces,unicodeScalars:true)); ids.Add(end);
        }
        if (system.Length > 0) AddMessage("system",system);
        AddMessage("user",user);
        ids.Add(start); ids.AddRange(bpe.Encode("assistant",Pieces)); ids.Add(channel);
        ids.AddRange(bpe.Encode("final",Pieces)); ids.Add(message);
        return ids;
    }

    /// <summary>Generates a single final-channel answer. Requests on this model are serialized.</summary>
    public async Task<CpuGeneration> GenerateAsync(string system, string user, int maxNewTokens = 128, CancellationToken cancellationToken = default)
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
            return await Task.Run(() => Run(prompt,maxNewTokens,cancellationToken),cancellationToken).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }
    private CpuGeneration Run(List<int> prompt, int maximum, CancellationToken cancellation)
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
            GptOssMatrix.Multiply(file,output,state.Norm,state.Logits,null,0,options);
            int best=0;
            for (int i=0;i<state.Logits.Length;i++)
            {
                if (!float.IsFinite(state.Logits[i])) throw new InvalidDataException("Non-finite GPT-OSS output.");
                if (state.Logits[i]>state.Logits[best]) best=i;
            }
            if (best == stop) { finish="stop"; break; }
            if (best >= 199998) throw new InvalidDataException("Unexpected Harmony control in final answer.");
            ids.Add(best);
            if (ids.Count < maximum) Forward(best,position++,state,options);
        }
        return new(bpe.Decode(ids),prompt.Count,ids.Count,finish,watch.ElapsedMilliseconds,"gpt-oss-mxfp4-cpu")
        { GeneratedTokenSha256=Convert.ToHexStringLower(SHA256.HashData(MemoryMarshal.AsBytes(CollectionsMarshal.AsSpan(ids)))),
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
        embedding.Encoding.Decode(new ReadOnlySpan<byte>(file.Pointer(embedding)+(long)token*embedding.RowBytes,embedding.RowBytes),s.X);
        float amplitude=1+0.1f*MathF.Log(32);
        for (int i=0;i<s.Cos.Length;i++)
        {
            var (sin, cos)=MathF.SinCos(pos*frequencies[i]);
            s.Cos[i]=cos*amplitude; s.Sin[i]=sin*amplitude;
        }
        void Project(Linear linear,float[] x,float[] dest,int expert=-1) =>
            GptOssMatrix.Multiply(file,expert<0?linear.Weight:linear.Weight.MatrixSlice(expert),x,dest,linear.Bias,expert<0?0:expert*Width,options);
        Span<int> order=stackalloc int[Experts];
        for (int layer=0;layer<Layers;layer++)
        {
            var w=layers[layer];
            Normalize(s.X,w.Norm,s.Norm);
            Project(w.Q,s.Norm,s.Q); Project(w.K,s.Norm,s.K); Project(w.V,s.Norm,s.V);
            Rotate(s.Q,s.Cos,s.Sin); Rotate(s.K,s.Cos,s.Sin);
            s.K.CopyTo(s.Keys[layer],pos*KvWidth); s.V.CopyTo(s.Values[layer],pos*KvWidth);
            Attend(s,layer,pos,w.Sinks);
            Project(w.O,s.Attention,s.Projection);
            for (int i=0;i<Width;i++) s.X[i]+=s.Projection[i];
            Normalize(s.X,w.MlpNorm,s.Norm); Project(w.Router,s.Norm,s.Router);
            for (int i=0;i<Experts;i++) order[i]=i;
            order.Sort((a,b) => { int cmp=s.Router[b].CompareTo(s.Router[a]); return cmp!=0?cmp:a.CompareTo(b); });
            float total=0, peak=s.Router[order[0]];
            for (int i=0;i<4;i++) { int e=order[i]; s.Router[e]=MathF.Exp(s.Router[e]-peak); total+=s.Router[e]; }
            Array.Clear(s.Mlp);
            for (int i=0;i<4;i++)
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
    private static void Rotate(float[] values,float[] cos,float[] sin)
    {
        for (int offset=0;offset<values.Length;offset+=Head)
            for (int j=0;j<Head/2;j++)
            {
                int a=offset+j,b=a+Head/2;
                (values[a],values[b])=(values[a]*cos[j]-values[b]*sin[j],values[b]*cos[j]+values[a]*sin[j]);
            }
    }
    private static void Normalize(float[] input,float[] weight,float[] result)
    {
        float factor=1/MathF.Sqrt(GptOssMatrix.Dot(input,input)/input.Length+1e-5f);
        for (int i=0;i<input.Length;i++) result[i]=input[i]*factor*weight[i];
    }
    private static void Attend(State s, int layer, int pos, float[] sinks)
    {
        Array.Clear(s.Attention);
        int first=layer%2==0?Math.Max(0,pos-127):0;
        for (int h=0;h<Heads;h++)
        {
            int kv=(h/(Heads/KvHeads))*Head;
            float peak=sinks[h];
            var q=s.Q.AsSpan(h*Head,Head);
            for (int t=first;t<=pos;t++)
            {
                s.Scores[t]=GptOssMatrix.Dot(q,s.Keys[layer].AsSpan(t*KvWidth+kv,Head))/MathF.Sqrt(Head);
                peak=Math.Max(peak,s.Scores[t]);
            }
            float total=MathF.Exp(sinks[h]-peak);
            for (int t=first;t<=pos;t++) { s.Scores[t]=MathF.Exp(s.Scores[t]-peak); total+=s.Scores[t]; }
            for (int t=first;t<=pos;t++)
            {
                float scale=s.Scores[t]/total;
                for (int d=0;d<Head;d++) s.Attention[h*Head+d]+=scale*s.Values[layer][t*KvWidth+kv+d];
            }
        }
    }
    /// <inheritdoc/>
    public void Dispose()
    {
        gate.Wait();
        try { if (!disposed) { disposed=true; file.Dispose(); } }
        finally { gate.Release(); }
    }
}
