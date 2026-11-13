using System.Diagnostics;
using System.Numerics;
using System.Security.Cryptography;

namespace AIKernel.Kernel.Inference;

/// <summary>A CPU-only Qwen3 dense transformer using immutable mapped Q1_0 weights and private request state.</summary>
public sealed class Qwen3CpuModel : IDisposable
{
    private readonly GgufWeights file;
    private readonly QwenTokenizer tokenizer;
    private readonly GgufWeights.Tensor embedding, output;
    private readonly Layer[] layers;
    private readonly float[] finalNorm;
    private readonly int width,hidden,heads,kvHeads,headSize,querySize,kvSize,context,threads;
    private readonly float epsilon;
    private readonly float[] frequencies;
    private readonly float ropeAmplitude;
    private readonly SemaphoreSlim gate=new(1,1);
    private readonly string backend;
    private PrefixState? prefixCache;
    private sealed record PrefixState(string System,int Count,float[][] Keys,float[][] Values);
    private sealed record Layer(GgufWeights.Tensor Q,GgufWeights.Tensor K,GgufWeights.Tensor V,GgufWeights.Tensor O,
        GgufWeights.Tensor Gate,GgufWeights.Tensor Up,GgufWeights.Tensor Down,float[] Norm,float[] FfnNorm,float[] QNorm,float[] KNorm);

    /// <summary>Loads only supported bounded dense models. No CUDA library is loaded.</summary>
    public Qwen3CpuModel(string modelPath,int cpuThreads=4,int contextTokens=1024)
    {
        if(cpuThreads<1||cpuThreads>6||contextTokens<64||contextTokens>2048)throw new ArgumentOutOfRangeException(nameof(cpuThreads));
        threads=cpuThreads;context=contextTokens;backend=QuantizedCpuMatrix.Backend;
        file=new(modelPath);
        try
        {
            width=file.Int("qwen3.embedding_length");hidden=file.Int("qwen3.feed_forward_length");
            heads=file.Int("qwen3.attention.head_count");kvHeads=file.Int("qwen3.attention.head_count_kv");
            headSize=file.Int("qwen3.attention.key_length",width/Math.Max(1,heads));
            var count=file.Int("qwen3.block_count");epsilon=file.Float("qwen3.attention.layer_norm_rms_epsilon",1e-6f);
            if(width is <128 or >8192||hidden is <128 or >32768||heads is <1 or >64||kvHeads<1||heads%kvHeads!=0||
               headSize is <32 or >256||headSize%2!=0||count is <1 or >64||!float.IsFinite(epsilon)||epsilon<=0||
               context>file.Int("qwen3.context_length")||file.Int("qwen3.attention.value_length",headSize)!=headSize)
                throw new InvalidDataException("Unsupported Qwen3 dimensions.");
            querySize=heads*headSize;kvSize=kvHeads*headSize;
            embedding=file.Require("token_embd.weight",file.Tokens.Length,width);
            output=file.ContainsOutput()?file.Require("output.weight",file.Tokens.Length,width):embedding;
            finalNorm=file.Vector(file.Require("output_norm.weight",1,width));
            layers=new Layer[count];
            for(int i=0;i<count;i++)
            {
                string p=$"blk.{i}.";
                layers[i]=new(file.Require(p+"attn_q.weight",querySize,width),file.Require(p+"attn_k.weight",kvSize,width),
                    file.Require(p+"attn_v.weight",kvSize,width),file.Require(p+"attn_output.weight",width,querySize),
                    file.Require(p+"ffn_gate.weight",hidden,width),file.Require(p+"ffn_up.weight",hidden,width),
                    file.Require(p+"ffn_down.weight",width,hidden),file.Vector(file.Require(p+"attn_norm.weight",1,width)),
                    file.Vector(file.Require(p+"ffn_norm.weight",1,width)),file.Vector(file.Require(p+"attn_q_norm.weight",1,headSize)),
                    file.Vector(file.Require(p+"attn_k_norm.weight",1,headSize)));
            }
            float theta=file.Float("qwen3.rope.freq_base",1000000);
            var scaling=file.Metadata.GetValueOrDefault("qwen3.rope.scaling.type") as string;
            if(scaling is not (null or "none" or "yarn"))throw new NotSupportedException("Unsupported rotary scaling.");
            float factor=scaling=="yarn"?file.Float("qwen3.rope.scaling.factor",1):1;
            int original=file.Int("qwen3.rope.scaling.original_context_length",8192);
            if(!float.IsFinite(theta)||theta<=1||!float.IsFinite(factor)||factor<1||factor>32||original<1)throw new InvalidDataException("Invalid rotary metadata.");
            ropeAmplitude=factor==1?1:1+0.1f*MathF.Log(factor);
            frequencies=new float[headSize/2];
            float low=Math.Max(0,MathF.Floor(headSize*MathF.Log(original/(32*2*MathF.PI))/(2*MathF.Log(theta))));
            float high=Math.Min(headSize-1,MathF.Ceiling(headSize*MathF.Log(original/(2*MathF.PI))/(2*MathF.Log(theta))));
            for(int i=0;i<frequencies.Length;i++)
            {
                float freq=MathF.Pow(theta,-2f*i/headSize);
                float extrapolation=1-Math.Clamp((i-low)/Math.Max(0.001f,high-low),0,1);
                frequencies[i]=freq*(extrapolation+(1-extrapolation)/factor);
            }
            tokenizer=new(file);
        }
        catch{file.Dispose();throw;}
    }
    /// <summary>Selected and OS-supported instruction family.</summary>
    public string CpuBackend=>backend;
    /// <summary>Encoded mapped-file length, without FP32 expansion.</summary>
    public long WeightBytes=>file.Length;

