using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AIKernel.Kernel.Inference;

/// <summary>Text-only Gemma 4 E2B, mapped GGUF weights, FP32 CPU reference computation and private shared-KV state.
/// No Torch/CUDA dependency. Other Gemma sizes and multimodal inputs are not supported.</summary>
public sealed class Gemma4E2BCpuModel : IDisposable
{
    private const int Width=1536,LayerCount=35,Heads=8,PleWidth=256,Vocabulary=262144,Owners=15,Window=512;
    private const float Epsilon=1e-6f;
    private readonly GgufWeights file;
    private readonly Gemma4Tokenizer tokenizer;
    private readonly Gemma4ChatTemplate chat;
    private readonly GgufWeights.Tensor embedding,pleEmbedding,pleProjection;
    private readonly float[] finalNorm,pleNorm,fullFrequencies,localFrequencies;
    private readonly Layer[] layers=new Layer[LayerCount];
    private readonly int context,threads;
    private readonly SemaphoreSlim gate=new(1,1);
    private bool disposed;
    private sealed record Layer(int HeadSize,int Hidden,int Owner,GgufWeights.Tensor Q,GgufWeights.Tensor? K,GgufWeights.Tensor? V,
        GgufWeights.Tensor O,GgufWeights.Tensor Gate,GgufWeights.Tensor Up,GgufWeights.Tensor Down,
        GgufWeights.Tensor PleGate,GgufWeights.Tensor PleOutput,float[] Norm,float[] QNorm,float[]? KNorm,
        float[] PostAttention,float[] FfnNorm,float[] PostFfn,float[] PostPle,float Scale);

