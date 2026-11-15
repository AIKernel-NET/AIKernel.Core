using System.Diagnostics;

namespace AIKernel.Kernel.Inference;

/// <summary>Wall-clock aggregate for one inference stage, not the sum of worker CPU times.</summary>
public sealed record InferenceStageProfile(long Calls,double Milliseconds);

/// <summary>Opt-in diagnostics. Matrix includes quantization and offload costs; nested stages must not be added twice.
/// ProcessAllocatedBytes is a process-wide GC allocation delta, not retained memory or exclusively this request.
/// RequestMilliseconds includes managed state cleanup but excludes GPU scratch cleanup.</summary>
public sealed record InferenceProfile(
    IReadOnlyDictionary<string,InferenceStageProfile> Stages,double RequestMilliseconds,long ProcessAllocatedBytes,
    int Width,int HiddenSize,int Layers,int Heads,int KvHeads,int HeadSize,int ContextTokens,
    string CpuBackend,string ActivationPrecision)
{
    /// <summary>Optional event-instrumented GEMV timeline. Host sync overlaps stream intervals; never sum them.</summary>
    public CudaMatrixProfile? CudaMatrix {get;init;}
}

/// <summary>CUDA event intervals include stream scheduling gaps. Not pure arithmetic time or an uninstrumented benchmark.</summary>
public sealed record CudaMatrixProfile(ulong Calls,ulong UploadBytes,ulong DownloadBytes,
    double UploadMilliseconds,double KernelIntervalMilliseconds,double DownloadMilliseconds,double HostSyncMilliseconds);

internal enum InferenceStage { Tokenization,StateAllocation,PrefixCopy,Embedding,Matrix,Quantization,Normalization,Rope,Attention,SwiGLU,Residual,Argmax,StateClear }
internal enum InferencePhase { Request,Prefill,Decode }

// One request owns this object under the model admission gate. Worker loops do not record timestamps.
internal sealed class InferenceProfiler
{
    private static readonly int StageCount=Enum.GetValues<InferenceStage>().Length;
    private readonly long[] ticks=new long[StageCount*3];
    private readonly long[] calls=new long[StageCount*3];
    internal InferencePhase Phase {get;set;}
    private readonly long started=Stopwatch.GetTimestamp();
    private readonly long allocated=GC.GetTotalAllocatedBytes(false);
    internal long Start()=>Stopwatch.GetTimestamp();
    internal void Add(InferenceStage stage,long start)
    {int index=(int)Phase*StageCount+(int)stage;ticks[index]+=Stopwatch.GetTimestamp()-start;calls[index]++;}
    internal InferenceProfile Snapshot(int width,int hidden,int layers,int heads,int kvHeads,int headSize,int context,string backend,string precision)
    {
        var values=new Dictionary<string,InferenceStageProfile>();
        foreach(var phase in Enum.GetValues<InferencePhase>())foreach(var stage in Enum.GetValues<InferenceStage>())
        {
            int index=(int)phase*StageCount+(int)stage;
            if(calls[index]>0)values.Add($"{phase}.{stage}",new(calls[index],ticks[index]*1000.0/Stopwatch.Frequency));
        }
        return new(values,(Stopwatch.GetTimestamp()-started)*1000.0/Stopwatch.Frequency,GC.GetTotalAllocatedBytes(false)-allocated,width,hidden,layers,heads,kvHeads,headSize,context,backend,precision);
    }
}
