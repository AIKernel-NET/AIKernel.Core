using AIKernel.Kernel.Inference;

namespace AIKernel.Core.Tests.Inference;

public sealed class PreparedQ8ActivationTests
{
    [Fact]
    public void SharedBytesScalesSumsAndRoundedValuesMatchTheOriginalQuantizer()
    {
        float[] input=Enumerable.Range(0,384).Select(i=>MathF.Sin(i*.17f)*(i<128?1:100)).ToArray();
        var q=new sbyte[384];var scales=new float[3];var sums=new int[3];
        Int8CpuMatrix.Quantize(input,q,scales,sums);
        using var prepared=new PreparedQ8Activation(input,384,true);
        Assert.Equal(q,prepared.Quantized.Take(384));Assert.Equal(scales,prepared.Scales.Take(3));Assert.Equal(sums,prepared.Sums.Take(3));
        Assert.Equal(q.Select((v,i)=>BitConverter.SingleToInt32Bits(v*scales[i/128])),prepared.Rounded!.Take(384).Select(BitConverter.SingleToInt32Bits));
        for(int projection=0;projection<3;projection++)prepared.Validate(input,384,rounded:true);
        Assert.Throws<ArgumentException>(()=>prepared.Validate(input.ToArray(),384));
        Assert.Throws<ArgumentException>(()=>prepared.Validate(input,128));
    }
    [Fact]
    public void ANewLeaseRequantizesMutatedInputAndDisposedLeasesAreRejected()
    {
        var input=new float[128];input[0]=1;
        var before=new PreparedQ8Activation(input,128,false);float scale=before.Scales[0];before.Dispose();before.Dispose();
        Assert.Throws<ObjectDisposedException>(()=>before.Validate(input,128));
        input[0]=100;
        using var after=new PreparedQ8Activation(input,128,false);
        Assert.True(after.Scales[0]>scale*99);Assert.Null(after.Rounded);
        Assert.Throws<ArgumentException>(()=>after.Validate(input,128,rounded:true));
    }
    [Fact]
    public void SharedPreparationIsCountedOnceOutsideMatrixTiming()
    {
        var profiler=new InferenceProfiler {Phase=InferencePhase.Prefill};var x=new float[128];
        using var prepared=new PreparedQ8Activation(x,128,false,profiler,InferenceStage.SharedQuantization);
        prepared.Validate(x,128);prepared.Validate(x,128);prepared.Validate(x,128);
        var result=profiler.Snapshot(128,128,1,1,1,128,64,"avx2-int8-q8","q8");
        Assert.Equal(1,result.Stages["Prefill.SharedQuantization"].Calls);
        Assert.DoesNotContain("Prefill.Quantization",result.Stages.Keys);
    }
}
