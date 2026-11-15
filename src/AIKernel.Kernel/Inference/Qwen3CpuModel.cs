using System.Diagnostics;
using System.Numerics;
using System.Security.Cryptography;

namespace AIKernel.Kernel.Inference;

/// <summary>A CPU-only Qwen3 dense transformer using immutable mapped Q1_0 weights and private request state.</summary>
public sealed class Qwen3CpuModel : IDisposable
{
    private readonly GgufWeights file;
    private readonly IMatrixOffload? gpu;
    private readonly bool quantizeGpuActivations;
    private readonly bool profileEnabled=Environment.GetEnvironmentVariable("AIKERNEL_INFERENCE_PROFILE")=="1";
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
    private readonly string requestedInstructionSet;
    private string prefillBackend,decodeBackend;
    private int prefillThreads,decodeThreads;
    private CpuKernelTuner? tuner;
    private CpuAutoTuningOptions? tuningOptions;
    private CpuTuningStatus? tuningStatus;
    private PrefixState? prefixCache;
    private Qwen3Workspace? workspace;
    private bool cleanupFailed;
    private bool integerWarmupComplete;
    private sealed record PrefixState(string System,int Count,float[][] Keys,float[][] Values);
    private sealed record Layer(GgufWeights.Tensor Q,GgufWeights.Tensor K,GgufWeights.Tensor V,GgufWeights.Tensor O,
        GgufWeights.Tensor Gate,GgufWeights.Tensor Up,GgufWeights.Tensor Down,float[] Norm,float[] FfnNorm,float[] QNorm,float[] KNorm);

    /// <summary>Loads only supported bounded dense models. No CUDA library is loaded.</summary>
    public Qwen3CpuModel(string modelPath,int cpuThreads=4,int contextTokens=1024)
        : this(modelPath,cpuThreads,contextTokens,null,0) { }

    /// <summary>Selects a CPU instruction set per model. Null inherits AIKERNEL_CPU_GEMV; absent configuration uses auto.
    /// Explicit auto overrides the environment. Unsupported forced sets fail before the model is opened.</summary>
    public Qwen3CpuModel(string modelPath,int cpuThreads,int contextTokens,string? instructionSet)
        :this(modelPath,cpuThreads,contextTokens,null,0,instructionSet:instructionSet) { }

