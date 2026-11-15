using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace AIKernel.Kernel.Inference;

/// <summary>FP32 activations, encoded mapped weights. No expanded weight cache or extra quantization.</summary>
internal static unsafe class Gemma4CpuMatrix
{
    internal static bool Supported => Avx2.IsSupported;
    internal static float Dot(ReadOnlySpan<byte> weights,ReadOnlySpan<float> input,int type)
    {
        if(!Supported||type is not (1 or 2 or 14)||input.Length%32!=0||weights.Length!=GgufTensorEncoding.Get(type).RowBytes(input.Length))
            throw new ArgumentException("Unsupported SIMD dot layout.");
        fixed(byte* w=weights)fixed(float* x=input)return Dot(w,x,input.Length,type);
    }
    internal static void Multiply(GgufWeights file,GgufWeights.Tensor tensor,float[] input,float[] output,ParallelOptions options)
    {
        if(!Supported || tensor.Type is not (1 or 2 or 14) || tensor.Columns%32!=0)
        {ReferenceCpuMatrix.Multiply(file,tensor,input,output,options);return;}
        if(tensor.Rank>2||input.Length<tensor.Columns||output.Length<tensor.Rows)throw new ArgumentException("Matrix dimensions mismatch.");
        options.CancellationToken.ThrowIfCancellationRequested();
        nint address=(nint)file.Pointer(tensor);
        void Row(int row)
        {
            options.CancellationToken.ThrowIfCancellationRequested();
            fixed(float* x=input)output[row]=Dot((byte*)address+row*tensor.RowByteLength,x,tensor.Columns,tensor.Type);
        }
        if(options.MaxDegreeOfParallelism==1)for(int r=0;r<tensor.Rows;r++)Row(r);
        else Parallel.For(0,tensor.Rows,options,Row);
    }
    internal static float Dot(byte* w,float* x,int columns,int type)
    {
        var a=Vector256<float>.Zero;var b=a;var c=a;var d=a;
        var mask=Vector128.Create((byte)15);var eight=Vector256.Create(8);
        Span<float> tile=stackalloc float[256];
        for(int k=0;k<columns;)
        {
            if(type==2)
            {
                var scale=Vector256.Create((float)*(Half*)w);var packed=Sse2.LoadVector128(w+2);
                var low=Sse2.And(packed,mask);var high=Sse2.And(Sse2.ShiftRightLogical(packed.AsUInt16(),4).AsByte(),mask);
                var w0=Avx.Multiply(Avx.ConvertToVector256Single(Avx2.Subtract(Avx2.ConvertToVector256Int32(low),eight)),scale);
                var w1=Avx.Multiply(Avx.ConvertToVector256Single(Avx2.Subtract(Avx2.ConvertToVector256Int32(Sse2.ShiftRightLogical128BitLane(low,8)),eight)),scale);
                var w2=Avx.Multiply(Avx.ConvertToVector256Single(Avx2.Subtract(Avx2.ConvertToVector256Int32(high),eight)),scale);
                var w3=Avx.Multiply(Avx.ConvertToVector256Single(Avx2.Subtract(Avx2.ConvertToVector256Int32(Sse2.ShiftRightLogical128BitLane(high,8)),eight)),scale);
                a=Avx.Add(a,Avx.Multiply(w0,Avx.LoadVector256(x+k)));b=Avx.Add(b,Avx.Multiply(w1,Avx.LoadVector256(x+k+8)));
                c=Avx.Add(c,Avx.Multiply(w2,Avx.LoadVector256(x+k+16)));d=Avx.Add(d,Avx.Multiply(w3,Avx.LoadVector256(x+k+24)));
                w+=18;k+=32;
            }
            else
            {
                // Q6_K's 16-element signed scales must be applied before multiplying activations.
                // Decode into a bounded stack tile, then consume independent FP32 accumulators.
                int count=type==14?256:32;
                if(type==14)GgufTensorEncoding.Get(14).Decode(new ReadOnlySpan<byte>(w,210),tile);
                else for(int i=0;i<count;i++)tile[i]=(float)((Half*)w)[i];
                fixed(float* v=tile)
                for(int i=0;i<count;i+=32)
                {
                    a=Avx.Add(a,Avx.Multiply(Avx.LoadVector256(v+i),Avx.LoadVector256(x+k+i)));
                    b=Avx.Add(b,Avx.Multiply(Avx.LoadVector256(v+i+8),Avx.LoadVector256(x+k+i+8)));
                    c=Avx.Add(c,Avx.Multiply(Avx.LoadVector256(v+i+16),Avx.LoadVector256(x+k+i+16)));
                    d=Avx.Add(d,Avx.Multiply(Avx.LoadVector256(v+i+24),Avx.LoadVector256(x+k+i+24)));
                }
                tile.Clear();w+=type==14?210:64;k+=count;
            }
        }
        return Vector256.Sum(Avx.Add(Avx.Add(a,b),Avx.Add(c,d)));
    }
}
