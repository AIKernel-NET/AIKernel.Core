using AIKernel.Kernel.Inference;

namespace AIKernel.Core.Tests.Inference;

public sealed class CpuAutoTuningTests : IDisposable
{
    private readonly string directory=Path.Combine(Path.GetTempPath(),"aikernel-tuning-"+Guid.NewGuid().ToString("N"));
    private CpuAutoTuningOptions Options=>new(directory,new string('a',64));
    private static CpuKernelChoice[] Choices=>[new("avx2",4),new("avx2",1),new("avx512",4),new("avx512",1)];
    private CpuKernelTuner Create(string identity="fixture")=>new(Options,identity,Choices);
    private static double Timing(string phase,CpuKernelChoice choice)=>phase=="prefill"
        ?choice.Id=="avx512@4"?1:3
        :choice.Id=="avx2@1"?1:3;

    [Fact]
    public void SelectsIsaAndWorkerCountIndependentlyForEachPhase()
    {
        var result=Create().Tune(Timing,_=>true,false,TestContext.Current.CancellationToken);
        Assert.Equal("avx512",result.PrefillBackend);Assert.Equal(4,result.PrefillThreads);
        Assert.Equal("avx2",result.DecodeBackend);Assert.Equal(1,result.DecodeThreads);
        Assert.Equal("calibrated",result.Source);Assert.Equal(3,result.SamplesPerBackend);
    }
    [Fact]
    public void RestartReusesValidatedProfileWithoutRunningTimingProbes()
    {
        var first=Create().Tune(Timing,_=>true,false,TestContext.Current.CancellationToken);
        int probes=0,validations=0;
        var second=Create().Tune((p,b)=>{probes++;return 1;},_=>{validations++;return true;},false,TestContext.Current.CancellationToken);
        Assert.Equal("cache",second.Source);Assert.Equal(0,probes);Assert.Equal(2,validations);
        Assert.Equal(first.PrefillBackend,second.PrefillBackend);Assert.Equal(first.DecodeThreads,second.DecodeThreads);
    }
    [Theory]
    [InlineData("{broken")]
    [InlineData("null")]
    [InlineData("{\"Schema\":1,\"Identity\":\"fixture\",\"Prefill\":\"unsupported@64\"}")]
    public void MalformedOrUnsupportedCacheCannotSelectAKernel(string content)
    {
        Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"fixture.json"),content);
        var result=Create().Tune(Timing,_=>true,false,TestContext.Current.CancellationToken);
        Assert.Equal("calibrated",result.Source);Assert.Equal("avx512",result.PrefillBackend);
    }
    [Fact]
    public void RejectedArithmeticCandidateIsNeverBenchmarkedOrSelected()
    {
        var seen=new List<string>();
        var result=Create().Tune((phase,choice)=>{seen.Add(choice.Backend);return 1;},c=>c.Backend=="avx2",false,TestContext.Current.CancellationToken);
        Assert.DoesNotContain("avx512",seen);Assert.Equal("avx2",result.PrefillBackend);Assert.Equal("avx2",result.DecodeBackend);
    }
    [Fact]
    public void NearTieDoesNotCauseBackendChurn()
    {
        var result=Create().Tune((_,c)=>c.Id=="avx512@4"?0.98:1,_=>true,false,TestContext.Current.CancellationToken);
        Assert.Equal("avx2",result.PrefillBackend);Assert.Equal(4,result.PrefillThreads);
    }
    [Fact]
    public void RefreshLearnsNewTimingAndSavesOnlyAggregateOnlineMeasurements()
    {
        var tuner=Create();tuner.Tune((_,c)=>c.Id=="avx2@4"?1:2,_=>true,false,TestContext.Current.CancellationToken);
        tuner.Observe("decode","avx2@4",101,10,20);
        tuner.Observe("decode","avx2@4",900,10,40);
        CpuTuningStatus next=null!;
        for(int i=0;i<8;i++)next=tuner.Tune((_,c)=>c.Id=="avx512@1"?0.1:5,_=>true,true,TestContext.Current.CancellationToken);
        Assert.Equal("avx512",next.DecodeBackend);Assert.Equal(1,next.DecodeThreads);
        var json=File.ReadAllText(Path.Combine(directory,"fixture.json"));
        Assert.Contains("ContextBucket",json);Assert.Contains("EmaMillisecondsPerToken",json);
        Assert.DoesNotContain("prompt",json,StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public void CancellationDoesNotPublishAnIncompleteProfile()
    {
        using var cancel=CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);cancel.Cancel();
        Assert.Throws<OperationCanceledException>(()=>Create().Tune(Timing,_=>true,false,cancel.Token));
        Assert.False(File.Exists(Path.Combine(directory,"fixture.json")));
    }
    [Fact]
    public void IdentityChangesWhenModelOrResourceBudgetChanges()
    {
        string first=CpuKernelTuner.Identity(Options,4,1024,"shape");
        Assert.NotEqual(first,CpuKernelTuner.Identity(Options,2,1024,"shape"));
        Assert.NotEqual(first,CpuKernelTuner.Identity(Options with {ModelSha256=new string('b',64)},4,1024,"shape"));
    }
    [Fact]
    public void RowPartitionsCoverEveryRowAndUseActualCacheLineAddresses()
    {
        foreach(int rows in new[]{1,3,63,64,129,1024})foreach(int workers in new[]{1,2,4,6})
        for(nuint offset=0;offset<64;offset+=4)
        {
            nuint address=0x1000+offset;int previous=0;
            for(int worker=1;worker<workers;worker++)
            {
                int boundary=QuantizedCpuMatrix.RowBoundary(rows*worker/workers,rows,address);
                Assert.InRange(boundary,previous,rows);
                if(boundary<rows)Assert.Equal((nuint)0,(address+(nuint)boundary*4)%64);
                previous=boundary;
            }
            Assert.True(previous<=rows);
        }
    }
    public void Dispose(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
}
