using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace AIKernel.Kernel.Inference;

/// <summary>FP32 activations, encoded mapped weights. No expanded weight cache or extra quantization.</summary>
internal static unsafe class Gemma4CpuMatrix
{
    // Encoded weights only: 256 KiB shared by all Gemma matrices. Preserve the
    // runtime's Half conversion bits, including subnormals, signed zero and NaNs.
    private static readonly float[] HalfValues = Enumerable.Range(0, 65536)
        .Select(i => (float)BitConverter.UInt16BitsToHalf((ushort)i)).ToArray();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float HalfToSingle(ushort bits) => HalfValues[bits];

    internal static bool Supported => Avx2.IsSupported;
    internal static Gemma4InstructionSet Resolve(Gemma4InstructionSet requested)
    {
        if(requested==Gemma4InstructionSet.Auto)return Avx512F.IsSupported?Gemma4InstructionSet.Avx512:Avx2.IsSupported?Gemma4InstructionSet.Avx2:Gemma4InstructionSet.Auto;
        if(requested==Gemma4InstructionSet.Avx2&&Avx2.IsSupported||requested==Gemma4InstructionSet.Avx512&&Avx512F.IsSupported||Gemma4VnniMatrix.IsAvailable(requested))return requested;
        throw new PlatformNotSupportedException("The requested Gemma CPU ISA requires compatible hardware, OS and native library.");
    }
    internal static bool Uses512(Gemma4InstructionSet isa)=>isa==Gemma4InstructionSet.Avx512||isa==Gemma4InstructionSet.Avx512VnniResidual||isa==Gemma4InstructionSet.Auto&&Avx512F.IsSupported;
    internal static float Dot(ReadOnlySpan<byte> weights,ReadOnlySpan<float> input,int type,Gemma4InstructionSet isa=Gemma4InstructionSet.Auto)
    {
        if(!Supported||type is not (1 or 2 or 14)||input.Length%32!=0||weights.Length!=GgufTensorEncoding.Get(type).RowBytes(input.Length))
            throw new ArgumentException("Unsupported SIMD dot layout.");
        fixed(byte* w=weights)fixed(float* x=input)return Uses512(isa)?Dot512(w,x,input.Length,type):Dot(w,x,input.Length,type);
    }
    internal static void Multiply(GgufWeights file,GgufWeights.Tensor tensor,float[] input,float[] output,ParallelOptions options,Gemma4InstructionSet isa=Gemma4InstructionSet.Auto)
    {
        if(!Supported || tensor.Type is not (1 or 2 or 14) || tensor.Columns%32!=0)
        {ReferenceCpuMatrix.Multiply(file,tensor,input,output,options);return;}
        if(tensor.Rank>2||input.Length<tensor.Columns||output.Length<tensor.Rows)throw new ArgumentException("Matrix dimensions mismatch.");
        options.CancellationToken.ThrowIfCancellationRequested();
        nint address=(nint)file.Pointer(tensor);
        if(tensor.Type==2&&Gemma4VnniMatrix.IsResidual(isa))
        {
            using var prepared=new Gemma4VnniMatrix(input.AsSpan(0,tensor.Columns));
            prepared.Multiply(address,tensor.Rows,output,0,options,isa);return;
        }
        void Row(int row)
        {
            options.CancellationToken.ThrowIfCancellationRequested();
            fixed(float* x=input)output[row]=Uses512(isa)?Dot512((byte*)address+row*tensor.RowByteLength,x,tensor.Columns,tensor.Type):Dot((byte*)address+row*tensor.RowByteLength,x,tensor.Columns,tensor.Type);
        }
        if(options.MaxDegreeOfParallelism==1)for(int r=0;r<tensor.Rows;r++)Row(r);
        else Parallel.For(0,tensor.Rows,options,Row);
    }
    private static float Dot512(byte* w, float* x, int columns, int type) => type switch
    {
        1 => DotHalf512(w, x, columns),
        2 => DotQ4_512(w, x, columns),
        14 => DotQ6_512(w, x, columns),
        _ => throw new ArgumentException("Unsupported SIMD dot type.")
    };

