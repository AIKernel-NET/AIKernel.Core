using AIKernel.Kernel.Inference;

namespace AIKernel.Core.Tests.Inference;

public sealed class Qwen3WorkspaceTests
{
    private static Qwen3Workspace Create()=>new(32,64,64,32,2,32,128,2,8);

    [Fact]
    public void LargeThenSmallRequestClearsOwnedBuffersBeforeAnotherLease()
    {
        var state=Create();
        foreach(int used in new[]{6,2})
        {
            state.Acquire(TestContext.Current.CancellationToken,2);state.MarkUsed(used);
            foreach(var array in new[]{state.X,state.Normal,state.Q,state.K,state.V,state.Attention,state.Projected,state.Gate,state.Up,state.Logits,state.RopeCos,state.RopeSin})Array.Fill(array,17f);
            foreach(var array in state.Keys.Concat(state.Values))array.AsSpan(0,used*32).Fill(19f);
            foreach(var array in state.Scores)array.AsSpan(0,used).Fill(23f);
            state.MarkUsed(1);Assert.Equal(used,state.UsedTokens);
            Assert.Throws<InvalidOperationException>(()=>state.Acquire(TestContext.Current.CancellationToken,1));
            state.Dispose();Assert.False(state.InUse);Assert.Equal(0,state.UsedTokens);
            foreach(var array in new[]{state.X,state.Normal,state.Q,state.K,state.V,state.Attention,state.Projected,state.Gate,state.Up,state.Logits,state.RopeCos,state.RopeSin}.Concat(state.Keys).Concat(state.Values).Concat(state.Scores))
                Assert.All(array,value=>Assert.Equal(0,value));
        }
    }

    [Fact]
    public void CancellationClearsPartialKvAndReleasesTokenReference()
    {
        var state=Create();using var cancelled=new CancellationTokenSource();cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(()=>
        {
            using var lease=state.Acquire(cancelled.Token,1);
            state.MarkUsed(3);state.Keys[0][95]=7;state.Scores[0][2]=9;state.Logits[1]=11;
            state.Options.CancellationToken.ThrowIfCancellationRequested();
        });
        Assert.Equal(0,state.Keys[0][95]);Assert.Equal(0,state.Scores[0][2]);Assert.Equal(0,state.Logits[1]);
        Assert.False(state.Options.CancellationToken.CanBeCanceled);
        using var again=state.Acquire(TestContext.Current.CancellationToken,2);Assert.Equal(2,state.Options.MaxDegreeOfParallelism);
        Assert.Throws<InvalidOperationException>(()=>state.MarkUsed(9));
    }

    [Theory]
    [InlineData(0,1f)]
    [InlineData(17,1f)]
    [InlineData(2047,1.3f)]
    public void SharedTrigonometryMatchesOriginalPerHeadEvaluationBitForBit(int position,float amplitude)
    {
        using var state=Create().Acquire(TestContext.Current.CancellationToken,1);
        float[] frequencies=Enumerable.Range(0,16).Select(i=>MathF.Pow(1000000,-2f*i/32)).ToArray();
        float[] expected=Enumerable.Range(0,64).Select(i=>MathF.Sin(i*.17f)*100).ToArray();
        var actual=expected.ToArray();
        for(int h=0;h<2;h++)for(int i=0;i<16;i++)
        {
            float angle=position*frequencies[i],c=MathF.Cos(angle)*amplitude,s=MathF.Sin(angle)*amplitude;
            int a=h*32+i,b=a+16;float x=expected[a],y=expected[b];expected[a]=x*c-y*s;expected[b]=x*s+y*c;
        }
        state.PrepareRope(position,frequencies,amplitude);state.Rotate(actual,2,32);
        Assert.Equal(expected.Select(BitConverter.SingleToInt32Bits),actual.Select(BitConverter.SingleToInt32Bits));
    }
}
