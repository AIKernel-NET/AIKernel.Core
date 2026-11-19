using AIKernel.Kernel.Inference;

namespace AIKernel.Core.Tests.Inference;

public sealed class CudaQ8SelectionTests
{
    [Fact]
    public void StableAdvantageSelectsTensorAndRetainsEvidence()
    {
        double[] dp4a=[10,11,10,12,10,11,10,12,11],tensor=dp4a.Select(t=>t*.8).ToArray();
        var result=CudaQ8Selection.Choose(dp4a,tensor);
        Assert.Equal("tensor-core-int8",result.SelectedKernel);
        Assert.InRange(result.MedianPairedRatio,.799,.801);
        Assert.Equal(9,result.PairedRatios.Count);
        tensor[0]=1000;Assert.InRange(result.PairedRatios[0],.799,.801);
    }
    [Theory]
    [InlineData(.92,false)]
    [InlineData(.8,true)]
    public void MarginalOrInconsistentAdvantageKeepsDp4a(double ratio,bool noisy)
    {
        var a=Enumerable.Repeat(10d,9).ToArray();var b=a.Select(x=>x*ratio).ToArray();
        if(noisy)b[3]=b[6]=11;
        Assert.Equal("dp4a",CudaQ8Selection.Choose(a,b).SelectedKernel);
    }
    [Fact]
    public void InvalidClockSamplesCannotBecomeAWinner()
    {
        var a=Enumerable.Repeat(10d,9).ToArray();var b=a.ToArray();b[0]=0;
        Assert.Throws<ArgumentException>(()=>CudaQ8Selection.Choose(a,b));
        b[0]=double.NaN;Assert.Throws<ArgumentException>(()=>CudaQ8Selection.Choose(a,b));
    }
    [Fact]
    public void OneSchedulingOutlierDoesNotOverrideRepeatedPairedWins()
    {
        var a=Enumerable.Repeat(10d,9).ToArray();var b=Enumerable.Repeat(8d,9).ToArray();b[2]=100;
        Assert.Equal("tensor-core-int8",CudaQ8Selection.Choose(a,b).SelectedKernel);
    }
}
