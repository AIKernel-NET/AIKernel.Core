using System.Runtime.CompilerServices;
using System.Buffers;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace AIKernel.Kernel.Inference;

internal static unsafe class QuantizedCpuMatrix
{
    private static readonly float[] HalfValues=Enumerable.Range(0,65536).Select(i=>(float)BitConverter.UInt16BitsToHalf((ushort)i)).ToArray();
    private static readonly Vector256<float>[] Signs=Enumerable.Range(0,256).Select(b=>Vector256.Create(
        Mask(b,0),Mask(b,1),Mask(b,2),Mask(b,3),Mask(b,4),Mask(b,5),Mask(b,6),Mask(b,7)).AsSingle()).ToArray();
    private static int Mask(int b,int bit)=>(b&(1<<bit))==0?int.MinValue:0;
    internal static string Backend
    {
        get
        {
            var requested=Environment.GetEnvironmentVariable("AIKERNEL_CPU_GEMV");
            var auto=Avx512F.IsSupported?"avx512":Avx2.IsSupported&&Fma.IsSupported?"avx2":Avx.IsSupported?"avx":"scalar";
            if(string.IsNullOrEmpty(requested)||requested=="auto")return auto;
            if(requested=="scalar" || requested=="avx"&&Avx.IsSupported || requested=="avx2"&&Avx2.IsSupported&&Fma.IsSupported ||
               requested=="avx512"&&Avx512F.IsSupported)return requested;
            throw new PlatformNotSupportedException("Requested CPU instruction set is unavailable.");
        }
    }
    internal static void Row(GgufWeights file,GgufWeights.Tensor t,int row,Span<float> output)
    {
        var p=file.Pointer(t)+(long)row*t.RowBytes;
        if(t.Type==0){new ReadOnlySpan<float>(p,t.Columns).CopyTo(output);return;}
        for(int i=0;i<t.Columns;i+=128,p+=18)
        {
            float scale=HalfValues[Unsafe.ReadUnaligned<ushort>(p)];
            for(int j=0;j<128;j++)output[i+j]=((p[2+j/8]>>(j%8))&1)==0?-scale:scale;
        }
    }
    internal static void Multiply(GgufWeights file,GgufWeights.Tensor t,float[] x,float[] y,ParallelOptions options,string backend)
    {
        if(x.Length<t.Columns||y.Length<t.Rows)throw new ArgumentException("Matrix dimensions mismatch.");
        nint ptr=(nint)file.Pointer(t);
        int workers=Math.Min(options.MaxDegreeOfParallelism,Math.Max(1,t.Rows/64));
        var lut = t.Type == 41 && backend != "scalar" && Environment.GetEnvironmentVariable("AIKERNEL_Q1_KERNEL") == "lookup" ? ArrayPool<float>.Shared.Rent(t.Columns * 32) : null;
        if(lut is not null) BuildLookup(x,t.Columns,lut,backend);
        try { Parallel.For(0,workers,options,w=>{
            int first=t.Rows*w/workers,last=t.Rows*(w+1)/workers;
            fixed(float* input=x)
            for(int row=first;row<last;row++)
            {
                if((row&63)==0)options.CancellationToken.ThrowIfCancellationRequested();
                var p=(byte*)ptr+(long)row*t.RowBytes;
                y[row]=t.Type==41?(lut is null?Q1(p,input,t.Columns,backend):LookupDot(p,lut,t.Columns)):F32((float*)p,input,t.Columns);
            }
        }); } finally {if(lut is not null)ArrayPool<float>.Shared.Return(lut,clearArray:true);}
    }
    private static void BuildLookup(float[] x,int n,float[] table,string backend)
    {
        fixed(float* data=table)
        for(int i=0;i<n;i+=8)
        {
            int start=i*32;float sum=0;for(int j=0;j<8;j++)sum-=x[i+j];data[start]=sum;
            for(int bit=0;bit<8;bit++)
            {
                int count=1<<bit,j=0;float delta=2*x[i+bit];
                if(backend=="avx512")for(;j+16<=count;j+=16)Avx512F.Store(data+start+count+j,Avx512F.Add(Avx512F.LoadVector512(data+start+j),Vector512.Create(delta)));
                for(;j+8<=count;j+=8)Avx.Store(data+start+count+j,Avx.Add(Avx.LoadVector256(data+start+j),Vector256.Create(delta)));
                for(;j<count;j++)data[start+count+j]=data[start+j]+delta;
            }
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static float LookupDot(byte* p,float[] table,int n)
    {
        float total=0;
        fixed(float* lookup=table)
        for(int i=0;i<n;i+=128,p+=18)
        {
            float* l=lookup+i*32;float sum=0;
            for(int j=0;j<16;j++)sum+=l[j*256+p[2+j]];
            total+=sum*HalfValues[Unsafe.ReadUnaligned<ushort>(p)];
        }
        return total;
    }
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static float F32(float* w,float* x,int n){float s=0;for(int i=0;i<n;i++)s+=w[i]*x[i];return s;}

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static float Q1(byte* p,float* x,int n,string backend)
    {
        float result=0;
        if(backend=="avx512")
        {
            Vector512<float> total=Vector512<float>.Zero;
            for(int i=0;i<n;i+=128,p+=18)
            {
                var sum=Vector512<float>.Zero;
                for(int j=0;j<16;j+=2)
                {
                    var sign=Vector512.Create(Signs[p[2+j]],Signs[p[3+j]]);
                    sum=Avx512F.Add(sum,Avx512F.Xor(Avx512F.LoadVector512(x+i+j*8).AsInt32(),sign.AsInt32()).AsSingle());
                }
                float scale=HalfValues[Unsafe.ReadUnaligned<ushort>(p)];
                total=Avx512F.Add(total,Avx512F.Multiply(sum,Vector512.Create(scale)));
            }
            return Vector512.Sum(total);
        }
        if(backend is "avx" or "avx2")
        {
            var total=Vector256<float>.Zero;
            for(int i=0;i<n;i+=128,p+=18)
            {
                var sum=Vector256<float>.Zero;
                for(int j=0;j<16;j++)sum=Avx.Add(sum,Avx.Xor(Avx.LoadVector256(x+i+j*8),Signs[p[2+j]]));
                float scale=HalfValues[Unsafe.ReadUnaligned<ushort>(p)];
                total=backend=="avx2"?Fma.MultiplyAdd(sum,Vector256.Create(scale),total):Avx.Add(total,Avx.Multiply(sum,Vector256.Create(scale)));
            }
            return Vector256.Sum(total);
        }
        for(int i=0;i<n;i+=128,p+=18)
        {
            float scale=HalfValues[Unsafe.ReadUnaligned<ushort>(p)],sum=0;
            for(int j=0;j<128;j++)sum+=((p[2+j/8]>>(j%8))&1)==0?-x[i+j]:x[i+j];
            result+=sum*scale;
        }
        return result;
    }
}
