using AIKernel.Common.Results;

namespace AIKernel.Core.Tests.Common;

public sealed class AsyncTests
{
    [Fact]
    public async Task AsyncSupportsLinqQueryComposition()
    {
        var pipeline =
            from left in Async<int>.FromValue(2)
            from right in Async<int>.FromTask(_ => Task.FromResult(3))
            select left + right;

        var result = await pipeline.RunAsync(TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccessState);
        Assert.Equal(5, result.Value);
    }

    [Fact]
    public async Task AsyncCapturesTaskExceptionsAsResultFailure()
    {
        var pipeline = Async<int>.FromTask(_ => throw new InvalidOperationException("blocked"));

        var result = await pipeline.RunAsync(TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Contains("blocked", result.Error!.Message);
    }
}
