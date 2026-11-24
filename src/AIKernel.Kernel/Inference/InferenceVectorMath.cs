using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace AIKernel.Kernel.Inference;

// Common CPU stages, including the CPU portion of GPU offload. Runtime feature
// checks also cover AVX-only hosts; no unconditional FMA or AVX2 integer intrinsic.
internal static unsafe class InferenceVectorMath
{
    internal static void SwiGlu(float[] gate, float[] up, int hidden, bool ordered)
    {
        if (!ordered) { SwiGlu(gate, up); return; }
        for (int i = 0; i < hidden; i++) gate[i] = gate[i] / (1 + MathF.Exp(-gate[i])) * up[i];
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    internal static float Dot(ReadOnlySpan<float> x,ReadOnlySpan<float> y)
    {
        if(x.Length!=y.Length)throw new ArgumentException("Dot dimensions mismatch.");
        float sum=0;int i=0,n=x.Length;
        fixed(float* a=x)fixed(float* b=y)
        {
            if(Avx512F.IsSupported)
            {
                var s0=Vector512<float>.Zero;var s1=s0;var s2=s0;var s3=s0;
                for(;i+64<=n;i+=64)
                {
                    s0+=Vector512.Load(a+i)*Vector512.Load(b+i);s1+=Vector512.Load(a+i+16)*Vector512.Load(b+i+16);
                    s2+=Vector512.Load(a+i+32)*Vector512.Load(b+i+32);s3+=Vector512.Load(a+i+48)*Vector512.Load(b+i+48);
                }
                var total=(s0+s1)+(s2+s3);
                for(;i+16<=n;i+=16)total+=Vector512.Load(a+i)*Vector512.Load(b+i);
                sum=Vector512.Sum(total);
            }
            else if(Avx.IsSupported)
            {
                var s0=Vector256<float>.Zero;var s1=s0;var s2=s0;var s3=s0;
                for(;i+32<=n;i+=32)
                {
                    s0+=Vector256.Load(a+i)*Vector256.Load(b+i);s1+=Vector256.Load(a+i+8)*Vector256.Load(b+i+8);
                    s2+=Vector256.Load(a+i+16)*Vector256.Load(b+i+16);s3+=Vector256.Load(a+i+24)*Vector256.Load(b+i+24);
                }
                var total=(s0+s1)+(s2+s3);
                for(;i+8<=n;i+=8)total+=Vector256.Load(a+i)*Vector256.Load(b+i);
                sum=Vector256.Sum(total);
            }
            for(;i<n;i++)sum+=a[i]*b[i];
        }
        return sum;
    }
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    internal static void Norm(ReadOnlySpan<float> x,ReadOnlySpan<float> weights,Span<float> y,float epsilon)
    {
        if(x.Length!=weights.Length||y.Length<x.Length||x.Length==0)throw new ArgumentException("Norm dimensions mismatch.");
        float scale=1/MathF.Sqrt(Dot(x,x)/x.Length+epsilon);int i=0;
        fixed(float* a=x)fixed(float* w=weights)fixed(float* b=y)
        {
            if(Avx512F.IsSupported)for(;i+16<=x.Length;i+=16)(Vector512.Load(a+i)*Vector512.Create(scale)*Vector512.Load(w+i)).Store(b+i);
            else if(Avx.IsSupported)for(;i+8<=x.Length;i+=8)(Vector256.Load(a+i)*Vector256.Create(scale)*Vector256.Load(w+i)).Store(b+i);
            for(;i<x.Length;i++)b[i]=a[i]*scale*w[i];
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    internal static void SwiGlu(Span<float> gate,ReadOnlySpan<float> up)
    {
        if(gate.Length!=up.Length)throw new ArgumentException("SwiGLU dimensions mismatch.");
        int i=0;
        fixed(float* g=gate)fixed(float* u=up)
        {
            if(Avx512F.IsSupported)for(;i+16<=gate.Length;i+=16)
            {var v=Vector512.Load(g+i);(v/(Vector512.Create(1f)+Vector512.Exp(-v))*Vector512.Load(u+i)).Store(g+i);}
            else if(Avx.IsSupported)for(;i+8<=gate.Length;i+=8)
            {var v=Vector256.Load(g+i);(v/(Vector256.Create(1f)+Vector256.Exp(-v))*Vector256.Load(u+i)).Store(g+i);}
            for(;i<gate.Length;i++)g[i]=g[i]/(1+MathF.Exp(-g[i]))*u[i];
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    internal static void Add(Span<float> target,ReadOnlySpan<float> source)
    {
        if(target.Length!=source.Length)throw new ArgumentException("Residual dimensions mismatch.");
        int i=0;
        fixed(float* a=target)fixed(float* b=source)
        {
            if(Avx512F.IsSupported)for(;i+16<=target.Length;i+=16)(Vector512.Load(a+i)+Vector512.Load(b+i)).Store(a+i);
            else if(Avx.IsSupported)for(;i+8<=target.Length;i+=8)(Vector256.Load(a+i)+Vector256.Load(b+i)).Store(a+i);
            for(;i<target.Length;i++)a[i]+=b[i];
        }
    }
}