    /// <summary>Loads the E2B text graph. Defaults bound concurrency and context; mapped bytes are not an RSS guarantee.</summary>
    public Gemma4E2BCpuModel(string modelPath,int cpuThreads=4,int contextTokens=1024,long maximumWeightBytes=4L*1024*1024*1024)
    {
        if(cpuThreads is <1 or >6)throw new ArgumentOutOfRangeException(nameof(cpuThreads));
        if(contextTokens is <64 or >2048)throw new ArgumentOutOfRangeException(nameof(contextTokens));
        if(maximumWeightBytes<1)throw new ArgumentOutOfRangeException(nameof(maximumWeightBytes));
        if(new FileInfo(modelPath).Length>maximumWeightBytes)throw new NotSupportedException("Gemma file exceeds the configured mapped-weight budget.");
        context=contextTokens;threads=cpuThreads;file=new(modelPath);
        try
        {
            ValidateDefinition();tokenizer=new(file);chat=new(tokenizer);
            embedding=file.Require("token_embd.weight",Vocabulary,Width);pleEmbedding=file.Require("per_layer_token_embd.weight",Vocabulary,LayerCount*PleWidth);
            pleProjection=file.Require("per_layer_model_proj.weight",LayerCount*PleWidth,Width);
            finalNorm=Vector("output_norm.weight",Width);pleNorm=Vector("per_layer_proj_norm.weight",PleWidth);
            float[] factors=Vector("rope_freqs.weight",256);
            fullFrequencies=new float[256];localFrequencies=new float[128];
            for(int i=0;i<256;i++)
            {
                if(!float.IsFinite(factors[i])||factors[i]<=0)throw new InvalidDataException("Invalid Gemma proportional RoPE factors.");
                fullFrequencies[i]=1f/MathF.Pow(1000000f,2f*i/512)/factors[i];
            }
            for(int i=0;i<128;i++)localFrequencies[i]=1f/MathF.Pow(10000f,2f*i/256);
            for(int i=0;i<LayerCount;i++)
            {
                string p=$"blk.{i}.";bool local=IsLocal(i),own=i<Owners;int head=local?256:512,hidden=own?6144:12288;
                layers[i]=new(head,hidden,own?i:local?13:14,
                    file.Require(p+"attn_q.weight",Heads*head,Width),own?file.Require(p+"attn_k.weight",head,Width):null,
                    own?file.Require(p+"attn_v.weight",head,Width):null,file.Require(p+"attn_output.weight",Width,Heads*head),
                    file.Require(p+"ffn_gate.weight",hidden,Width),file.Require(p+"ffn_up.weight",hidden,Width),file.Require(p+"ffn_down.weight",Width,hidden),
                    file.Require(p+"inp_gate.weight",PleWidth,Width),file.Require(p+"proj.weight",Width,PleWidth),
                    Vector(p+"attn_norm.weight",Width),Vector(p+"attn_q_norm.weight",head),own?Vector(p+"attn_k_norm.weight",head):null,
                    Vector(p+"post_attention_norm.weight",Width),Vector(p+"ffn_norm.weight",Width),Vector(p+"post_ffw_norm.weight",Width),
                    Vector(p+"post_norm.weight",Width),Vector(p+"layer_output_scale.weight",1)[0]);
            }
        }
        catch{file.Dispose();throw;}
    }
    /// <summary>Encoded file size; execution uses mapped blocks rather than a full FP32 copy.</summary>
    public long WeightBytes=>file.Length;
    /// <summary>Tokenizes ordinary text without activating model control tokens or adding a BOS.</summary>
    public ImmutableArray<int> Tokenize(string text)
    {ObjectDisposedException.ThrowIf(disposed,this);return tokenizer.Encode(text).ToImmutableArray();}
    internal Gemma4Tokenizer Tokenizer=>tokenizer;
    internal Gemma4ChatTemplate Chat=>chat;
    private static bool IsLocal(int layer)=>(layer+1)%5!=0;
    private float[] Vector(string name,int width)
    {
        var values=file.Vector(file.Require(name,1,width));
        if(values.Any(x=>!float.IsFinite(x)))throw new InvalidDataException("Non-finite Gemma weights: "+name);
        return values;
    }
    private void ValidateDefinition()
    {
        if(file.Metadata.GetValueOrDefault("general.architecture") is not "gemma4"||file.Int("gemma4.block_count")!=LayerCount||
            file.Int("gemma4.embedding_length")!=Width||file.Int("gemma4.attention.head_count")!=Heads||file.Int("gemma4.attention.head_count_kv")!=1||
            file.Int("gemma4.attention.shared_kv_layers")!=20||file.Int("gemma4.embedding_length_per_layer_input")!=PleWidth||
            file.Int("gemma4.attention.sliding_window")!=Window||file.Tokens.Length!=Vocabulary||file.Int("gemma4.context_length")<context||
            file.Int("gemma4.attention.key_length")!=512||file.Int("gemma4.attention.value_length")!=512||
            file.Int("gemma4.attention.key_length_swa")!=256||file.Int("gemma4.attention.value_length_swa")!=256||
            file.Float("gemma4.attention.layer_norm_rms_epsilon",0)!=Epsilon||file.Float("gemma4.final_logit_softcapping",0)!=30f||
            file.Float("gemma4.rope.freq_base",0)!=1000000f||file.Float("gemma4.rope.freq_base_swa",0)!=10000f)
            throw new NotSupportedException("Only the Gemma 4 E2B causal text definition is supported.");
        if(file.Metadata.GetValueOrDefault("gemma4.attention.sliding_window_pattern") is not object[] pattern||pattern.Length!=LayerCount||
            pattern.Where((v,i)=>v is not bool b||b!=IsLocal(i)).Any()||
            file.Metadata.GetValueOrDefault("gemma4.feed_forward_length") is not object[] hidden||hidden.Length!=LayerCount||
            hidden.Where((v,i)=>Convert.ToInt32(v)!=(i<Owners?6144:12288)).Any())throw new InvalidDataException("Unsupported Gemma layer pattern.");
        // Every tensor in this tied-embedding definition is required below; reject extra heads,
        // experts or encoders rather than silently executing only an arbitrary subset.
        if(file.Tensors.Count!=541||file.ContainsOutput()||file.Tensors.Values.Any(t=>t.Rank>2||t.Type is not (0 or 1 or 2 or 8 or 12 or 14 or 30)))
            throw new NotSupportedException("Expected the tied-embedding E2B text tensor set; extra heads, experts and encoders are unsupported.");
    }

