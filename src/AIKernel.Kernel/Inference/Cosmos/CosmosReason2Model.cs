using System.Collections.Immutable;
using System.Diagnostics;

namespace AIKernel.Kernel.Inference;

/// <summary>A Cosmos Reason2 reply generated locally from text, RGB images and timestamped video frames.</summary>
public sealed record CosmosGeneration(string Text,ImmutableArray<int> TokenIds,int InputTokens,string FinishReason,long ElapsedMilliseconds);

/// <summary>Managed CPU Qwen3-VL inference for Cosmos-Reason2-2B safetensors. No Python, LibTorch or remote API is used.
/// Caller-decoded RGB inputs make resizing and video sampling explicit. This initial path does not implement GPU offload.</summary>
public sealed class CosmosReason2Model : IDisposable
{
    private readonly SafeTensorWeights weights;
    private readonly CosmosModelDefinition definition;
    private readonly CosmosTokenizer tokenizer;
    private readonly CosmosVisionEncoder vision;
    private readonly CosmosTextDecoder decoder;
    private readonly CosmosExecutionOptions options;
    private readonly SemaphoreSlim gate=new(1,1);
    private bool disposed;

    /// <summary>Loads local model, tokenizer, processor and chat-template metadata plus safetensors weights.</summary>
    public CosmosReason2Model(string modelDirectory,CosmosExecutionOptions? execution=null)
    {
        options=execution??new();options.Validate();definition=CosmosModelDefinition.Read(Path.Combine(modelDirectory,"config.json"));
        CosmosModelDefinition.ValidateProcessor(modelDirectory);
        if(options.ContextTokens>definition.MaximumContext)throw new ArgumentException("Requested context exceeds the model definition.");
        if(definition.Width!=2048 || definition.Layers!=28 || definition.VisionWidth!=1024 || definition.VisionLayers!=24)
            throw new NotSupportedException("This public entry point requires the Cosmos Reason2 2B architecture.");
        tokenizer=new(Path.Combine(modelDirectory,"tokenizer.json"),definition.Vocabulary);
        foreach(var (name,id) in new[]{("<|image_pad|>",definition.ImageToken),("<|video_pad|>",definition.VideoToken),
            ("<|vision_start|>",definition.VisionStart),("<|vision_end|>",definition.VisionEnd),("<|im_end|>",definition.EndToken)})
            if(tokenizer.Special(name)!=id)throw new InvalidDataException("Tokenizer/config control ID mismatch.");
        weights=new(modelDirectory);
        try{vision=new(weights,definition);decoder=new(weights,definition);}
        catch{weights.Dispose();throw;}
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
        using var prompt=CosmosPrompt.Build(tokenizer,definition,system,content,options);
        if(prompt.Tokens.Count+maxNewTokens>options.ContextTokens)throw new ArgumentException("Prompt plus generation exceeds the context budget.");
        using var arena=new CosmosWorkspace();using var state=new CosmosTextDecoder.State(definition,options.ContextTokens,options.CpuThreads,token);
        var embeddings=new List<float[]>();var deep=Enumerable.Range(0,definition.DeepStack.Length).Select(_=>new List<float[]>()).ToArray();
        var watch=Stopwatch.StartNew();
        foreach(var media in prompt.Media)
        {
            var features=vision.Forward(media,arena,state.Options);embeddings.AddRange(features.Embeddings);
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
        return new(tokenizer.Decode(generated),generated.ToImmutable(),prompt.Tokens.Count,finish,watch.ElapsedMilliseconds);
    }
    /// <summary>Waits for an active generation, then releases the read-only weight mappings.</summary>
    public void Dispose()
    {
        gate.Wait();try{if(disposed)return;disposed=true;weights.Dispose();}finally{gate.Release();}
    }
}
