using System.Runtime.CompilerServices;
using System.Buffers;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace AIKernel.Kernel.Inference;

internal static unsafe class QuantizedCpuMatrix
{
    // Initialize lookup tables before any hot kernel is JIT-compiled, including tuning before Row().
    // An explicit constructor removes beforefieldinit semantics and repeated lazy-init checks.
    static QuantizedCpuMatrix() {}
    private static readonly float[] HalfValues=Enumerable.Range(0,65536).Select(i=>(float)BitConverter.UInt16BitsToHalf((ushort)i)).ToArray();
    private static readonly Vector256<float>[] Signs=Enumerable.Range(0,256).Select(b=>Vector256.Create(
        Mask(b,0),Mask(b,1),Mask(b,2),Mask(b,3),Mask(b,4),Mask(b,5),Mask(b,6),Mask(b,7)).AsSingle()).ToArray();
    private static int Mask(int b,int bit)=>(b&(1<<bit))==0?int.MinValue:0;
    internal static string Backend=>Resolve(Environment.GetEnvironmentVariable("AIKERNEL_CPU_GEMV"));
    internal static string Resolve(string? requested)
    {
            var auto=Avx512F.IsSupported?"avx512":Avx2.IsSupported&&Fma.IsSupported?"avx2":Avx.IsSupported?"avx":"scalar";
            if(string.IsNullOrEmpty(requested)||requested=="auto")return auto;
            if(requested=="q8-auto"||Int8CpuMatrix.IsBackend(requested)||Int8CpuMatrix.IsRoundedFloatBackend(requested))return Int8CpuMatrix.Resolve(requested);
            if(requested=="scalar" || requested=="avx"&&Avx.IsSupported || requested=="avx2"&&Avx2.IsSupported&&Fma.IsSupported ||
               requested=="avx512"&&Avx512F.IsSupported)return requested;
            if(!CpuInstructionSets.Known.Contains(requested))throw new ArgumentException($"Unknown CPU instruction set '{requested}'.",nameof(requested));
            throw new PlatformNotSupportedException($"Requested CPU instruction set '{requested}' is unavailable on this CPU/OS.");
    }
    internal static void Row(GgufWeights file,GgufWeights.Tensor t,int row,Span<float> output)
    {
        if(t.Rank>2||(uint)row>=(uint)t.Rows||output.Length<t.Columns)throw new ArgumentException("Row dimensions mismatch.");
        var p=file.Pointer(t)+(long)row*t.RowBytes;
        if(t.Type==0){new ReadOnlySpan<float>(p,t.Columns).CopyTo(output);return;}
        if(t.Type!=41){t.Encoding.Decode(new ReadOnlySpan<byte>(p,t.RowBytes),output[..t.Columns]);return;}
        for(int i=0;i<t.Columns;i+=128,p+=18)
        {
            float scale=HalfValues[Unsafe.ReadUnaligned<ushort>(p)];
            for(int j=0;j<128;j++)output[i+j]=((p[2+j/8]>>(j%8))&1)==0?-scale:scale;
        }
    }
    internal static void Multiply(GgufWeights file,GgufWeights.Tensor t,float[] x,float[] y,ParallelOptions options,string backend,InferenceProfiler? profile=null,PreparedQ8Activation? prepared=null,bool preserveArithmetic=false)
    {
        if(t.Rank>2||x.Length<t.Columns||y.Length<t.Rows)throw new ArgumentException("Matrix dimensions mismatch.");
        if(t.Type is not (0 or 41))
        {
            if(backend.EndsWith("-q8",StringComparison.Ordinal))throw new NotSupportedException("Q8 activation kernels require Q1_0 weights.");
            ReferenceCpuMatrix.Multiply(file,t,x,y,options);return;
        }
        if(t.Type==41&&Int8CpuMatrix.IsRoundedFloatBackend(backend))
        {
            options.CancellationToken.ThrowIfCancellationRequested();
            using var owned=prepared is null?new PreparedQ8Activation(x,t.Columns,true,profile):null;
            var activation=prepared??owned!;activation.Validate(x,t.Columns,rounded:true);
            Multiply(file,t,activation.Rounded!,y,options,backend[..^3],preserveArithmetic:true);return;
        }
        if(t.Type==41&&Int8CpuMatrix.IsBackend(backend)){Int8CpuMatrix.Multiply(file,t,x,y,options,backend,profile,prepared);return;}
        nint ptr=(nint)file.Pointer(t);
        int workers=Math.Min(options.MaxDegreeOfParallelism,Math.Max(1,t.Rows/64));
        var lut = t.Type == 41 && backend != "scalar" && Environment.GetEnvironmentVariable("AIKERNEL_Q1_KERNEL") == "lookup" ? ArrayPool<float>.Shared.Rent(t.Columns * 32) : null;
        if(lut is not null) BuildLookup(x,t.Columns,lut,backend);
        try
        {
            fixed(float* destination=y)
            fixed(float* input=x)
            {
                nuint address=(nuint)destination;
                nint inputAddress=(nint)input;
                void Work(int worker)
                {
                    int first=worker==0?0:RowBoundary(t.Rows*worker/workers,t.Rows,address);
                    int last=worker+1==workers?t.Rows:RowBoundary(t.Rows*(worker+1)/workers,t.Rows,address);
                    MultiplyRows((byte*)ptr,t.Columns,t.RowBytes,t.Type,(float*)inputAddress,(float*)address,first,last,backend,lut,options.CancellationToken,preserveArithmetic);
                }
                if(workers==1)Work(0);else Parallel.For(0,workers,options,Work);
            }
        }
        finally {if(lut is not null)ArrayPool<float>.Shared.Return(lut,clearArray:true);}
    }
    // Keep captured state and managed-array range checks outside the hot row loop.
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void MultiplyRows(byte* weights,int columns,int rowBytes,int type,float* input,float* output,
        int first,int last,string backend,float[]? lookup,CancellationToken cancellationToken,bool preserveArithmetic)
    {
        for(int row=first;row<last;row++)
        {
            if((row&63)==0)cancellationToken.ThrowIfCancellationRequested();
            var p=weights+(long)row*rowBytes;
            output[row]=type==41?(lookup is null?(preserveArithmetic?Q1Legacy(p,input,columns,backend):Q1(p,input,columns,backend)):LookupDot(p,lookup,columns,preserveArithmetic)):F32((float*)p,input,columns);
        }
    }
    // Align actual output addresses, rather than assuming a managed array starts on a cache line.
    internal static int RowBoundary(int row,int rows,nuint address)
    {
        nuint aligned=(address+(nuint)row*sizeof(float)+63)&~(nuint)63;
        return Math.Min(rows,checked((int)((aligned-address)/sizeof(float))));
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
    private static float LookupDot(byte* p,float[] table,int n,bool preserveArithmetic)
    {
        float total=0;
        fixed(float* lookup=table)
        for(int i=0;i<n;i+=128,p+=18)
        {
            float* l=lookup+i*32;float a=0,b=0,c=0,d=0;
            float sum=0;
            if(preserveArithmetic)for(int j=0;j<16;j++)sum+=l[j*256+p[2+j]];
            else
            {
                for(int j=0;j<16;j+=4)
                {a+=l[j*256+p[2+j]];b+=l[(j+1)*256+p[3+j]];c+=l[(j+2)*256+p[4+j]];d+=l[(j+3)*256+p[5+j]];}
                sum=(a+b)+(c+d);
            }
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
                var a=Vector512<float>.Zero;var b=a;var c=a;var d=a;
                for(int j=0;j<16;j+=8)
                {
                    a=Avx512F.Add(a,Avx512F.Xor(Avx512F.LoadVector512(x+i+j*8).AsInt32(),Vector512.Create(Signs[p[2+j]],Signs[p[3+j]]).AsInt32()).AsSingle());
                    b=Avx512F.Add(b,Avx512F.Xor(Avx512F.LoadVector512(x+i+j*8+16).AsInt32(),Vector512.Create(Signs[p[4+j]],Signs[p[5+j]]).AsInt32()).AsSingle());
                    c=Avx512F.Add(c,Avx512F.Xor(Avx512F.LoadVector512(x+i+j*8+32).AsInt32(),Vector512.Create(Signs[p[6+j]],Signs[p[7+j]]).AsInt32()).AsSingle());
                    d=Avx512F.Add(d,Avx512F.Xor(Avx512F.LoadVector512(x+i+j*8+48).AsInt32(),Vector512.Create(Signs[p[8+j]],Signs[p[9+j]]).AsInt32()).AsSingle());
                }
                var sum=Avx512F.Add(Avx512F.Add(a,b),Avx512F.Add(c,d));
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
                var a=Vector256<float>.Zero;var b=a;var c=a;var d=a;
                for(int j=0;j<16;j+=4)
                {
                    a=Avx.Add(a,Avx.Xor(Avx.LoadVector256(x+i+j*8),Signs[p[2+j]]));
                    b=Avx.Add(b,Avx.Xor(Avx.LoadVector256(x+i+j*8+8),Signs[p[3+j]]));
                    c=Avx.Add(c,Avx.Xor(Avx.LoadVector256(x+i+j*8+16),Signs[p[4+j]]));
                    d=Avx.Add(d,Avx.Xor(Avx.LoadVector256(x+i+j*8+24),Signs[p[5+j]]));
                }
                var sum=Avx.Add(Avx.Add(a,b),Avx.Add(c,d));
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
    // Q8 rounding boundaries amplify tiny changes. Retain its original FP32 accumulation order.
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static float Q1Legacy(byte* p,float* x,int n,string backend)
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
