namespace AIKernel.Kernel.Inference;

// One bounded workspace per model, leased only while its admission gate is held.
// No request contents survive Release, including partially completed forwards.
internal sealed class Qwen3Workspace : IDisposable
{
    internal readonly float[] X,Normal,Q,K,V,Attention,Projected,Gate,Up,Logits,RopeCos,RopeSin;
    internal readonly float[][] Keys,Values,Scores;
    internal readonly ParallelOptions Options=new();
    private readonly float[][] scratch;
    private readonly int kvSize,context;
    internal int UsedTokens {get;private set;}
    internal bool InUse {get;private set;}

    internal Qwen3Workspace(int width,int hidden,int querySize,int kvSize,int heads,int headSize,int vocabulary,int layers,int context)
    {
        this.kvSize=kvSize;this.context=context;
        X=new float[width];Normal=new float[width];Q=new float[querySize];K=new float[kvSize];V=new float[kvSize];
        Attention=new float[querySize];Projected=new float[width];Gate=new float[hidden];Up=new float[hidden];Logits=new float[vocabulary];
        RopeCos=new float[headSize/2];RopeSin=new float[headSize/2];
        scratch=[X,Normal,Q,K,V,Attention,Projected,Gate,Up,Logits,RopeCos,RopeSin];
        Keys=Enumerable.Range(0,layers).Select(_=>new float[context*kvSize]).ToArray();
        Values=Enumerable.Range(0,layers).Select(_=>new float[context*kvSize]).ToArray();
        Scores=Enumerable.Range(0,heads).Select(_=>new float[context]).ToArray();
    }
    internal Qwen3Workspace Acquire(CancellationToken token,int threads)
    {
        if(InUse)throw new InvalidOperationException("Inference workspace is already leased.");
        InUse=true;Options.CancellationToken=token;Options.MaxDegreeOfParallelism=threads;return this;
    }
    internal void MarkUsed(int tokens)
    {
        if(!InUse||tokens<0||tokens>context)throw new InvalidOperationException("Invalid inference workspace range.");
        UsedTokens=Math.Max(UsedTokens,tokens);
    }
    internal void PrepareRope(int position,float[] frequencies,float amplitude)
    {
        for(int i=0;i<frequencies.Length;i++)
        {
            float angle=position*frequencies[i];
            RopeCos[i]=MathF.Cos(angle)*amplitude;RopeSin[i]=MathF.Sin(angle)*amplitude;
        }
    }
    internal void Rotate(float[] vector,int heads,int headSize)
    {
        int half=headSize/2;
        for(int h=0;h<heads;h++)for(int i=0;i<half;i++)
        {
            float c=RopeCos[i],s=RopeSin[i];
            int a=h*headSize+i,b=a+half;float x=vector[a],y=vector[b];vector[a]=x*c-y*s;vector[b]=x*s+y*c;
        }
    }
    public void Dispose()
    {
        foreach(var array in scratch)Array.Clear(array);
        int elements=UsedTokens*kvSize;
        foreach(var array in Keys)array.AsSpan(0,elements).Clear();
        foreach(var array in Values)array.AsSpan(0,elements).Clear();
        foreach(var array in Scores)array.AsSpan(0,UsedTokens).Clear();
        UsedTokens=0;Options.CancellationToken=default;InUse=false;
    }
}