    /// <summary>Generates one text reply, greedily, with thinking and tools disabled. State is cleared before releasing admission.</summary>
    public async Task<CpuGeneration> GenerateAsync(string system,string user,int maxNewTokens=128,CancellationToken cancellationToken=default)
    {
        ArgumentNullException.ThrowIfNull(system);ArgumentNullException.ThrowIfNull(user);
        if(system.Length>8192||user.Length>8192||maxNewTokens is <1 or >256)throw new ArgumentOutOfRangeException(nameof(maxNewTokens));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(disposed,this);
            return await Task.Run(()=>Generate(system,user,maxNewTokens,cancellationToken),cancellationToken).ConfigureAwait(false);
        }
        finally{gate.Release();}
    }
    private CpuGeneration Generate(string system,string user,int maximum,CancellationToken cancellationToken)
    {
        var prompt=chat.Chat(system,user);var tokens=new List<int>();string finish="length";
        try
        {
            if(prompt.Count+maximum>context)throw new ArgumentException("Gemma request exceeds the configured context; input was not truncated.");
            var watch=Stopwatch.StartNew();using var state=CreateState(cancellationToken);
            for(int i=0;i<prompt.Count;i++)Forward(prompt[i],i,state,i==prompt.Count-1);
            double prefillMilliseconds=watch.Elapsed.TotalMilliseconds;
            for(int i=0;i<maximum;i++)
            {
                cancellationToken.ThrowIfCancellationRequested();int next=Argmax(state.Logits);
                if(chat.StopTokens.Contains(next))
                {
                    if(next==tokenizer.SpecialToken("<|tool_response>"))throw new NotSupportedException("Gemma requested a tool; tool execution is disabled.");
                    finish="stop";break;
                }
                tokens.Add(next);if(i+1<maximum)Forward(next,prompt.Count+i,state,true);
            }
            string text=chat.DecodeReply(tokens);
            if(string.IsNullOrWhiteSpace(text))throw new InvalidDataException("Gemma did not produce a visible text reply.");
            return new CpuGeneration(text,prompt.Count,tokens.Count,finish,watch.ElapsedMilliseconds,"gemma4-reference-fp32")
            {PrefillForwardTokens=prompt.Count,DecodeForwardTokens=Math.Max(0,tokens.Count-(finish=="stop"?0:1)),PrefillThreads=threads,DecodeThreads=threads,
                PrefillMilliseconds=prefillMilliseconds,DecodeMilliseconds=watch.Elapsed.TotalMilliseconds-prefillMilliseconds};
        }
        finally{CollectionsMarshal.AsSpan(prompt).Clear();CollectionsMarshal.AsSpan(tokens).Clear();}
    }
    internal static int Argmax(float[] logits)
    {
        int best=0;for(int i=0;i<logits.Length;i++){if(!float.IsFinite(logits[i]))throw new ArithmeticException("Non-finite Gemma logits.");if(logits[i]>logits[best])best=i;}return best;
    }
    internal State CreateState(CancellationToken ct)=>new(context,threads,ct);
    internal void Forward(int token,int position,State s,bool computeLogits,Action<int,string,float[]>? trace=null)
    {
        ObjectDisposedException.ThrowIf(disposed,this);s.Options.CancellationToken.ThrowIfCancellationRequested();
        if((uint)token>=Vocabulary||position!=s.Position||position>=context)throw new ArgumentOutOfRangeException(nameof(position));
        void Mm(GgufWeights.Tensor tensor,float[] x,float[] y)=>ReferenceCpuMatrix.Multiply(file,tensor,x,y,s.Options);
        void Trace(int layer,string stage,float[] data)=>trace?.Invoke(layer,stage,data);
        QuantizedCpuMatrix.Row(file,embedding,token,s.X);Scale(s.X,MathF.Sqrt(Width));Trace(-1,"embedding",s.X);
        QuantizedCpuMatrix.Row(file,pleEmbedding,token,s.Ple);Scale(s.Ple,MathF.Sqrt(PleWidth));
        Mm(pleProjection,s.X,s.ProjectedPle);Scale(s.ProjectedPle,1f/MathF.Sqrt(Width));
        for(int layer=0;layer<LayerCount;layer++)Norm(s.ProjectedPle.AsSpan(layer*PleWidth,PleWidth),s.ProjectedPle.AsSpan(layer*PleWidth,PleWidth),pleNorm);
        for(int i=0;i<s.Ple.Length;i++)s.Ple[i]=(s.ProjectedPle[i]+s.Ple[i])*(float)Math.Pow(2,-.5);
        Trace(-1,"ple",s.Ple);PrepareRope(localFrequencies,position,s.LocalCos,s.LocalSin);PrepareRope(fullFrequencies,position,s.FullCos,s.FullSin);
        for(int i=0;i<LayerCount;i++)
        {
            s.Options.CancellationToken.ThrowIfCancellationRequested();var l=layers[i];int head=l.HeadSize;
            Norm(s.X,s.Normal,l.Norm);Mm(l.Q,s.Normal,s.Query);
            var cos=IsLocal(i)?s.LocalCos:s.FullCos;var sin=IsLocal(i)?s.LocalSin:s.FullSin;
            for(int h=0;h<Heads;h++)
            {var q=s.Query.AsSpan(h*head,head);Norm(q,q,l.QNorm);Rope(q,cos,sin);}
            var cache=s.Caches[l.Owner];
            if(i<Owners)
            {
                Mm(l.K!,s.Normal,s.Key);Mm(l.V!,s.Normal,s.Value);
                Norm(s.Key.AsSpan(0,head),s.Key.AsSpan(0,head),l.KNorm!);Rope(s.Key.AsSpan(0,head),cos,sin);
                Norm(s.Value.AsSpan(0,head),s.Value.AsSpan(0,head),ReadOnlySpan<float>.Empty);
                cache.Append(position,s.Key.AsSpan(0,head),s.Value.AsSpan(0,head));
            }
            for(int h=0;h<Heads;h++)Attention(s.Query.AsSpan(h*head,head),cache,s.Scores,s.Attention.AsSpan(h*head,head));
            Mm(l.O,s.Attention,s.Temp);Norm(s.Temp,s.Temp,l.PostAttention);Add(s.X,s.Temp);Trace(i,"attention",s.X);
            Norm(s.X,s.Normal,l.FfnNorm);Mm(l.Gate,s.Normal,s.Gate);Mm(l.Up,s.Normal,s.Up);
            for(int j=0;j<l.Hidden;j++)s.Gate[j]=Gelu(s.Gate[j])*s.Up[j];
            Mm(l.Down,s.Gate,s.Temp);Norm(s.Temp,s.Temp,l.PostFfn);Add(s.X,s.Temp);Trace(i,"ffn",s.X);
            Mm(l.PleGate,s.X,s.PleGate);
            for(int j=0;j<PleWidth;j++)s.PleGate[j]=Gelu(s.PleGate[j])*s.Ple[i*PleWidth+j];
            Mm(l.PleOutput,s.PleGate,s.Temp);Norm(s.Temp,s.Temp,l.PostPle);Add(s.X,s.Temp);Scale(s.X,l.Scale);Trace(i,"layer",s.X);
        }
        Norm(s.X,s.Normal,finalNorm);Trace(-1,"final_norm",s.Normal);
        if(computeLogits)
        {Mm(embedding,s.Normal,s.Logits);for(int i=0;i<s.Logits.Length;i++)s.Logits[i]=30*MathF.Tanh(s.Logits[i]/30);Trace(-1,"logits",s.Logits);}
        s.Position++;
    }
    internal static void Attention(ReadOnlySpan<float> query,Gemma4KvCache cache,Span<float> scores,Span<float> output)
    {
        int count=cache.Count-cache.Start;float maximum=float.NegativeInfinity;
        for(int p=cache.Start;p<cache.Count;p++)
        {float sum=0;var key=cache.Key(p);for(int d=0;d<query.Length;d++)sum+=query[d]*key[d];scores[p-cache.Start]=sum;maximum=Math.Max(maximum,sum);}
        float total=0;for(int i=0;i<count;i++){scores[i]=MathF.Exp(scores[i]-maximum);total+=scores[i];}
        output.Clear();for(int p=cache.Start;p<cache.Count;p++)
        {float probability=scores[p-cache.Start]/total;var value=cache.Value(p);for(int d=0;d<output.Length;d++)output[d]+=probability*value[d];}
    }
    internal static void Norm(ReadOnlySpan<float> input,Span<float> output,ReadOnlySpan<float> weights)
    {
        float sum=0;foreach(float x in input)sum+=x*x;float scale=MathF.Pow(sum/input.Length+Epsilon,-.5f);
        for(int i=0;i<input.Length;i++)output[i]=(input[i]*scale)*(weights.IsEmpty?1:weights[i]);
    }
    internal static float Gelu(float x)=>.5f*x*(1+MathF.Tanh(.7978845608028654f*(x+.044715f*x*x*x)));
    private static void Scale(Span<float> values,float scale){for(int i=0;i<values.Length;i++)values[i]*=scale;}
    private static void Add(Span<float> values,ReadOnlySpan<float> extra){for(int i=0;i<values.Length;i++)values[i]+=extra[i];}
    private static void PrepareRope(float[] frequencies,int position,float[] cos,float[] sin)
    {for(int i=0;i<frequencies.Length;i++){float angle=position*frequencies[i];cos[i]=MathF.Cos(angle);sin[i]=MathF.Sin(angle);}}
    private static void Rope(Span<float> values,float[] cos,float[] sin)
    {
        int half=values.Length/2;
        for(int i=0;i<half;i++){float a=values[i],b=values[i+half];values[i]=a*cos[i]-b*sin[i];values[i+half]=b*cos[i]+a*sin[i];}
    }
    internal sealed class State : IDisposable
    {
        internal readonly float[] X=new float[Width],Normal=new float[Width],Temp=new float[Width],Query=new float[Heads*512],
            Key=new float[512],Value=new float[512],Attention=new float[Heads*512],Gate=new float[12288],Up=new float[12288],
            Ple=new float[LayerCount*PleWidth],ProjectedPle=new float[LayerCount*PleWidth],PleGate=new float[PleWidth],Logits=new float[Vocabulary],
            LocalCos=new float[128],LocalSin=new float[128],FullCos=new float[256],FullSin=new float[256];
        internal readonly float[] Scores;
        internal readonly Gemma4KvCache[] Caches;
        internal readonly ParallelOptions Options;
        internal int Position;
        internal State(int context,int threads,CancellationToken ct)
        {
            Scores=new float[context];Options=new(){MaxDegreeOfParallelism=threads,CancellationToken=ct};
            Caches=Enumerable.Range(0,Owners).Select(i=>new Gemma4KvCache(IsLocal(i)?256:512,context,IsLocal(i)?Window:0)).ToArray();
        }
        public void Dispose()
        {
            foreach(var cache in Caches)cache.Dispose();
            foreach(var array in new[]{X,Normal,Temp,Query,Key,Value,Attention,Gate,Up,Ple,ProjectedPle,PleGate,Logits,Scores,LocalCos,LocalSin,FullCos,FullSin})Array.Clear(array);
            Position=0;
        }
    }
    /// <summary>Waits for active generation to finish, then releases the mapped model.</summary>
    public void Dispose(){gate.Wait();try{if(disposed)return;disposed=true;file.Dispose();}finally{gate.Release();}}
}