    /// <summary>Precomputes only a trusted system-prefix KV before accepting user requests.</summary>
    public async Task WarmupAsync(string system,CancellationToken cancellationToken=default)
    {
        ArgumentNullException.ThrowIfNull(system);
        if(system.Length>8192)throw new ArgumentException("System instructions exceed the input limit.");
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await Task.Run(()=>{
                if(prefixCache?.System==system)return;
                var prefix=tokenizer.Prefix(system);
                if(prefix.Count>=context)throw new ArgumentException("System prefix exceeds the context limit.");
                using var state=new State(this,cancellationToken);
                for(int i=0;i<prefix.Count;i++){cancellationToken.ThrowIfCancellationRequested();Forward(prefix[i],i,state);}
                prefixCache=new(system,prefix.Count,state.Keys.Select(k=>k.AsSpan(0,prefix.Count*kvSize).ToArray()).ToArray(),
                    state.Values.Select(v=>v.AsSpan(0,prefix.Count*kvSize).ToArray()).ToArray());
            },cancellationToken).ConfigureAwait(false);
        }
        finally{gate.Release();}
    }
    /// <summary>Generates a bounded reply. Concurrent use is serialized; request KV state is never shared.</summary>
    public async Task<CpuGeneration> GenerateAsync(string system,string user,int maxNewTokens=128,CancellationToken cancellationToken=default)
    {
        if(maxNewTokens<1||maxNewTokens>256)throw new ArgumentOutOfRangeException(nameof(maxNewTokens));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try{return await Task.Run(()=>Run(system,user,maxNewTokens,null,cancellationToken),cancellationToken).ConfigureAwait(false);}
        finally{gate.Release();}
    }
    /// <summary>Scores the next token over explicitly supplied single-token labels, without fabricated confidence.</summary>
    public async Task<CpuGeneration> ClassifyAsync(string system,string user,IReadOnlyList<string> labels,CancellationToken cancellationToken=default)
    {
        if(labels.Count is <2 or >16)throw new ArgumentException("Between two and sixteen labels required.");
        int[] ids=labels.Select(label=>{var encoded=tokenizer.Encode(label);return encoded.Count==1?encoded[0]:throw new ArgumentException("Labels must each encode to one token.");}).ToArray();
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try{return await Task.Run(()=>Run(system,user,1,ids,cancellationToken),cancellationToken).ConfigureAwait(false);}
        finally{gate.Release();}
    }
    private CpuGeneration Run(string system,string user,int maximum,int[]? choices,CancellationToken token)
    {
        if(system.Length>8192||user.Length>4096)throw new ArgumentException("Text exceeds model input limit.");
        var prompt=tokenizer.Chat(system,user);
        if(prompt.Count+maximum>context)throw new ArgumentException("Model context budget exceeded.");
        using var state=new State(this,token);var watch=Stopwatch.StartNew();var generated=new List<int>();
        int prefixCount=tokenizer.Prefix(system).Count,begin=0;
        if(prefixCache is { } cached && cached.System==system)
        {
            for(int i=0;i<layers.Length;i++){cached.Keys[i].CopyTo(state.Keys[i],0);cached.Values[i].CopyTo(state.Values[i],0);}
            begin=cached.Count;
        }
        for(int i=begin;i<prompt.Count;i++)
        {
            token.ThrowIfCancellationRequested();Forward(prompt[i],i,state);
            if(i+1==prefixCount)
            {
                // Cache only trusted system instructions, never inquiry text or generated content.
                prefixCache=new(system,prefixCount,state.Keys.Select(k=>k.AsSpan(0,prefixCount*kvSize).ToArray()).ToArray(),
                    state.Values.Select(v=>v.AsSpan(0,prefixCount*kvSize).ToArray()).ToArray());
            }
        }
        var finish="length";
        for(int step=0;step<maximum;step++)
        {
            token.ThrowIfCancellationRequested();
            int next;
            if(choices is not null)
            {
                float best=float.NegativeInfinity;next=choices[0];var score=new float[1];
                foreach(int id in choices)
                {
                    var row=output with {Offset=output.Offset+(long)id*output.RowBytes,Rows=1};
                    QuantizedCpuMatrix.Multiply(file,row,state.Normal,score,state.Options,backend);
                    if(score[0]>best){best=score[0];next=id;}
                }
                if(!float.IsFinite(best))throw new ArithmeticException("Non-finite model score.");
            }
            else
            {
                QuantizedCpuMatrix.Multiply(file,output,state.Normal,state.Logits,state.Options,backend);
                next=0;float best=float.NegativeInfinity;
                for(int i=0;i<state.Logits.Length;i++)if(state.Logits[i]>best){best=state.Logits[i];next=i;}
                if(!float.IsFinite(best))throw new ArithmeticException("Non-finite model logits.");
            }
            if(next==tokenizer.End){finish="stop";break;}
            generated.Add(next);
            if(choices is not null){finish="classified";break;}
            if(step+1<maximum)Forward(next,prompt.Count+step,state);
        }
        return new(tokenizer.DecodeReply(generated),prompt.Count,generated.Count,finish,watch.ElapsedMilliseconds,backend);
    }
    private void Forward(int id,int position,State s)
    {
        QuantizedCpuMatrix.Row(file,embedding,id,s.X);
        for(int index=0;index<layers.Length;index++)
        {
            s.Options.CancellationToken.ThrowIfCancellationRequested();var l=layers[index];
            Norm(s.X,l.Norm,s.Normal,epsilon);
            QuantizedCpuMatrix.Multiply(file,l.Q,s.Normal,s.Q,s.Options,backend);
            QuantizedCpuMatrix.Multiply(file,l.K,s.Normal,s.K,s.Options,backend);
            QuantizedCpuMatrix.Multiply(file,l.V,s.Normal,s.V,s.Options,backend);
            for(int h=0;h<heads;h++)Norm(s.Q.AsSpan(h*headSize,headSize),l.QNorm,s.Q.AsSpan(h*headSize,headSize),epsilon);
            for(int h=0;h<kvHeads;h++)Norm(s.K.AsSpan(h*headSize,headSize),l.KNorm,s.K.AsSpan(h*headSize,headSize),epsilon);
            Rope(s.Q,heads,position);Rope(s.K,kvHeads,position);
            var keys=s.Keys[index];var values=s.Values[index];
            s.K.CopyTo(keys,position*kvSize);s.V.CopyTo(values,position*kvSize);
            Parallel.For(0,heads,s.Options,h=>{
                var scores=new float[position+1];int kv=h/(heads/kvHeads)*headSize;float max=float.NegativeInfinity;
                var q=s.Q.AsSpan(h*headSize,headSize);
                for(int t=0;t<=position;t++){float v=Dot(q,keys.AsSpan(t*kvSize+kv,headSize))/MathF.Sqrt(headSize);scores[t]=v;max=Math.Max(max,v);}
                float sum=0;for(int t=0;t<=position;t++){scores[t]=MathF.Exp(scores[t]-max);sum+=scores[t];}
                var target=s.Attention.AsSpan(h*headSize,headSize);target.Clear();
                for(int t=0;t<=position;t++){float p=scores[t]/sum;var v=values.AsSpan(t*kvSize+kv,headSize);for(int d=0;d<headSize;d++)target[d]+=p*v[d];}
            });
            QuantizedCpuMatrix.Multiply(file,l.O,s.Attention,s.Projected,s.Options,backend);
            for(int i=0;i<width;i++)s.X[i]+=s.Projected[i];
            Norm(s.X,l.FfnNorm,s.Normal,epsilon);
            QuantizedCpuMatrix.Multiply(file,l.Gate,s.Normal,s.Gate,s.Options,backend);
            QuantizedCpuMatrix.Multiply(file,l.Up,s.Normal,s.Up,s.Options,backend);
            for(int i=0;i<hidden;i++)s.Gate[i]=s.Gate[i]/(1+MathF.Exp(-s.Gate[i]))*s.Up[i];
            QuantizedCpuMatrix.Multiply(file,l.Down,s.Gate,s.Projected,s.Options,backend);
            for(int i=0;i<width;i++)s.X[i]+=s.Projected[i];
        }
        Norm(s.X,finalNorm,s.Normal,epsilon);
    }
    private void Rope(float[] vector,int count,int position)
    {
        int half=headSize/2;
        for(int h=0;h<count;h++)for(int i=0;i<half;i++)
        {
            float angle=position*frequencies[i],c=MathF.Cos(angle)*ropeAmplitude,s=MathF.Sin(angle)*ropeAmplitude;
            int a=h*headSize+i,b=a+half;float x=vector[a],y=vector[b];vector[a]=x*c-y*s;vector[b]=x*s+y*c;
        }
    }
    private static float Dot(ReadOnlySpan<float> a,ReadOnlySpan<float> b){float sum=0;for(int i=0;i<a.Length;i++)sum+=a[i]*b[i];return sum;}
    private static void Norm(ReadOnlySpan<float> x,ReadOnlySpan<float> weights,Span<float> y,float eps)
    {float sum=Dot(x,x);float scale=1/MathF.Sqrt(sum/x.Length+eps);for(int i=0;i<x.Length;i++)y[i]=x[i]*scale*weights[i];}
    private sealed class State : IDisposable
    {
        public readonly float[] X,Normal,Q,K,V,Attention,Projected,Gate,Up,Logits;
        public readonly float[][] Keys,Values;
        public readonly ParallelOptions Options;
        public State(Qwen3CpuModel m,CancellationToken token)
        {
            X=new float[m.width];Normal=new float[m.width];Q=new float[m.querySize];K=new float[m.kvSize];V=new float[m.kvSize];
            Attention=new float[m.querySize];Projected=new float[m.width];Gate=new float[m.hidden];Up=new float[m.hidden];Logits=new float[m.file.Tokens.Length];
            Keys=Enumerable.Range(0,m.layers.Length).Select(_=>new float[m.context*m.kvSize]).ToArray();
            Values=Enumerable.Range(0,m.layers.Length).Select(_=>new float[m.context*m.kvSize]).ToArray();
            Options=new(){MaxDegreeOfParallelism=m.threads,CancellationToken=token};
        }
        public void Dispose(){foreach(var array in new[]{X,Normal,Q,K,V,Attention,Projected,Gate,Up,Logits}.Concat(Keys).Concat(Values))Array.Clear(array);}
    }
    /// <summary>Dispose only after active requests have stopped.</summary>
    public void Dispose(){if(prefixCache is {} c)foreach(var a in c.Keys.Concat(c.Values))Array.Clear(a);prefixCache=null;file.Dispose();gate.Dispose();}
}
/// <summary>Measured result of a completed CPU generation.</summary>
public sealed record CpuGeneration(string Text,int InputTokens,int OutputTokens,string FinishReason,long ElapsedMilliseconds,string CpuBackend);

internal static class GgufExtensions
{
    internal static bool ContainsOutput(this GgufWeights file)=>file.Tensors.ContainsKey("output.weight");
}