    internal Qwen3CpuModel(string modelPath,int cpuThreads,int contextTokens,int? deviceOrdinal,long maximumWeightBytes,
        bool vulkan=false,bool gpuQ8=false,string? instructionSet=null,CudaQ8Kernel? gpuInt8=null)
    {
        if(cpuThreads<1||cpuThreads>6||contextTokens<64||contextTokens>2048)throw new ArgumentOutOfRangeException(nameof(cpuThreads));
        requestedInstructionSet=instructionSet??Environment.GetEnvironmentVariable("AIKERNEL_CPU_GEMV")??"auto";
        if(requestedInstructionSet.Length==0)requestedInstructionSet="auto";
        threads=cpuThreads;context=contextTokens;backend=QuantizedCpuMatrix.Resolve(requestedInstructionSet);prefillBackend=decodeBackend=backend;prefillThreads=decodeThreads=cpuThreads;
        quantizeGpuActivations=gpuQ8;
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
            if(deviceOrdinal.HasValue)gpu=vulkan
                ? new VulkanQuantizedMatrix(file,deviceOrdinal.Value,maximumWeightBytes)
                : new CudaQuantizedMatrix(file,deviceOrdinal.Value,maximumWeightBytes,gpuInt8);
        }
        catch{file.Dispose();throw;}
    }
    /// <summary>Selected and OS-supported instruction family.</summary>
    public string RequestedInstructionSet=>requestedInstructionSet;
    /// <summary>Actual matrix instruction set(s), including tuning decisions.</summary>
    public string CpuBackend=>prefillBackend==decodeBackend?prefillBackend:$"prefill={prefillBackend};decode={decodeBackend}";
    private string ActivationPrecision=>(gpu as CudaQuantizedMatrix)?.UsesInt8==true?"q8-int32":quantizeGpuActivations||backend.EndsWith("-q8",StringComparison.Ordinal)?"q8":"fp32";
    /// <summary>Most recent tuning decision, or null when tuning was not requested.</summary>
    public CpuTuningStatus? TuningStatus=>tuningStatus;
    /// <summary>Encoded mapped-file length, without FP32 expansion.</summary>
    public long WeightBytes=>file.Length;

    /// <summary>Measures supported kernels on mapped weight samples. Call at startup or while admission is closed.</summary>
    public async Task<CpuTuningStatus> TuneCpuAsync(CpuAutoTuningOptions options,bool refresh=false,CancellationToken cancellationToken=default)
    {
        ArgumentNullException.ThrowIfNull(options);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string forced=requestedInstructionSet;
            if(!string.IsNullOrEmpty(forced)&&forced!="auto"&&forced!="q8-auto")
                return tuningStatus=new(backend,backend,"operator-forced","",0,0,threads,threads);
            if(Environment.GetEnvironmentVariable("AIKERNEL_Q1_KERNEL") is { } kernel&&kernel!="vector")
                return tuningStatus=new(backend,backend,"non-vector-kernel","",0,0,threads,threads);
            if(tuner is null||tuningOptions!=options)
            {
                var candidates=forced=="q8-auto"?Int8CpuMatrix.Candidates(includeAmx:false):new List<string>();
                if(forced!="q8-auto"&&System.Runtime.Intrinsics.X86.Avx2.IsSupported&&System.Runtime.Intrinsics.X86.Fma.IsSupported)candidates.Add("avx2");
                if(forced!="q8-auto"&&System.Runtime.Intrinsics.X86.Avx512F.IsSupported)candidates.Add("avx512");
                if(candidates.Count==0)candidates.Add(backend);
                IEnumerable<int> counts=options.TuneThreadCount
                    ?new[]{1,2,4,6}.Where(n=>n<=Math.Min(threads,Environment.ProcessorCount)).Append(Math.Min(threads,Environment.ProcessorCount)).Distinct().OrderDescending()
                    :new[]{threads};
                var choices=candidates.SelectMany(b=>counts.Select(n=>new CpuKernelChoice(b,n)));
                tuner=new(options,CpuKernelTuner.Identity(options,threads,context,$"{file.Length}/{width}/{hidden}/{layers.Length}/{(forced=="q8-auto"?"q8-v1":"fp32")}/tuneThreads={options.TuneThreadCount}"),choices);
                tuningOptions=options;
            }
            var selected=await Task.Run(()=>tuner.Tune(ProbeKernel,ValidateKernel,refresh,cancellationToken),cancellationToken).ConfigureAwait(false);
            // The trusted prefix represents the same model/input across validated arithmetic kernels.
            prefillBackend=selected.PrefillBackend;decodeBackend=selected.DecodeBackend;prefillThreads=selected.PrefillThreads;decodeThreads=selected.DecodeThreads;tuningStatus=selected;
            return selected;
        }
        finally{gate.Release();}

        IEnumerable<GgufWeights.Tensor> Samples(string phase)
        {
            foreach(int i in new[]{0,layers.Length/2,layers.Length-1}.Distinct())
            {yield return layers[i].Q;yield return layers[i].Gate;yield return layers[i].Down;}
            if(phase=="decode")yield return output with {Rows=Math.Min(8192,output.Rows)};
        }
        double ProbeKernel(string phase,CpuKernelChoice selected)
        {
            // Current prefill is repeated GEMV, not a batched GEMM or a VNNI kernel.
            var matrices=Samples(phase).ToArray();
            var inputs=matrices.Select(t=>Enumerable.Range(0,t.Columns).Select(i=>MathF.Sin(i*.017f)).ToArray()).ToArray();
            var outputs=matrices.Select(t=>new float[t.Rows]).ToArray();
            var parallel=new ParallelOptions{MaxDegreeOfParallelism=selected.Threads,CancellationToken=cancellationToken};
            var watch=Stopwatch.StartNew();
            for(int pass=0;pass<(phase=="prefill"?2:1);pass++)for(int i=0;i<matrices.Length;i++)
            {cancellationToken.ThrowIfCancellationRequested();QuantizedCpuMatrix.Multiply(file,matrices[i],inputs[i],outputs[i],parallel,selected.Backend);}
            return Math.Max(watch.Elapsed.TotalMilliseconds,.000001);
        }
        bool ValidateKernel(CpuKernelChoice selected)
        {
            var parallel=new ParallelOptions{MaxDegreeOfParallelism=selected.Threads,CancellationToken=cancellationToken};
            foreach(var tensor in Samples("decode"))
            {
                cancellationToken.ThrowIfCancellationRequested();var sample=tensor with {Rows=Math.Min(8,tensor.Rows)};
                float[] x=Enumerable.Range(0,sample.Columns).Select(i=>MathF.Sin(i*.17f)).ToArray(),expected=new float[sample.Rows],actual=new float[sample.Rows];
                QuantizedCpuMatrix.Multiply(file,sample,x,expected,parallel,
                    selected.Backend.EndsWith("-q8",StringComparison.Ordinal)?"scalar-q8":"scalar");
                QuantizedCpuMatrix.Multiply(file,sample,x,actual,parallel,selected.Backend);
                for(int i=0;i<actual.Length;i++)if(!float.IsFinite(actual[i])||Math.Abs(actual[i]-expected[i])>.001f*Math.Max(1,Math.Abs(expected[i])))return false;
            }
            return true;
        }
    }

    /// <summary>Precomputes only a trusted system-prefix KV before accepting user requests.</summary>
    public async Task WarmupAsync(string system,CancellationToken cancellationToken=default)
    {
        ArgumentNullException.ThrowIfNull(system);
        if(system.Length>8192)throw new ArgumentException("System instructions exceed the input limit.");
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await Task.Run(()=>WithCleanup(()=>{
                if(prefixCache?.System==system&&(gpu is not CudaQuantizedMatrix {UsesInt8:true}||integerWarmupComplete))return 0;
                var prefix=tokenizer.Prefix(system);
                if(prefix.Count>=context)throw new ArgumentException("System prefix exceeds the context limit.");
                using var state=AcquireWorkspace(cancellationToken);state.Options.MaxDegreeOfParallelism=prefillThreads;
                for(int i=0;i<prefix.Count;i++){cancellationToken.ThrowIfCancellationRequested();Forward(prefix[i],i,state,prefillBackend);}
                // Calibrate output and single-label shapes before request admission too.
                if(gpu is CudaQuantizedMatrix cuda&&cuda.UsesInt8)
                {
                    Multiply(output,state.Normal,state.Logits,state.Options,prefillBackend);
                    Multiply(output with {Rows=1},state.Normal,state.Logits,state.Options,prefillBackend);
                    integerWarmupComplete=true;
                }
                ReplacePrefix(new(system,prefix.Count,state.Keys.Select(k=>k.AsSpan(0,prefix.Count*kvSize).ToArray()).ToArray(),
                    state.Values.Select(v=>v.AsSpan(0,prefix.Count*kvSize).ToArray()).ToArray()));
                return 0;
            }),cancellationToken).ConfigureAwait(false);
        }
        finally{gate.Release();}
    }
    /// <summary>Generates a bounded reply. Concurrent use is serialized; request KV state is never shared.</summary>
    public async Task<CpuGeneration> GenerateAsync(string system,string user,int maxNewTokens=128,CancellationToken cancellationToken=default)
    {
        if(maxNewTokens<1||maxNewTokens>256)throw new ArgumentOutOfRangeException(nameof(maxNewTokens));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try{return await Task.Run(()=>WithCleanup(()=>Run(system,user,maxNewTokens,null,cancellationToken)),cancellationToken).ConfigureAwait(false);}
        finally{gate.Release();}
    }
    /// <summary>Benchmark only: greedy generation for exactly tokenCount steps, including EOS tokens.
    /// This is not a user-facing reply API. Context limits and cancellation still apply.</summary>
    public async Task<CpuGeneration> GenerateBenchmarkAsync(string system,string user,int tokenCount=128,CancellationToken cancellationToken=default)
    {
        if(tokenCount<1||tokenCount>256)throw new ArgumentOutOfRangeException(nameof(tokenCount));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try{return await Task.Run(()=>WithCleanup(()=>Run(system,user,tokenCount,null,cancellationToken,ignoreEos:true)),cancellationToken).ConfigureAwait(false);}
        finally{gate.Release();}
    }
    /// <summary>Scores the next token over explicitly supplied single-token labels, without fabricated confidence.</summary>
    public async Task<CpuGeneration> ClassifyAsync(string system,string user,IReadOnlyList<string> labels,CancellationToken cancellationToken=default)
    {
        if(labels.Count is <2 or >16)throw new ArgumentException("Between two and sixteen labels required.");
        int[] ids=labels.Select(label=>{var encoded=tokenizer.Encode(label);return encoded.Count==1?encoded[0]:throw new ArgumentException("Labels must each encode to one token.");}).ToArray();
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try{return await Task.Run(()=>WithCleanup(()=>Run(system,user,1,ids,cancellationToken)),cancellationToken).ConfigureAwait(false);}
        finally{gate.Release();}
    }
    private CpuGeneration Run(string system,string user,int maximum,int[]? choices,CancellationToken token,bool ignoreEos=false)
    {
        if(system.Length>8192||user.Length>4096)throw new ArgumentException("Text exceeds model input limit.");
        var profile=profileEnabled?new InferenceProfiler():null;
        if(profile is not null)(gpu as CudaQuantizedMatrix)?.BeginProfile();
        long stage=profile?.Start()??0;
        var prompt=tokenizer.Chat(system,user);
        profile?.Add(InferenceStage.Tokenization,stage);
        if(prompt.Count+maximum>context)throw new ArgumentException("Model context budget exceeded.");
        stage=profile?.Start()??0;
        var state=AcquireWorkspace(token);
        profile?.Add(InferenceStage.StateAllocation,stage);
        CpuGeneration result;
        try
        {
        state.Options.MaxDegreeOfParallelism=prefillThreads;var watch=Stopwatch.StartNew();var generated=new List<int>();
        stage=profile?.Start()??0;
        int prefixCount=tokenizer.Prefix(system).Count,begin=0;
        if(prefixCache is { } cached && cached.System==system)
        {
            state.MarkUsed(cached.Count);
            for(int i=0;i<layers.Length;i++){cached.Keys[i].CopyTo(state.Keys[i],0);cached.Values[i].CopyTo(state.Values[i],0);}
            begin=cached.Count;
        }
        profile?.Add(InferenceStage.PrefixCopy,stage);
        if(profile is not null)profile.Phase=InferencePhase.Prefill;
        for(int i=begin;i<prompt.Count;i++)
        {
            token.ThrowIfCancellationRequested();Forward(prompt[i],i,state,prefillBackend,profile);
            if(i+1==prefixCount)
            {
                // Cache only trusted system instructions, never inquiry text or generated content.
                ReplacePrefix(new(system,prefixCount,state.Keys.Select(k=>k.AsSpan(0,prefixCount*kvSize).ToArray()).ToArray(),
                    state.Values.Select(v=>v.AsSpan(0,prefixCount*kvSize).ToArray()).ToArray()));
            }
        }
        long prefillTicks=watch.ElapsedTicks;int decodeForwards=0;state.Options.MaxDegreeOfParallelism=decodeThreads;
        if(profile is not null)profile.Phase=InferencePhase.Decode;
        var finish=ignoreEos?"benchmark-fixed-tokens":"length";
        for(int step=0;step<maximum;step++)
        {
            token.ThrowIfCancellationRequested();
            int next;
            if(choices is not null)
            {
                float best=float.NegativeInfinity;next=choices[0];var score=state.Logits;
                foreach(int id in choices)
                {
                    var row=output with {Offset=output.Offset+(long)id*output.RowBytes,Rows=1};
                    Multiply(row,state.Normal,score,state.Options,decodeBackend,profile);
                    if(score[0]>best){best=score[0];next=id;}
                }
                if(!float.IsFinite(best))throw new ArithmeticException("Non-finite model score.");
            }
            else
            {
                Multiply(output,state.Normal,state.Logits,state.Options,decodeBackend,profile);
                stage=profile?.Start()??0;
                next=0;float best=float.NegativeInfinity;
                for(int i=0;i<state.Logits.Length;i++)if(state.Logits[i]>best){best=state.Logits[i];next=i;}
                profile?.Add(InferenceStage.Argmax,stage);
                if(!float.IsFinite(best))throw new ArithmeticException("Non-finite model logits.");
            }
            if(next==tokenizer.End&&!ignoreEos){finish="stop";break;}
            generated.Add(next);
            if(choices is not null){finish="classified";break;}
            if(step+1<maximum){Forward(next,prompt.Count+step,state,decodeBackend,profile);decodeForwards++;}
        }
        long elapsed=watch.ElapsedMilliseconds;
        double prefillMs=(double)prefillTicks*1000/Stopwatch.Frequency,decodeMs=watch.Elapsed.TotalMilliseconds-prefillMs;
        tuner?.Observe("prefill",prefillBackend+"@"+prefillThreads,prompt.Count,prompt.Count-begin,prefillMs);
        if(choices is null)tuner?.Observe("decode",decodeBackend+"@"+decodeThreads,prompt.Count+generated.Count,generated.Count,decodeMs);
        result=new(ignoreEos?string.Empty:tokenizer.DecodeReply(generated),prompt.Count,generated.Count,finish,elapsed,CpuBackend)
        {ActivationPrecision=ActivationPrecision,GeneratedTokenSha256=ignoreEos?Convert.ToHexStringLower(SHA256.HashData(System.Runtime.InteropServices.MemoryMarshal.AsBytes(generated.ToArray().AsSpan()))):null,PrefillMilliseconds=prefillMs,DecodeMilliseconds=decodeMs,PrefillForwardTokens=prompt.Count-begin,DecodeForwardTokens=decodeForwards,PrefillThreads=prefillThreads,DecodeThreads=decodeThreads};
        }
        finally
        {
            if(profile is not null)profile.Phase=InferencePhase.Request;
            stage=profile?.Start()??0;state.Dispose();profile?.Add(InferenceStage.StateClear,stage);
        }
        if(profile is null)return result;
        var snapshot=profile.Snapshot(width,hidden,layers.Length,heads,kvHeads,headSize,context,CpuBackend,
            ActivationPrecision);
        return result with {Profile=snapshot with {CudaMatrix=(gpu as CudaQuantizedMatrix)?.ReadProfile()}};
    }
    internal long DeviceWeightBytes=>gpu?.WeightBytes??0;
    internal string? DevicePciBusId=>gpu?.PciBusId;
    internal CudaQ8Statistics? CudaQ8Statistics=>(gpu as CudaQuantizedMatrix)?.Statistics;
    internal object? VulkanDiagnostics=>(gpu as VulkanQuantizedMatrix)?.Diagnostics;
    internal VulkanComputeDeviceInfo? VulkanDevice=>(gpu as VulkanQuantizedMatrix)?.DeviceInfo;
    private T WithCleanup<T>(Func<T> work)
    {
        if(cleanupFailed)throw new InvalidOperationException("Model disabled after GPU scratch clearing failure.");
        // Runs inside the admission gate. A cancelled queued request must not wipe another request's scratch.
        return gpu is null?work():InferenceRequestCleanup.Execute(work,gpu.ZeroScratch,()=>cleanupFailed=true);
    }
    private void ReplacePrefix(PrefixState next)
    {
        var old=prefixCache;prefixCache=next;
        if(old is not null)foreach(var array in old.Keys.Concat(old.Values))Array.Clear(array);
    }
    private void Multiply(GgufWeights.Tensor tensor,float[] input,float[] output,ParallelOptions options,string selectedBackend,InferenceProfiler? profile=null)
    {
        long started=profile?.Start()??0;
        if(gpu is null)QuantizedCpuMatrix.Multiply(file,tensor,input,output,options,selectedBackend,profile);
        else if(quantizeGpuActivations&&tensor.Type==41)
        {
            options.CancellationToken.ThrowIfCancellationRequested();
            using var rounded=new Q8ActivationBuffer(input,tensor.Columns,profile);
            gpu.Multiply(tensor,rounded.Values,output,options.CancellationToken,profile);
        }
        else gpu.Multiply(tensor,input,output,options.CancellationToken,profile);
        profile?.Add(InferenceStage.Matrix,started);
    }
    private void Forward(int id,int position,Qwen3Workspace s,string selectedBackend,InferenceProfiler? profile=null)
    {
        long stage=profile?.Start()??0;
        s.MarkUsed(position+1);
        QuantizedCpuMatrix.Row(file,embedding,id,s.X);
        profile?.Add(InferenceStage.Embedding,stage);
        stage=profile?.Start()??0;s.PrepareRope(position,frequencies,ropeAmplitude);profile?.Add(InferenceStage.Rope,stage);
        for(int index=0;index<layers.Length;index++)
        {
            s.Options.CancellationToken.ThrowIfCancellationRequested();var l=layers[index];
            stage=profile?.Start()??0;
            Norm(s.X,l.Norm,s.Normal,epsilon);
            profile?.Add(InferenceStage.Normalization,stage);
            Multiply(l.Q,s.Normal,s.Q,s.Options,selectedBackend,profile);
            Multiply(l.K,s.Normal,s.K,s.Options,selectedBackend,profile);
            Multiply(l.V,s.Normal,s.V,s.Options,selectedBackend,profile);
            stage=profile?.Start()??0;
            for(int h=0;h<heads;h++)Norm(s.Q.AsSpan(h*headSize,headSize),l.QNorm,s.Q.AsSpan(h*headSize,headSize),epsilon);
            for(int h=0;h<kvHeads;h++)Norm(s.K.AsSpan(h*headSize,headSize),l.KNorm,s.K.AsSpan(h*headSize,headSize),epsilon);
            profile?.Add(InferenceStage.Normalization,stage);
            stage=profile?.Start()??0;
            s.Rotate(s.Q,heads,headSize);s.Rotate(s.K,kvHeads,headSize);
            profile?.Add(InferenceStage.Rope,stage);
            stage=profile?.Start()??0;
            var keys=s.Keys[index];var values=s.Values[index];
            s.K.CopyTo(keys,position*kvSize);s.V.CopyTo(values,position*kvSize);
            Parallel.For(0,heads,s.Options,h=>{
                var scores=s.Scores[h];int kv=h/(heads/kvHeads)*headSize;float max=float.NegativeInfinity;
                var q=s.Q.AsSpan(h*headSize,headSize);
                for(int t=0;t<=position;t++){float v=Dot(q,keys.AsSpan(t*kvSize+kv,headSize))/MathF.Sqrt(headSize);scores[t]=v;max=Math.Max(max,v);}
                float sum=0;for(int t=0;t<=position;t++){scores[t]=MathF.Exp(scores[t]-max);sum+=scores[t];}
                var target=s.Attention.AsSpan(h*headSize,headSize);target.Clear();
                for(int t=0;t<=position;t++){float p=scores[t]/sum;var v=values.AsSpan(t*kvSize+kv,headSize);for(int d=0;d<headSize;d++)target[d]+=p*v[d];}
            });
            profile?.Add(InferenceStage.Attention,stage);
            Multiply(l.O,s.Attention,s.Projected,s.Options,selectedBackend,profile);
            stage=profile?.Start()??0;
            for(int i=0;i<width;i++)s.X[i]+=s.Projected[i];
            profile?.Add(InferenceStage.Residual,stage);
            stage=profile?.Start()??0;
            Norm(s.X,l.FfnNorm,s.Normal,epsilon);
            profile?.Add(InferenceStage.Normalization,stage);
            Multiply(l.Gate,s.Normal,s.Gate,s.Options,selectedBackend,profile);
            Multiply(l.Up,s.Normal,s.Up,s.Options,selectedBackend,profile);
            stage=profile?.Start()??0;
            for(int i=0;i<hidden;i++)s.Gate[i]=s.Gate[i]/(1+MathF.Exp(-s.Gate[i]))*s.Up[i];
            profile?.Add(InferenceStage.SwiGLU,stage);
            Multiply(l.Down,s.Gate,s.Projected,s.Options,selectedBackend,profile);
            stage=profile?.Start()??0;
            for(int i=0;i<width;i++)s.X[i]+=s.Projected[i];
            profile?.Add(InferenceStage.Residual,stage);
        }
        stage=profile?.Start()??0;
        Norm(s.X,finalNorm,s.Normal,epsilon);
        profile?.Add(InferenceStage.Normalization,stage);
    }
    private Qwen3Workspace AcquireWorkspace(CancellationToken token)
        =>(workspace??=new(width,hidden,querySize,kvSize,heads,headSize,file.Tokens.Length,layers.Length,context)).Acquire(token,threads);
    private static float Dot(ReadOnlySpan<float> a,ReadOnlySpan<float> b){float sum=0;for(int i=0;i<a.Length;i++)sum+=a[i]*b[i];return sum;}
    private static void Norm(ReadOnlySpan<float> x,ReadOnlySpan<float> weights,Span<float> y,float eps)
    {float sum=Dot(x,x);float scale=1/MathF.Sqrt(sum/x.Length+eps);for(int i=0;i<x.Length;i++)y[i]=x[i]*scale*weights[i];}
    /// <summary>Dispose only after active requests have stopped.</summary>
    public void Dispose(){workspace?.Dispose();workspace=null;if(prefixCache is {} c)foreach(var a in c.Keys.Concat(c.Values))Array.Clear(a);prefixCache=null;try{gpu?.Dispose();}finally{file.Dispose();gate.Dispose();}}
}
/// <summary>Measured result of a completed CPU generation.</summary>
public sealed record CpuGeneration(string Text,int InputTokens,int OutputTokens,string FinishReason,long ElapsedMilliseconds,string CpuBackend)
{
    /// <summary>Actual activation precision, retained even when stage profiling is disabled for replay diagnostics.</summary>
    public string ActivationPrecision {get;init;}="fp32";
    /// <summary>Optional stage diagnostics enabled at model construction by AIKERNEL_INFERENCE_PROFILE=1.</summary>
    public InferenceProfile? Profile {get;init;}
    /// <summary>Benchmark-only SHA-256 of little-endian Int32 generated token IDs, including EOS. Text is empty in benchmark mode.</summary>
    public string? GeneratedTokenSha256 {get;init;}
    /// <summary>Prompt forward passes excluding already cached system-prefix work.</summary>
    public double PrefillMilliseconds {get;init;}
    /// <summary>Output projections and generated-token forward passes.</summary>
    public double DecodeMilliseconds {get;init;}
    /// <summary>Number of prompt tokens forwarded during this call.</summary>
    public int PrefillForwardTokens {get;init;}
    /// <summary>Generated tokens forwarded for subsequent predictions, excluding the final unforwarded token.</summary>
    public int DecodeForwardTokens {get;init;}
    /// <summary>Worker ceiling used during prompt processing.</summary>
    public int PrefillThreads {get;init;}
    /// <summary>Worker ceiling used during token generation.</summary>
    public int DecodeThreads {get;init;}
}

internal static class GgufExtensions
{
    internal static bool ContainsOutput(this GgufWeights file)=>file.Tensors.ContainsKey("output.weight");
}
