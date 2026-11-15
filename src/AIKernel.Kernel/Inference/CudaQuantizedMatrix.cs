using AIKernel.Kernel.Memory;
using System.Buffers;
using System.Diagnostics;

namespace AIKernel.Kernel.Inference;

// Owns one private CUDA context. The caller serializes all requests and disposal.
internal sealed unsafe class CudaQuantizedMatrix : IMatrixOffload
{
    private readonly CudaVirtualMemoryDevice device;
    private readonly CudaVirtualMemoryDevice.DeviceBlock weights;
    private readonly CudaQ8Kernel? int8Kernel;
    private readonly uint int8Capabilities;
    private readonly Dictionary<(int Rows,int Columns),uint> selections=[];
    private long dp4aCalls,tensorCoreCalls;
    internal CudaQ8Statistics Statistics
    {
        get {lock(device.Gate)return new(dp4aCalls,tensorCoreCalls,selections.ToDictionary(p=>$"{p.Key.Rows}x{p.Key.Columns}",p=>p.Value==1?"dp4a":"tensor-core-int8"));}
    }
    public long WeightBytes => weights.Length;
    public string? PciBusId => device.GetPciBusId();

    internal CudaQuantizedMatrix(GgufWeights file, int ordinal, long maximumWeightBytes,CudaQ8Kernel? int8Kernel=null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(ordinal);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumWeightBytes);
        if (file.Length > maximumWeightBytes || file.Length > int.MaxValue)
            throw new ArgumentException("Encoded model exceeds the configured GPU weight budget.");
        device = new(ordinal);
        try
        {
            if(int8Kernel.HasValue)
            {
                if(!Enum.IsDefined(int8Kernel.Value))throw new ArgumentOutOfRangeException(nameof(int8Kernel));
                NativeCompute.Check(NativeCompute.GetInt8Capabilities(device.Context,out int8Capabilities),"GPU INT8 capability query");
                uint required=int8Kernel==CudaQ8Kernel.TensorCore?2u:1u;
                if((int8Capabilities&required)==0)throw new PlatformNotSupportedException($"Requested CUDA Q8 kernel {int8Kernel} is unavailable on this device.");
            }
            this.int8Kernel=int8Kernel;
            weights = new(device, checked((int)file.Length));
            NativeCompute.Check(NativeCompute.DeviceAllocate(device.Context, (nuint)file.Length, out var address), "GPU model allocation");
            weights.Address = address;
            // Upload in bounded chunks: native staging never mirrors the entire model.
            const int chunkBytes = 1024 * 1024;
            for (long offset = 0; offset < file.Length; offset += chunkBytes)
            {
                var count = (int)Math.Min(chunkBytes, file.Length - offset);
                NativeCompute.Check(NativeCompute.Upload(device.Context, address + (ulong)offset,
                    file.BytesAt(offset, count), (nuint)count), "GPU model upload");
            }
            device.ZeroScratch();
        }
        catch { device.Dispose(); throw; }
    }

    public void Multiply(GgufWeights.Tensor tensor, float[] input, float[] output, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        long bytes = checked((long)tensor.Rows * tensor.RowBytes);
        if (tensor.Rows < 1 || tensor.Columns < 1 || input.Length < tensor.Columns || output.Length < tensor.Rows ||
            tensor.Offset < 0 || tensor.Offset > WeightBytes || bytes > WeightBytes - tensor.Offset)
            throw new ArgumentException("GPU matrix dimensions or mapped range are invalid.");
        lock (device.Gate)
        {
            ObjectDisposedException.ThrowIf(weights.Address == 0, this);
            if(int8Kernel.HasValue&&tensor.Type==41)
            {
                MultiplyInt8(tensor,input,output,token);
                token.ThrowIfCancellationRequested();return;
            }
            fixed (float* x = input)
            fixed (float* y = output)
            {
                ulong address = weights.Address + (ulong)tensor.Offset;
                int status = tensor.Type switch
                {
                    0 => NativeCompute.Gemv(device.Context, address, x, y, (uint)tensor.Rows, (uint)tensor.Columns),
                    41 => NativeCompute.GemvQ1(device.Context, address, x, y, (uint)tensor.Rows, (uint)tensor.Columns),
                    _ => throw new NotSupportedException("GPU matrix type is unsupported.")
                };
                NativeCompute.Check(status, "GPU matrix multiplication");
            }
        }
        token.ThrowIfCancellationRequested();
    }

    public void ZeroScratch() => device.ZeroScratch();
    private void MultiplyInt8(GgufWeights.Tensor tensor,float[] input,float[] output,CancellationToken token)
    {
        int columns=tensor.Columns,rows=tensor.Rows;
        if(columns%128!=0)throw new ArgumentException("Q8 matrices require 128-element blocks.");
        var q=ArrayPool<sbyte>.Shared.Rent(columns);var scales=ArrayPool<float>.Shared.Rent(columns/128);var sums=ArrayPool<int>.Shared.Rent(columns/128);
        try
        {
            Int8CpuMatrix.Quantize(input.AsSpan(0,columns),q,scales,sums);
            fixed(sbyte* qp=q)fixed(float* sp=scales)fixed(int* ip=sums)fixed(float* yp=output)
            {
                ulong address=weights.Address+(ulong)tensor.Offset;
                uint selected=int8Kernel==CudaQ8Kernel.TensorCore?2u:1u;
                if(int8Kernel==CudaQ8Kernel.Auto&&(int8Capabilities&2)!=0)
                {
                    if(!selections.TryGetValue((rows,columns),out selected))
                    {
                        // The request gate is held. Only aggregate timings and the shape are cached.
                        var comparison=ArrayPool<float>.Shared.Rent(rows);
                        try
                        {
                            Call(1,address,qp,sp,ip,yp,rows,columns,token);output.AsSpan(0,rows).CopyTo(comparison);
                            Call(2,address,qp,sp,ip,yp,rows,columns,token);
                            for(int i=0;i<rows;i++)if(!float.IsFinite(output[i])||Math.Abs(output[i]-comparison[i])>1e-5*Math.Max(1,Math.Abs(comparison[i])))
                                throw new ArithmeticException("CUDA integer kernels disagreed during calibration.");
                            long[][] timings=[new long[3],new long[3]];
                            for(int pass=0;pass<3;pass++)for(int order=0;order<2;order++)
                            {
                                uint candidate=(uint)((order+pass)%2+1);long start=Stopwatch.GetTimestamp();
                                Call(candidate,address,qp,sp,ip,yp,rows,columns,token);timings[candidate-1][pass]=Stopwatch.GetTimestamp()-start;
                            }
                            Array.Sort(timings[0]);Array.Sort(timings[1]);selected=timings[1][1]<timings[0][1]*.95?2u:1u;
                            selections.Add((rows,columns),selected);
                        }
                        finally{ArrayPool<float>.Shared.Return(comparison,clearArray:true);}
                    }
                }
                else selections.TryAdd((rows,columns),selected);
                Call(selected,address,qp,sp,ip,yp,rows,columns,token);
            }
        }
        finally{ArrayPool<sbyte>.Shared.Return(q,clearArray:true);ArrayPool<float>.Shared.Return(scales,clearArray:true);ArrayPool<int>.Shared.Return(sums,clearArray:true);}
    }
    private void Call(uint selected,ulong address,sbyte* q,float* scales,int* sums,float* y,int rows,int columns,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        NativeCompute.Check(NativeCompute.GemvQ8(device.Context,address,q,scales,sums,y,(uint)rows,(uint)columns,selected),"GPU native INT8 matrix multiplication");
        if(selected==1)dp4aCalls++;else tensorCoreCalls++;
    }
    public void Dispose()
    {
        try { weights.Dispose(); }
        finally { device.Dispose(); }
    }
}
