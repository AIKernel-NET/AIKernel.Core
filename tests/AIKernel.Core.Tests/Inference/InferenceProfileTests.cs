using AIKernel.Kernel.Inference;

namespace AIKernel.Core.Tests.Inference;

public sealed class InferenceProfileTests
{
    [Fact]
    public void PhasesAreSeparateAndSnapshotsDoNotAliasLiveCounters()
    {
        var p=new InferenceProfiler {Phase=InferencePhase.Prefill};
        p.Add(InferenceStage.Matrix,p.Start());
        p.Add(InferenceStage.Matrix,p.Start());
        p.Phase=InferencePhase.Decode;p.Add(InferenceStage.Matrix,p.Start());
        p.Add(InferenceStage.Quantization,p.Start());
        var first=p.Snapshot(128,256,2,4,2,32,64,"avx2-int8-q8","q8");
        p.Add(InferenceStage.Matrix,p.Start());
        Assert.Equal(2,first.Stages["Prefill.Matrix"].Calls);
        Assert.Equal(1,first.Stages["Decode.Matrix"].Calls);
        Assert.Equal(1,first.Stages["Decode.Quantization"].Calls);
        Assert.DoesNotContain("Prefill.Quantization",first.Stages.Keys);
        Assert.All(first.Stages.Values,v=>Assert.True(v.Milliseconds>=0));
        Assert.True(first.RequestMilliseconds>=0);
        Assert.Equal("avx2-int8-q8",first.CpuBackend);
        Assert.Equal(128,first.Width);
    }
}
