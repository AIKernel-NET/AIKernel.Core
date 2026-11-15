using System.Buffers;

namespace AIKernel.Kernel.Inference;

// Common Q8 block128 rounding for FP32 CPU/CUDA/Vulkan accumulators. This is
// activation quantization, not a claim that GPU INT8 or Tensor Cores are used.
internal sealed class Q8ActivationBuffer : IDisposable
{
    internal float[] Values { get; }
    internal Q8ActivationBuffer(float[] input,int columns,InferenceProfiler? profile=null)
    {
        var q=ArrayPool<sbyte>.Shared.Rent(columns);
        var scales=ArrayPool<float>.Shared.Rent(columns/128);
        var sums=ArrayPool<int>.Shared.Rent(columns/128);
        Values=ArrayPool<float>.Shared.Rent(columns);
        try
        {
            long started=profile?.Start()??0;
            Int8CpuMatrix.Quantize(input.AsSpan(0,columns),q,scales,sums);
            for(int i=0;i<columns;i++)Values[i]=q[i]*scales[i/128];
            profile?.Add(InferenceStage.Quantization,started);
        }
        catch{ArrayPool<float>.Shared.Return(Values,clearArray:true);throw;}
        finally{ArrayPool<sbyte>.Shared.Return(q,clearArray:true);ArrayPool<float>.Shared.Return(scales,clearArray:true);ArrayPool<int>.Shared.Return(sums,clearArray:true);}
    }
    public void Dispose()=>ArrayPool<float>.Shared.Return(Values,clearArray:true);
}
