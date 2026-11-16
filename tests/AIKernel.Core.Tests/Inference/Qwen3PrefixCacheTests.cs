using AIKernel.Kernel.Inference;

namespace AIKernel.Core.Tests.Inference;

public sealed class Qwen3PrefixCacheTests
{
    [Fact]
    public void OnlyTrustedPrefixRangeIsCopiedAndEvictionWipesIt()
    {
        using var cache=new Qwen3PrefixCache(2,96);
        float[][] keys=[[1,2,3,4,99,99]],values=[[5,6,7,8,99,99]];
        cache.Store("logos",2,keys,values,2);var first=cache.Get("logos")!;
        Assert.Equal(new float[]{1,2,3,4},first.Keys[0]);Assert.DoesNotContain(99f,first.Values[0]);
        Array.Fill(keys[0],17);Assert.Equal(1,first.Keys[0][0]);
        cache.Store("ethos",2,keys,values,2);cache.Get("logos");
        var ethos=cache.Get("ethos")!;cache.Get("logos");cache.Store("pathos",2,keys,values,2);
        Assert.Null(cache.Get("ethos"));Assert.All(ethos.Keys[0],v=>Assert.Equal(0,v));Assert.All(ethos.Values[0],v=>Assert.Equal(0,v));
        Assert.NotNull(cache.Get("logos"));Assert.Equal(64,cache.Statistics.Bytes);Assert.Equal(1,cache.Statistics.Evictions);
        cache.Dispose();Assert.All(first.Keys[0],v=>Assert.Equal(0,v));Assert.Equal(0,cache.Statistics.Bytes);
    }
    [Fact]
    public void ByteBudgetAndDisabledCacheDoNotRetainOversizedPrefixes()
    {
        float[][] keys=[[1,2,3,4]],values=[[5,6,7,8]];
        using var cache=new Qwen3PrefixCache(3,48);
        Assert.True(cache.Store("a",2,keys,values,2));var previous=cache.Get("a")!;
        Assert.True(cache.Store("b",2,keys,values,2));Assert.All(previous.Keys[0],v=>Assert.Equal(0,v));
        Assert.Equal(1,cache.Statistics.Entries);Assert.Equal(32,cache.Statistics.Bytes);
        using var disabled=new Qwen3PrefixCache(1,0);Assert.False(disabled.Store("a",2,keys,values,2));Assert.Equal(0,disabled.Statistics.Entries);
        Assert.Throws<ArgumentException>(()=>cache.Store("invalid",3,keys,values,2));
    }
}
