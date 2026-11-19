using System.Buffers;

namespace AIKernel.Kernel.Inference;

// A short-lived lease for one immutable input, shared by Q/K/V or Gate/Up only.
// Never cache by array identity across forwards: Normal is overwritten in-place.
internal sealed class PreparedQ8Activation : IDisposable
{
    internal sbyte[] Quantized {get;}=[];
    internal float[] Scales {get;}=[];
    internal int[] Sums {get;}=[];
    internal float[]? Rounded {get;}
    private float[]? source;
    private readonly int columns;
    private bool disposed;
    internal PreparedQ8Activation(float[] input,int columns,bool rounded,InferenceProfiler? profile=null,
        InferenceStage stage=InferenceStage.Quantization)
    {
        if(columns<1||columns%128!=0||input.Length<columns)throw new ArgumentException("Invalid Q8 input dimensions.");
        source=input;this.columns=columns;
        try
        {
            Quantized=ArrayPool<sbyte>.Shared.Rent(columns);
            Scales=ArrayPool<float>.Shared.Rent(columns/128);
            Sums=ArrayPool<int>.Shared.Rent(columns/128);
            if(rounded)Rounded=ArrayPool<float>.Shared.Rent(columns);
            long started=profile?.Start()??0;
            Int8CpuMatrix.Quantize(input.AsSpan(0,columns),Quantized,Scales,Sums);
            if(Rounded is {} values)for(int i=0;i<columns;i++)values[i]=Quantized[i]*Scales[i/128];
            profile?.Add(stage,started);
        }
        catch{Dispose();throw;}
    }
    internal void Validate(float[] input,int length,bool rounded=false)
    {
        ObjectDisposedException.ThrowIf(disposed,this);
        if(!ReferenceEquals(input,source)||length!=columns||rounded&&Rounded is null)
            throw new ArgumentException("Prepared Q8 input does not match this projection.");
    }
    public void Dispose()
    {
        if(disposed)return;disposed=true;source=null;
        if(Quantized.Length>0)ArrayPool<sbyte>.Shared.Return(Quantized,clearArray:true);
        if(Scales.Length>0)ArrayPool<float>.Shared.Return(Scales,clearArray:true);
        if(Sums.Length>0)ArrayPool<int>.Shared.Return(Sums,clearArray:true);
        if(Rounded is not null)ArrayPool<float>.Shared.Return(Rounded,clearArray:true);
    }
}
