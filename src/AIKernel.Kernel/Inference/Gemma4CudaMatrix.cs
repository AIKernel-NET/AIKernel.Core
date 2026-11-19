using AIKernel.Kernel.Memory;

namespace AIKernel.Kernel.Inference;

/// <summary>Retains only GEMV tensors in VRAM. The 1.93GB PLE lookup table stays mapped on the CPU.</summary>
internal sealed unsafe class Gemma4CudaMatrix : IDisposable
{
    private readonly CudaVirtualMemoryDevice device;
    private readonly Dictionary<long,ulong> addresses=new();
    internal long WeightBytes{get;private set;}
    internal Gemma4CudaMatrix(GgufWeights file,IEnumerable<GgufWeights.Tensor> tensors,int ordinal,long budget)
    {
        var distinct=tensors.DistinctBy(t=>t.Offset).ToArray();
        WeightBytes=distinct.Sum(t=>t.ByteLength);
        if(budget<1||WeightBytes>budget)throw new ArgumentOutOfRangeException(nameof(budget),"Gemma matrices exceed the VRAM weight budget.");
        if(distinct.Any(t=>t.Type is not (1 or 2 or 14)))throw new NotSupportedException("Gemma CUDA supports F16/Q4_0/Q6_K matrices.");
        device=new(ordinal);
        try
        {
            foreach(var tensor in distinct)
            {
                NativeCompute.Check(NativeCompute.DeviceAllocate(device.Context,(nuint)tensor.ByteLength,out ulong address),"Gemma weight allocation");
                addresses.Add(tensor.Offset,address);
                for(long offset=0;offset<tensor.ByteLength;offset+=1024*1024)
                {
                    int count=(int)Math.Min(1024*1024,tensor.ByteLength-offset);
                    NativeCompute.Check(NativeCompute.Upload(device.Context,address+(ulong)offset,file.BytesAt(tensor.Offset+offset,count),(nuint)count),"Gemma encoded weight upload");
                }
            }
        }
        catch{device.Dispose();throw;}
    }
    internal void Multiply(GgufWeights.Tensor tensor,float[] x,float[] y,CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if(x.Length<tensor.Columns||y.Length<tensor.Rows)throw new ArgumentException("Matrix dimensions mismatch.");
        fixed(float* input=x,output=y)
            NativeCompute.Check(NativeCompute.GemvGemma(device.Context,addresses[tensor.Offset],input,output,(uint)tensor.Rows,(uint)tensor.Columns,(uint)tensor.Type),"Gemma CUDA GEMV");
    }
    internal void ZeroScratch()=>device.ZeroScratch();
    public void Dispose()=>device.Dispose();
}
