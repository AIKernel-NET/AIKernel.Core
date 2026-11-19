using System.Collections.Immutable;
using System.Diagnostics;

namespace AIKernel.Kernel.Inference;

/// <summary>A Cosmos reply generated locally from text, RGB images and timestamped video frames.</summary>
public sealed record CosmosGeneration(string Text,ImmutableArray<int> TokenIds,int InputTokens,string FinishReason,long ElapsedMilliseconds)
{
    /// <summary>Resource/dependency decision made before inference, when paging is enabled.</summary>
    public CosmosMemoryPlan? MemoryPlan { get; init; }
    /// <summary>Verified checkpoint provenance, when paging is enabled.</summary>
    public CosmosArtifactIdentity? Artifact { get; init; }
}

/// <summary>Managed CPU Qwen3-VL inference for Cosmos-Reason2-2B safetensors. No Python, LibTorch or remote API is used.
/// Caller-decoded RGB inputs make resizing and video sampling explicit. This initial path does not implement GPU offload.</summary>
public sealed class CosmosReason2Model : IDisposable
{
    private readonly CosmosModelRuntime runtime;
    /// <summary>Loads the original Reason2 2B preview checkpoint.</summary>
    public CosmosReason2Model(string modelDirectory,CosmosExecutionOptions? execution=null)=>runtime=new(modelDirectory,execution,"qwen3_vl");
    /// <summary>Generates a greedy reply from local text, images and video frames.</summary>
    public Task<CosmosGeneration> GenerateAsync(string system,IReadOnlyList<CosmosContent> content,int maxNewTokens=128,CancellationToken cancellationToken=default)
        =>runtime.GenerateAsync(system,content,maxNewTokens,cancellationToken);
    /// <summary>Releases weight mappings after active requests finish.</summary>
    public void Dispose()=>runtime.Dispose();
}

internal sealed class CosmosModelRuntime : IDisposable
{
    private readonly SafeTensorWeights weights;
    private readonly CosmosMemoryBudget? memory;
    internal CosmosMemoryStatistics? MemoryStatistics=>memory?.Statistics;
    private readonly CosmosModelDefinition definition;
    private readonly CosmosTokenizer tokenizer;
    private Func<CosmosPatches,CosmosWorkspace,ParallelOptions,CosmosVisionEncoder.Features>? vision;
    private readonly CosmosArtifactIdentity? identity;
    private readonly CosmosTextDecoder decoder;
    private readonly CosmosExecutionOptions options;
    private readonly SemaphoreSlim gate=new(1,1);
    private bool disposed;

    /// <summary>Loads local model, tokenizer, processor and chat-template metadata plus safetensors weights.</summary>
    internal CosmosModelRuntime(string modelDirectory,CosmosExecutionOptions? execution,string requiredFamily)
    {
        options=execution??new();options.Validate();definition=CosmosModelDefinition.Read(Path.Combine(modelDirectory,"config.json"));
        CosmosModelDefinition.ValidateProcessor(modelDirectory,definition);
        if(options.ContextTokens>definition.MaximumContext)throw new ArgumentException("Requested context exceeds the model definition.");
        if(definition.Family!=requiredFamily)throw new NotSupportedException("Checkpoint does not match the requested Cosmos family.");
        if(requiredFamily=="qwen3_vl" && (definition.Width!=2048 || definition.Layers!=28 || definition.VisionWidth!=1024 || definition.VisionLayers!=24))
            throw new NotSupportedException("This entry point requires the Cosmos Reason2 2B architecture.");
        if(options.Memory is not null){options.Memory.Validate();identity=CosmosArtifactVerifier.Verify(modelDirectory,requiredFamily,options.Memory.ExpectedImportManifestSha256);}
        tokenizer=new(Path.Combine(modelDirectory,"tokenizer.json"),definition.Vocabulary);
        foreach(var (name,id) in new[]{("<|image_pad|>",definition.ImageToken),("<|video_pad|>",definition.VideoToken),
            ("<|vision_start|>",definition.VisionStart),("<|vision_end|>",definition.VisionEnd),("<|im_end|>",definition.EndToken)})
            if(tokenizer.Special(name)!=id)throw new InvalidDataException("Tokenizer/config control ID mismatch.");
        memory=options.Memory is null?null:new(options.Memory);
        try
        {
            weights=new(modelDirectory,memory);
            decoder=new(weights,definition);
        }
        catch{try{weights?.Dispose();}finally{memory?.Dispose();}throw;}
    }

