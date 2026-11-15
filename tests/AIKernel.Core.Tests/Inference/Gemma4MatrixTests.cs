using AIKernel.Kernel.Inference;
using System.Buffers.Binary;

namespace AIKernel.Core.Tests.Inference;

public sealed class Gemma4MatrixTests
{
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
        {
            float actual=Gemma4CpuMatrix.Dot(data,input,type);
            Assert.True(Math.Abs(actual-oracle)<=Math.Max(2e-5,magnitude*2e-6),$"type={type}, expected={oracle}, actual={actual}");
        }
    }
    [Fact]
    public void PrivateStateIsClearedBeforeGpuWipeAndWipeFailureIsReported()
    {
        var state=new Gemma4E2BCpuModel.State(64,1,default);Array.Fill(state.Logits,12f);state.Caches[0].Append(0,new float[256],new float[256]);
        int calls=0;state.Cleanup=()=>{calls++;Assert.All(state.Logits,x=>Assert.Equal(0,x));throw new InvalidOperationException("GPU wipe failed");};
        Assert.Throws<InvalidOperationException>(state.Dispose);state.Dispose();Assert.Equal(1,calls);
    }
}
