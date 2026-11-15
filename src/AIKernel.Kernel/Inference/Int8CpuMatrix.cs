using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace AIKernel.Kernel.Inference;

/// <summary>Optional Q1_0 weight / Q8 activation kernels. Quantizing activations changes arithmetic precision.</summary>
public static class CpuInt8Acceleration
{
    /// <summary>Usable explicit backends. AMX is hardware/OS-gated but requires permission again on the executing thread.</summary>
    public static IReadOnlyList<string> AvailableBackends=>Int8CpuMatrix.Candidates().AsReadOnly();
}

internal static unsafe class Int8CpuMatrix
{
    private const string Library="aikernel_cpu_accel";
    private static readonly Lazy<uint> Capabilities=new(()=>
    {
        try{return Abi()==1?Supports():0;}
        catch(Exception e) when(e is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException){return 0;}
    });
    [DllImport(Library,EntryPoint="ak_cpu_abi",CallingConvention=CallingConvention.Cdecl)] private static extern uint Abi();
    [DllImport(Library,EntryPoint="ak_cpu_supports",CallingConvention=CallingConvention.Cdecl)] private static extern uint Supports();
    [DllImport(Library,EntryPoint="ak_cpu_q1_q8",CallingConvention=CallingConvention.Cdecl)]
    private static extern int NativeMultiply(byte* weights,nuint weightBytes,sbyte* x,float* scales,int* sums,uint rows,uint columns,uint backend,float* y);
    internal static bool IsBackend(string backend)=>backend is "scalar-q8" or "avx2-int8-q8" or "avx-vnni-q8" or "avx512-vnni-q8" or "amx-int8-q8";
    internal static bool IsRoundedFloatBackend(string backend)=>backend is "avx-q8" or "avx2-q8" or "avx512-q8";
    internal static List<string> Candidates(bool includeAmx=true)
    {
        var list=new List<string>{"scalar-q8"};uint flags=Capabilities.Value;
        if(System.Runtime.Intrinsics.X86.Avx.IsSupported)list.Add("avx-q8");
        if(System.Runtime.Intrinsics.X86.Avx2.IsSupported&&System.Runtime.Intrinsics.X86.Fma.IsSupported)list.Add("avx2-q8");
        if(System.Runtime.Intrinsics.X86.Avx512F.IsSupported)list.Add("avx512-q8");
        if((flags&8)!=0)list.Add("avx2-int8-q8");
        if((flags&1)!=0)list.Add("avx-vnni-q8");
        if((flags&2)!=0)list.Add("avx512-vnni-q8");
        if(includeAmx&&(flags&4)!=0)list.Add("amx-int8-q8");
        return list;
    }
    // AMX stays explicitly selected until independently validated on an AMX machine.
    internal static string Resolve(string requested)
    {
        if(requested=="q8-auto")return Candidates(includeAmx:false)[^1];
        if((IsBackend(requested)||IsRoundedFloatBackend(requested))&&Candidates().Contains(requested))return requested;
        throw new PlatformNotSupportedException("Requested Q8 kernel needs a compatible native library, CPU and OS.");
    }
    internal static void Multiply(GgufWeights file,GgufWeights.Tensor tensor,float[] x,float[] y,ParallelOptions options,string backend,InferenceProfiler? profile=null,PreparedQ8Activation? prepared=null)
    {
        if(Resolve(backend)!=backend)throw new ArgumentException("Resolve the Q8 backend before multiplication.");
        if(tensor.Type!=41||tensor.Columns%128!=0||x.Length<tensor.Columns||y.Length<tensor.Rows)
            throw new ArgumentException("Q8 kernel requires a dimensionally valid Q1_0 matrix.");
        options.CancellationToken.ThrowIfCancellationRequested();
        using var owned=prepared is null?new PreparedQ8Activation(x,tensor.Columns,false,profile):null;
        var activation=prepared??owned!;activation.Validate(x,tensor.Columns);
        var q=activation.Quantized;var scales=activation.Scales;var sums=activation.Sums;
        {
            int workers=Math.Min(options.MaxDegreeOfParallelism,Math.Max(1,tensor.Rows/64));
            nint weights=(nint)file.Pointer(tensor);
            fixed(sbyte* xp=q)fixed(float* sp=scales)fixed(int* ip=sums)fixed(float* yp=y)
            {
                nint input=(nint)xp,scale=(nint)sp,sum=(nint)ip,destination=(nint)yp;
                void Work(int worker)
                {
                    int first=worker==0?0:QuantizedCpuMatrix.RowBoundary(tensor.Rows*worker/workers,tensor.Rows,(nuint)destination);
                    int last=worker+1==workers?tensor.Rows:QuantizedCpuMatrix.RowBoundary(tensor.Rows*(worker+1)/workers,tensor.Rows,(nuint)destination);
                    // Bound cancellation latency without calling native code for every row.
                    for(int row=first;row<last;row+=64)
                    {
                        options.CancellationToken.ThrowIfCancellationRequested();
                        int count=Math.Min(64,last-row);byte* w=(byte*)weights+(long)row*tensor.RowBytes;
                        float* output=(float*)destination+row;
                        if(backend=="scalar-q8")Scalar(w,(sbyte*)input,(float*)scale,count,tensor.Columns,output);
                        else
                        {
                            uint kind=backend=="avx-vnni-q8"?1u:backend=="avx512-vnni-q8"?2u:backend=="avx2-int8-q8"?4u:3u;
                            int status=NativeMultiply(w,(nuint)((long)count*tensor.RowBytes),(sbyte*)input,(float*)scale,(int*)sum,
                                (uint)count,(uint)tensor.Columns,kind,output);
                            if(status!=0)throw new PlatformNotSupportedException($"CPU Q8 kernel {backend} failed ({status}); no partial result is returned.");
                        }
                    }
                }
                if(workers==1)Work(0);else Parallel.For(0,workers,options,Work);
            }
        }
    }
    internal static void Quantize(ReadOnlySpan<float> x,Span<sbyte> q,Span<float> scales,Span<int> sums)
    {
        if(x.Length%128!=0||q.Length<x.Length||scales.Length<x.Length/128||sums.Length<x.Length/128)throw new ArgumentException("Invalid Q8 buffers.");
        for(int block=0;block<x.Length/128;block++)
        {
            var values=x.Slice(block*128,128);float maximum=0;
            foreach(float value in values){if(!float.IsFinite(value))throw new ArgumentException("Nonfinite activation.");maximum=Math.Max(maximum,Math.Abs(value));}
            float scale=maximum/127;
            if(scale==0&&maximum!=0)scale=maximum; // Preserve finite subnormals without division by zero.
            scales[block]=scale;int sum=0;
            for(int j=0;j<128;j++)
            {
                int value=scale==0?0:(int)Math.Clamp(MathF.Round(values[j]/scale,MidpointRounding.ToEven),-127,127);
                q[block*128+j]=(sbyte)value;sum+=value;
            }
            sums[block]=sum;
        }
    }
    private static void Scalar(byte* w,sbyte* x,float* scales,int rows,int columns,float* y)
    {
        int blocks=columns/128;
        for(int row=0;row<rows;row++)
        {
            float total=0;
            for(int block=0;block<blocks;block++)
            {
                byte* p=w+((long)row*blocks+block)*18;int dot=0;
                for(int k=0;k<128;k++)dot+=((p[2+k/8]>>(k%8))&1)==0?-x[block*128+k]:x[block*128+k];
                float scale=(float)BitConverter.UInt16BitsToHalf(Unsafe.ReadUnaligned<ushort>(p));
                total+=dot*(scales[block]*scale);
            }
            y[row]=total;
        }
    }
}
