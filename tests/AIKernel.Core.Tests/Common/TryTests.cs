namespace AIKernel.Core.Tests.Common;

using AIKernel.Common.Results;
using Xunit;

public sealed class TryTests
{
    [Fact]
    public void Run_ReturnsSuccess_WhenFunctionReturns()
    {
        var result = Try.Run(() => 42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Run_ReturnsFailure_WhenFunctionThrows()
    {
        var result = Try.Run<int>(() => throw new InvalidOperationException("boom"));

        Assert.True(result.IsFailure);
        Assert.Equal("boom", result.Error!.Message);
        Assert.Equal("UNHANDLED_EXCEPTION", result.Error.Code);
    }

    [Fact]
    public async Task RunAsync_ReturnsSuccess_WhenFunctionReturns()
    {
        var result = await Try.RunAsync(() => Task.FromResult(42));

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public async Task RunAsync_ReturnsFailure_WhenFunctionThrows()
    {
        var result = await Try.RunAsync<int>(
            () => throw new InvalidOperationException("async-boom"));

        Assert.True(result.IsFailure);
        Assert.Equal("async-boom", result.Error!.Message);
        Assert.Equal("UNHANDLED_EXCEPTION", result.Error.Code);
    }

    [Fact]
    public void ExtensionMap_DelegatesToResultMap()
    {
        var result = Result<int>.Success(4).Map(x => x * 2);

        Assert.True(result.IsSuccess);
        Assert.Equal(8, result.Value);
    }

    [Fact]
    public void ExtensionBind_DelegatesToResultBind()
    {
        var result = Result<int>
            .Success(4)
            .Bind(x => Result<string>.Success($"value:{x}"));

        Assert.True(result.IsSuccess);
        Assert.Equal("value:4", result.Value);
    }
}
