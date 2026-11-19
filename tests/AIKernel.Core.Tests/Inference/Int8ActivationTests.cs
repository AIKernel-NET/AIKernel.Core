using AIKernel.Kernel.Inference;

namespace AIKernel.Core.Tests.Inference;

public sealed class Int8ActivationTests
{
    [Fact]
    public void IndependentBlockScalesPreserveExtremaAndBoundRoundingError()
    {
        float[] x=Enumerable.Range(0,256).Select(i=>MathF.Sin(i*.17f)*(i<128?1:100)).ToArray();
        sbyte[] q=new sbyte[256];float[] scales=new float[2];int[] sums=new int[2];
        Int8CpuMatrix.Quantize(x,q,scales,sums);
        for(int block=0;block<2;block++)
        {
            Assert.Equal(q.Skip(block*128).Take(128).Sum(v=>(int)v),sums[block]);
            Assert.Contains(q.Skip(block*128).Take(128),v=>Math.Abs((int)v)==127);
            for(int j=0;j<128;j++)Assert.InRange(Math.Abs(x[block*128+j]-q[block*128+j]*scales[block]),0,scales[block]*.5001f);
        }
    }
    [Fact]
    public void ZeroSubnormalAndNonfiniteInputsHaveDefinedBehavior()
    {
        float[] x=new float[256];x[128]=float.Epsilon;
        sbyte[] q=new sbyte[256];float[] scales=new float[2];int[] sums=new int[2];
        Int8CpuMatrix.Quantize(x,q,scales,sums);
        Assert.Equal(0,scales[0]);Assert.Equal(0,sums[0]);Assert.Equal(float.Epsilon,scales[1]);Assert.Equal((sbyte)1,q[128]);
        x[0]=float.NaN;Assert.Throws<ArgumentException>(()=>Int8CpuMatrix.Quantize(x,q,scales,sums));
        x[0]=float.PositiveInfinity;Assert.Throws<ArgumentException>(()=>Int8CpuMatrix.Quantize(x,q,scales,sums));
    }
    [Fact]
    public void InvalidBufferSizesAreRejectedAndAutoNeverForcesAmx()
    {
        Assert.Throws<ArgumentException>(()=>Int8CpuMatrix.Quantize(new float[127],new sbyte[128],new float[1],new int[1]));
        Assert.Throws<ArgumentException>(()=>Int8CpuMatrix.Quantize(new float[128],new sbyte[127],new float[1],new int[1]));
        Assert.NotEqual("amx-int8-q8",Int8CpuMatrix.Resolve("q8-auto"));
        Assert.Contains(Int8CpuMatrix.Resolve("q8-auto"),CpuInt8Acceleration.AvailableBackends);
    }
}