    internal static float Dot(byte* w, float* x, int columns, int type) => type switch
    {
        1 => DotHalf256(w, x, columns),
        2 => DotQ4_256(w, x, columns),
        14 => DotQ6_256(w, x, columns),
        _ => throw new ArgumentException("Unsupported SIMD dot type.")
    };

    private static float DotQ4_512(byte* w,float* x,int columns)
    {
        var a=Vector512<float>.Zero;var b=a;var c=a;var d=a;
        var mask=Vector128.Create((byte)15);var eight=Vector512.Create(8);
        for(int k=0;k<columns;k+=32,w+=18)
        {
            var scale=Vector512.Create(HalfToSingle(Unsafe.ReadUnaligned<ushort>(w)));var packed=Sse2.LoadVector128(w+2);
            var low=Sse2.And(packed,mask);var high=Sse2.And(Sse2.ShiftRightLogical(packed.AsUInt16(),4).AsByte(),mask);
            var w0=Avx512F.Multiply(Avx512F.ConvertToVector512Single(Avx512F.Subtract(Avx512F.ConvertToVector512Int32(low),eight)),scale);
            var w1=Avx512F.Multiply(Avx512F.ConvertToVector512Single(Avx512F.Subtract(Avx512F.ConvertToVector512Int32(high),eight)),scale);
            // Alternate two independent pairs without mixing quantization scales.
            if((k&32)==0){a=Avx512F.Add(a,Avx512F.Multiply(w0,Avx512F.LoadVector512(x+k)));b=Avx512F.Add(b,Avx512F.Multiply(w1,Avx512F.LoadVector512(x+k+16)));}
            else{c=Avx512F.Add(c,Avx512F.Multiply(w0,Avx512F.LoadVector512(x+k)));d=Avx512F.Add(d,Avx512F.Multiply(w1,Avx512F.LoadVector512(x+k+16)));}
        }
        return Vector512.Sum(Avx512F.Add(Avx512F.Add(a,b),Avx512F.Add(c,d)));
    }
    private static float DotQ4_256(byte* w,float* x,int columns)
    {
        var a=Vector256<float>.Zero;var b=a;var c=a;var d=a;
        var mask=Vector128.Create((byte)15);var eight=Vector256.Create(8);
        for(int k=0;k<columns;k+=32,w+=18)
        {
            var scale=Vector256.Create(HalfToSingle(Unsafe.ReadUnaligned<ushort>(w)));var packed=Sse2.LoadVector128(w+2);
            var low=Sse2.And(packed,mask);var high=Sse2.And(Sse2.ShiftRightLogical(packed.AsUInt16(),4).AsByte(),mask);
            var w0=Avx.Multiply(Avx.ConvertToVector256Single(Avx2.Subtract(Avx2.ConvertToVector256Int32(low),eight)),scale);
            var w1=Avx.Multiply(Avx.ConvertToVector256Single(Avx2.Subtract(Avx2.ConvertToVector256Int32(Sse2.ShiftRightLogical128BitLane(low,8)),eight)),scale);
            var w2=Avx.Multiply(Avx.ConvertToVector256Single(Avx2.Subtract(Avx2.ConvertToVector256Int32(high),eight)),scale);
            var w3=Avx.Multiply(Avx.ConvertToVector256Single(Avx2.Subtract(Avx2.ConvertToVector256Int32(Sse2.ShiftRightLogical128BitLane(high,8)),eight)),scale);
            a=Avx.Add(a,Avx.Multiply(w0,Avx.LoadVector256(x+k)));b=Avx.Add(b,Avx.Multiply(w1,Avx.LoadVector256(x+k+8)));
            c=Avx.Add(c,Avx.Multiply(w2,Avx.LoadVector256(x+k+16)));d=Avx.Add(d,Avx.Multiply(w3,Avx.LoadVector256(x+k+24)));
        }
        return Vector256.Sum(Avx.Add(Avx.Add(a,b),Avx.Add(c,d)));
    }

