using AIKernel.Kernel.Inference;
using System.Buffers.Binary;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace AIKernel.Core.Tests.Inference;

public sealed class Gemma4MatrixTests
{
    [Fact]
    public void HalfLookupPreservesEveryRuntimeConversionBitPattern()
    {
        for (int bits = 0; bits <= ushort.MaxValue; bits++)
            Assert.Equal(BitConverter.SingleToInt32Bits((float)BitConverter.UInt16BitsToHalf((ushort)bits)),
                BitConverter.SingleToInt32Bits(Gemma4CpuMatrix.HalfToSingle((ushort)bits)));
    }

    [Theory]
    [InlineData(1, 32)] [InlineData(1, 1536)]
    [InlineData(2, 32)] [InlineData(2, 1536)] [InlineData(2, 12288)]
    [InlineData(14, 256)] [InlineData(14, 1536)] [InlineData(14, 8960)]
    public void EncodedDotPreservesBitsOfScalarDecodedWeightsAndFormerReduction(int type, int columns)
    {
        if (!Gemma4CpuMatrix.Supported) return;
        var encoding = GgufTensorEncoding.Get(type);
        var random = new Random(493 + type + columns);
        ushort[] scales = [0, 0x8000, 1, 0x8001, 0x03ff, 0x0400, 0x3555, 0xb555, 0x3c00, 0xbc00, 0x7bff, 0xfbff];
        for (int sample = 0; sample < scales.Length; sample++)
        {
            // A one-byte offset and Q6_K's 210-byte stride exercise unaligned loads.
            byte[] storage = new byte[checked((int)encoding.RowBytes(columns)) + 1];
            random.NextBytes(storage); var encoded = storage.AsSpan(1);
            for (int offset = 0; offset < encoded.Length; offset += encoding.BlockBytes)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(encoded[(offset + (type == 14 ? 208 : 0))..],
                    scales[(sample + offset / encoding.BlockBytes) % scales.Length]);
                if (type == 14)
                    for (int j = 0; j < 16; j++)
                        encoded[offset + 192 + j] = unchecked((byte)(sbyte)((j % 4) switch { 0 => -128, 1 => 127, 2 => 0, _ => j - 8 }));
            }
            var weights = new float[columns]; encoding.Decode(encoded, weights);
            var input = Enumerable.Range(0, columns).Select(i => MathF.ScaleB((float)(random.NextDouble() - .5), i % 17 - 8)).ToArray();
            foreach (var isa in new[] { Gemma4InstructionSet.Avx2, Gemma4InstructionSet.Avx512 })
            {
                if (isa == Gemma4InstructionSet.Avx512 && !Avx512F.IsSupported) continue;
                float expected = FormerReduction(weights, input, type, isa);
                float actual = Gemma4CpuMatrix.Dot(encoded, input, type, isa);
                Assert.True(BitConverter.SingleToInt32Bits(expected) == BitConverter.SingleToInt32Bits(actual),
                    $"type={type}, columns={columns}, sample={sample}, isa={isa}: {expected:R} != {actual:R}");
            }
        }
    }

    // Independent scalar decoding above fixes every weight before multiplication.
    // This is the previous lane/reduction contract, not a second packed decoder.
    private static float FormerReduction(float[] weights, float[] input, int type, Gemma4InstructionSet isa)
    {
        if (isa == Gemma4InstructionSet.Avx512)
        {
            var a = Vector512<float>.Zero; var b = a; var c = a; var d = a;
            for (int i = 0; i < input.Length; i += 32)
            {
                var lo = Avx512F.Multiply(Vector512.LoadUnsafe(ref weights[i]), Vector512.LoadUnsafe(ref input[i]));
                var hi = Avx512F.Multiply(Vector512.LoadUnsafe(ref weights[i + 16]), Vector512.LoadUnsafe(ref input[i + 16]));
                if (type != 2 || (i & 32) == 0) { a = Avx512F.Add(a, lo); b = Avx512F.Add(b, hi); }
                else { c = Avx512F.Add(c, lo); d = Avx512F.Add(d, hi); }
            }
            return Vector512.Sum(Avx512F.Add(Avx512F.Add(a, b), Avx512F.Add(c, d)));
        }
        else
        {
            var a = Vector256<float>.Zero; var b = a; var c = a; var d = a;
            for (int i = 0; i < input.Length; i += 32)
            {
                a = Avx.Add(a, Avx.Multiply(Vector256.LoadUnsafe(ref weights[i]), Vector256.LoadUnsafe(ref input[i])));
                b = Avx.Add(b, Avx.Multiply(Vector256.LoadUnsafe(ref weights[i + 8]), Vector256.LoadUnsafe(ref input[i + 8])));
                c = Avx.Add(c, Avx.Multiply(Vector256.LoadUnsafe(ref weights[i + 16]), Vector256.LoadUnsafe(ref input[i + 16])));
                d = Avx.Add(d, Avx.Multiply(Vector256.LoadUnsafe(ref weights[i + 24]), Vector256.LoadUnsafe(ref input[i + 24])));
            }
            return Vector256.Sum(Avx.Add(Avx.Add(a, b), Avx.Add(c, d)));
        }
    }

    [Theory]
    [InlineData(1,1536)] [InlineData(2,1536)] [InlineData(2,12288)]
    [InlineData(14,1536)] [InlineData(14,8960)]
    public void SimdEncodedDotMatchesIndependentDoubleAccumulation(int type,int columns)
    {
        if(!Gemma4CpuMatrix.Supported)return;
        var encoding=GgufTensorEncoding.Get(type);var random=new Random(914+type);
        byte[] bytes=new byte[(int)encoding.RowBytes(columns)+2];random.NextBytes(bytes);
        // Misaligned row start, signed scales, mixed signs and magnitudes.
        var data=bytes.AsSpan(2);
        for(int block=0;block<data.Length;block+=encoding.BlockBytes)
        {
            int scale=block+(type==14?208:0);
            BinaryPrimitives.WriteUInt16LittleEndian(data[scale..],BitConverter.HalfToUInt16Bits((Half)((random.NextDouble()-.5)*.2)));
        }
        float[] weights=new float[columns],input=Enumerable.Range(0,columns).Select(_=>(float)((random.NextDouble()-.5)*4)).ToArray();
        encoding.Decode(data,weights);double oracle=0,magnitude=0;
        for(int i=0;i<columns;i++){double term=(double)weights[i]*input[i];oracle+=term;magnitude+=Math.Abs(term);}
        foreach(var isa in new[]{Gemma4InstructionSet.Avx2,Gemma4InstructionSet.Avx512})
        {
            if(isa==Gemma4InstructionSet.Avx512&&!System.Runtime.Intrinsics.X86.Avx512F.IsSupported)continue;
            float actual=Gemma4CpuMatrix.Dot(data,input,type,isa);
            Assert.True(Math.Abs(actual-oracle)<=Math.Max(2e-5,magnitude*2e-6),$"type={type}, expected={oracle}, actual={actual}");
        }
    }
    [Fact]
    public void AutoNeverChangesToResidualQuantizationAndUnsupportedIsaFails()
    {
        Assert.False(Gemma4VnniMatrix.IsResidual(Gemma4CpuMatrix.Resolve(Gemma4InstructionSet.Auto)));
        if(!System.Runtime.Intrinsics.X86.Avx512F.IsSupported)
            Assert.Throws<PlatformNotSupportedException>(()=>Gemma4CpuMatrix.Resolve(Gemma4InstructionSet.Avx512));
        foreach(var isa in new[]{Gemma4InstructionSet.AvxVnniResidual,Gemma4InstructionSet.Avx512VnniResidual})
            if(!Gemma4VnniMatrix.IsAvailable(isa))Assert.Throws<PlatformNotSupportedException>(()=>Gemma4CpuMatrix.Resolve(isa));
    }
    [Fact]
    public void GpuStateDoesNotAllocateHostKvAndClearsSelectedToken()
    {
        var state=new Gemma4E2BCpuModel.State(2048,1,default,true){NextToken=123};
        Assert.Empty(state.Caches);state.Dispose();Assert.Equal(0,state.NextToken);
        Assert.Equal(72,System.Runtime.InteropServices.Marshal.SizeOf<Gemma4CudaForward.Command>());
    }
    [Fact]
    public void ResidualPreparationRejectsNonFiniteInputs()
    {
        var input=new float[32];input[7]=float.NaN;
        Assert.Throws<ArithmeticException>(()=>new Gemma4VnniMatrix(input));
        Assert.Throws<ArgumentException>(()=>new Gemma4VnniMatrix(new float[31]));
    }
    [Fact]
    public void PrivateStateIsClearedBeforeGpuWipeAndWipeFailureIsReported()
    {
        var state=new Gemma4E2BCpuModel.State(64,1,default);Array.Fill(state.Logits,12f);state.Caches[0].Append(0,new float[256],new float[256]);
        int calls=0;state.Cleanup=()=>{calls++;Assert.All(state.Logits,x=>Assert.Equal(0,x));throw new InvalidOperationException("GPU wipe failed");};
        Assert.Throws<InvalidOperationException>(state.Dispose);state.Dispose();Assert.Equal(1,calls);
    }
}