    /// <summary>Generates a greedy reply. In-flight requests serialize; cancellation clears private pixel features, KV and scratch state.</summary>
    public async Task<CosmosGeneration> GenerateAsync(string system,IReadOnlyList<CosmosContent> content,int maxNewTokens=128,CancellationToken cancellationToken=default)
    {
        ArgumentNullException.ThrowIfNull(system);ArgumentNullException.ThrowIfNull(content);
        if(maxNewTokens<1 || maxNewTokens>options.ContextTokens)throw new ArgumentOutOfRangeException(nameof(maxNewTokens));
        // Snapshot the content collection before awaiting. Caller must keep frame memory immutable for the request duration.
        var items=content.Select(item=>item is CosmosContent.Video video
            ? new CosmosContent.Video(video.Frames.ToArray()) : item).ToArray();
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(disposed,this);
            return await Task.Run(()=>Run(system,items,maxNewTokens,cancellationToken),cancellationToken).ConfigureAwait(false);
        }
        finally{gate.Release();}
    }
    private CosmosGeneration Run(string system,CosmosContent[] content,int maxNewTokens,CancellationToken token)
    {
        memory?.Check();
        // Reserve normalized patches before preparing them, plus metadata/input headroom.
        long mediaReserve=checked((long)options.MaximumVisionPatches*1536*sizeof(float)+16L*1024*1024);
        memory?.Reserve(mediaReserve);
        try
        {
        using var prompt=CosmosPrompt.Build(tokenizer,definition,system,content,options);
        if(prompt.Tokens.Count+maxNewTokens>options.ContextTokens)throw new ArgumentException("Prompt plus generation exceeds the context budget.");
        using var arena=new CosmosWorkspace(memory);using var state=new CosmosTextDecoder.State(definition,prompt.Tokens.Count+maxNewTokens,options.CpuThreads,token,memory);
        bool visual=prompt.Media.Count>0;
        var plan=memory is null?null:new CosmosMemoryPlan(visual?["text-decoder","vision-encoder","vision-projector"]:["text-decoder"],
            state.Context,weights.StoredBytes(visual),(long)definition.Layers*state.Context*definition.KvHeads*definition.HeadSize*8,
            memory.Options.MaximumResidentBytes,memory.Options.ProtectedResidentBytes,"immutable-source+encrypted-kv-swap");
        if(visual)vision??=definition.IsEdge?new CosmosEdgeVisionEncoder(weights,definition).Forward:new CosmosVisionEncoder(weights,definition).Forward;
        var embeddings=new List<float[]>();var deep=Enumerable.Range(0,definition.DeepStack.Length).Select(_=>new List<float[]>()).ToArray();
        var watch=Stopwatch.StartNew();
        foreach(var media in prompt.Media)
        {
            var features=vision!(media,arena,state.Options);embeddings.AddRange(features.Embeddings);
            for(int i=0;i<deep.Length;i++)deep[i].AddRange(features.DeepStack[i]);
        }
        for(int i=0;i<prompt.Tokens.Count;i++)
        {
            int image=prompt.VisualIndices[i];
            decoder.Forward(prompt.Tokens[i],i,prompt.Positions[i],state,image<0?null:embeddings[image],
                image<0?null:deep.Select(x=>x[image]).ToArray(),i==prompt.Tokens.Count-1);
        }
        var generated=ImmutableArray.CreateBuilder<int>();string finish="length";
        for(int n=0;n<maxNewTokens;n++)
        {
            token.ThrowIfCancellationRequested();int next=0;float best=float.NegativeInfinity;
            for(int i=0;i<state.Logits.Length;i++)
            {if(!float.IsFinite(state.Logits[i]))throw new ArithmeticException("Non-finite Cosmos logits.");if(state.Logits[i]>best){best=state.Logits[i];next=i;}}
            generated.Add(next);if(next==definition.EndToken){finish="stop";break;}
            if(n+1<maxNewTokens){int p=prompt.NextPosition+n;decoder.Forward(next,prompt.Tokens.Count+n,(p,p,p),state);}
        }
        return new(tokenizer.Decode(generated),generated.ToImmutable(),prompt.Tokens.Count,finish,watch.ElapsedMilliseconds){MemoryPlan=plan,Artifact=identity};
        }
        finally{memory?.Release(mediaReserve);}
    }
    /// <summary>Waits for an active generation, then releases the read-only weight mappings.</summary>
    public void Dispose()
    {
        gate.Wait();try{if(disposed)return;disposed=true;try{weights.Dispose();}finally{memory?.Dispose();}}finally{gate.Release();}
    }
}
