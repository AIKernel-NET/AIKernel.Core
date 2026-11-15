using AIKernel.Kernel.Inference;

namespace AIKernel.Core.Tests.Inference;

public sealed class InferenceVectorMathTests
{
    [Theory]
    [InlineData(1)] [InlineData(7)] [InlineData(8)] [InlineData(15)] [InlineData(16)]
    [InlineData(31)] [InlineData(32)] [InlineData(63)] [InlineData(64)] [InlineData(129)]
    [InlineData(2048)] [InlineData(6144)] [InlineData(9728)]
    public void DotAndNormMatchDoubleOracleIncludingTailsAndInPlaceNorm(int n)
    {
        var random=new Random(n);var x=Enumerable.Range(0,n).Select(i=>(float)(random.NextDouble()*2-1)).ToArray();
        var weights=Enumerable.Range(0,n).Select(i=>(float)(random.NextDouble()*2-1)).ToArray();
        double expected=0,bound=0,squares=0;
        for(int i=0;i<n;i++){double term=(double)x[i]*weights[i];expected+=term;bound+=Math.Abs(term);squares+=(double)x[i]*x[i];}
        Assert.True(Math.Abs(InferenceVectorMath.Dot(x,weights)-expected)<=2e-6*bound+1e-6);
        var actual=x.ToArray();InferenceVectorMath.Norm(actual,weights,actual,1e-6f);
        double scale=1/Math.Sqrt(squares/n+1e-6);
        for(int i=0;i<n;i++)Close(x[i]*scale*weights[i],actual[i]);
    }
    [Theory]
    [InlineData(1)] [InlineData(7)] [InlineData(8)] [InlineData(15)] [InlineData(16)]
    [InlineData(31)] [InlineData(32)] [InlineData(65)] [InlineData(6144)] [InlineData(9728)]
    public void SwiGluAndResidualMatchOracleIncludingExtremeInputs(int n)
    {
        var x=Enumerable.Range(0,n).Select(i=>(i%61-30)*4f).ToArray();var y=Enumerable.Range(0,n).Select(i=>MathF.Sin(i*.17f)).ToArray();
        var actual=x.ToArray();InferenceVectorMath.SwiGlu(actual,y);
        for(int i=0;i<n;i++)Close(x[i]/(1+Math.Exp(-x[i]))*y[i],actual[i]);
        x.CopyTo(actual,0);InferenceVectorMath.Add(actual,y);
        for(int i=0;i<n;i++)Assert.Equal(BitConverter.SingleToInt32Bits(x[i]+y[i]),BitConverter.SingleToInt32Bits(actual[i]));
    }
    [Fact]
    public void RejectsMismatchedDimensions()
    {
        Assert.Throws<ArgumentException>(()=>InferenceVectorMath.Dot(new float[2],new float[1]));
        Assert.Throws<ArgumentException>(()=>InferenceVectorMath.Norm(new float[2],new float[1],new float[2],1e-6f));
        Assert.Throws<ArgumentException>(()=>InferenceVectorMath.SwiGlu(new float[2],new float[1]));
        Assert.Throws<ArgumentException>(()=>InferenceVectorMath.Add(new float[2],new float[1]));
    }
    private static void Close(double expected,float actual)
        =>Assert.True(float.IsFinite(actual)&&Math.Abs(actual-expected)<=5e-6+2e-5*Math.Abs(expected),$"Expected {expected:R}, actual {actual:R}");
}