    // Q6_K stores 128 low-nibble bytes, 64 high-bit bytes, sixteen signed
    // scales and one Half. Each 32-element slice has two independent scales.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<byte> UnpackQ6(Vector256<ushort> low, Vector256<ushort> high)
    {
        // Mask each byte after the word shifts to discard bits from its neighbour.
        var nibbles = Avx2.And(low.AsByte(), Vector256.Create((byte)15));
        var upperBits = Avx2.And(high.AsByte(), Vector256.Create((byte)3));
        return Avx2.Or(nibbles, Avx2.ShiftLeftLogical(upperBits.AsUInt16(), 4).AsByte());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AccumulateQ6_256(Vector256<byte> packed, float scale, sbyte* scales, float* x,
        ref Vector256<float> a, ref Vector256<float> b, ref Vector256<float> c, ref Vector256<float> d)
    {
        var low = packed.GetLower(); var high = packed.GetUpper(); var zero = Vector256.Create(32);
        var s0 = Vector256.Create(scale * scales[0]); var s1 = Vector256.Create(scale * scales[1]);
        var w0 = Avx.Multiply(s0, Avx.ConvertToVector256Single(Avx2.Subtract(Avx2.ConvertToVector256Int32(low), zero)));
        var w1 = Avx.Multiply(s0, Avx.ConvertToVector256Single(Avx2.Subtract(Avx2.ConvertToVector256Int32(Sse2.ShiftRightLogical128BitLane(low, 8)), zero)));
        var w2 = Avx.Multiply(s1, Avx.ConvertToVector256Single(Avx2.Subtract(Avx2.ConvertToVector256Int32(high), zero)));
        var w3 = Avx.Multiply(s1, Avx.ConvertToVector256Single(Avx2.Subtract(Avx2.ConvertToVector256Int32(Sse2.ShiftRightLogical128BitLane(high, 8)), zero)));
        // Keep (blockScale * localScale) * quant, then * activation, then add.
        // Neither FMA nor offset factoring preserves this rounding contract.
        a = Avx.Add(a, Avx.Multiply(w0, Avx.LoadVector256(x)));
        b = Avx.Add(b, Avx.Multiply(w1, Avx.LoadVector256(x + 8)));
        c = Avx.Add(c, Avx.Multiply(w2, Avx.LoadVector256(x + 16)));
        d = Avx.Add(d, Avx.Multiply(w3, Avx.LoadVector256(x + 24)));
    }

    private static float DotQ6_256(byte* w, float* x, int columns)
    {
        var a = Vector256<float>.Zero; var b = a; var c = a; var d = a;
        for (int k = 0; k < columns; k += 256, w += 210)
        {
            float scale = HalfToSingle(Unsafe.ReadUnaligned<ushort>(w + 208));
            for (int half = 0; half < 2; half++)
            {
                var low0 = Avx.LoadVector256(w + half * 64).AsUInt16();
                var low1 = Avx.LoadVector256(w + half * 64 + 32).AsUInt16();
                var high = Avx.LoadVector256(w + 128 + half * 32).AsUInt16();
                var s = (sbyte*)(w + 192 + half * 8); var input = x + k + half * 128;
                AccumulateQ6_256(UnpackQ6(low0, high), scale, s, input, ref a, ref b, ref c, ref d);
                AccumulateQ6_256(UnpackQ6(low1, high >> 2), scale, s + 2, input + 32, ref a, ref b, ref c, ref d);
                AccumulateQ6_256(UnpackQ6(low0 >> 4, high >> 4), scale, s + 4, input + 64, ref a, ref b, ref c, ref d);
                AccumulateQ6_256(UnpackQ6(low1 >> 4, high >> 6), scale, s + 6, input + 96, ref a, ref b, ref c, ref d);
            }
        }
        return Vector256.Sum(Avx.Add(Avx.Add(a, b), Avx.Add(c, d)));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AccumulateQ6_512(Vector256<byte> packed, float scale, sbyte* scales, float* x,
        ref Vector512<float> a, ref Vector512<float> b)
    {
        var zero = Vector512.Create(32);
        var w0 = Avx512F.Multiply(Vector512.Create(scale * scales[0]), Avx512F.ConvertToVector512Single(Avx512F.Subtract(Avx512F.ConvertToVector512Int32(packed.GetLower()), zero)));
        var w1 = Avx512F.Multiply(Vector512.Create(scale * scales[1]), Avx512F.ConvertToVector512Single(Avx512F.Subtract(Avx512F.ConvertToVector512Int32(packed.GetUpper()), zero)));
        a = Avx512F.Add(a, Avx512F.Multiply(w0, Avx512F.LoadVector512(x)));
        b = Avx512F.Add(b, Avx512F.Multiply(w1, Avx512F.LoadVector512(x + 16)));
    }

    private static float DotQ6_512(byte* w, float* x, int columns)
    {
        var a = Vector512<float>.Zero; var b = a;
        for (int k = 0; k < columns; k += 256, w += 210)
        {
            float scale = HalfToSingle(Unsafe.ReadUnaligned<ushort>(w + 208));
            for (int half = 0; half < 2; half++)
            {
                var low0 = Avx.LoadVector256(w + half * 64).AsUInt16();
                var low1 = Avx.LoadVector256(w + half * 64 + 32).AsUInt16();
                var high = Avx.LoadVector256(w + 128 + half * 32).AsUInt16();
                var s = (sbyte*)(w + 192 + half * 8); var input = x + k + half * 128;
                AccumulateQ6_512(UnpackQ6(low0, high), scale, s, input, ref a, ref b);
                AccumulateQ6_512(UnpackQ6(low1, high >> 2), scale, s + 2, input + 32, ref a, ref b);
                AccumulateQ6_512(UnpackQ6(low0 >> 4, high >> 4), scale, s + 4, input + 64, ref a, ref b);
                AccumulateQ6_512(UnpackQ6(low1 >> 4, high >> 6), scale, s + 6, input + 96, ref a, ref b);
            }
        }
        // The former tile path left c and d at +0; retain that final addition.
        return Vector512.Sum(Avx512F.Add(Avx512F.Add(a, b), Vector512<float>.Zero));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<float> LoadHalf8(ushort* w) => Vector256.Create(
        HalfToSingle(w[0]), HalfToSingle(w[1]), HalfToSingle(w[2]), HalfToSingle(w[3]),
        HalfToSingle(w[4]), HalfToSingle(w[5]), HalfToSingle(w[6]), HalfToSingle(w[7]));

    private static float DotHalf256(byte* w, float* x, int columns)
    {
        var a = Vector256<float>.Zero; var b = a; var c = a; var d = a;
        for (int k = 0; k < columns; k += 32, w += 64)
        {
            var p = (ushort*)w;
            a = Avx.Add(a, Avx.Multiply(LoadHalf8(p), Avx.LoadVector256(x + k)));
            b = Avx.Add(b, Avx.Multiply(LoadHalf8(p + 8), Avx.LoadVector256(x + k + 8)));
            c = Avx.Add(c, Avx.Multiply(LoadHalf8(p + 16), Avx.LoadVector256(x + k + 16)));
            d = Avx.Add(d, Avx.Multiply(LoadHalf8(p + 24), Avx.LoadVector256(x + k + 24)));
        }
        return Vector256.Sum(Avx.Add(Avx.Add(a, b), Avx.Add(c, d)));
    }

    private static float DotHalf512(byte* w, float* x, int columns)
    {
        var a = Vector512<float>.Zero; var b = a;
        for (int k = 0; k < columns; k += 32, w += 64)
        {
            var p = (ushort*)w;
            a = Avx512F.Add(a, Avx512F.Multiply(Vector512.Create(LoadHalf8(p), LoadHalf8(p + 8)), Avx512F.LoadVector512(x + k)));
            b = Avx512F.Add(b, Avx512F.Multiply(Vector512.Create(LoadHalf8(p + 16), LoadHalf8(p + 24)), Avx512F.LoadVector512(x + k + 16)));
        }
        return Vector512.Sum(Avx512F.Add(Avx512F.Add(a, b), Vector512<float>.Zero));
    }
}
