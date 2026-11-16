namespace AIKernel.Core.Tests.Execution;

using AIKernel.Common.Results;

public sealed class CancellationClassifierCompatibilityTests
{
    [Fact]
    public void NullDoesNotRepresentCancellation() => Assert.False(CancellationExceptionClassifier.IsCancellation(null));

    [Theory]
    [InlineData("operation")]
    [InlineData("task")]
    [InlineData("derived")]
    public void GenericTryAndErrorContextKeepTheirPublishedProjection(string kind)
    {
        Exception error = kind switch { "task" => new TaskCanceledException("test"), "derived" => new DerivedCancellation(), _ => new OperationCanceledException("test") };
        Assert.True(CancellationExceptionClassifier.IsCancellation(error));
        var result = Try.Run<int>(() => throw error);
        Assert.True(result.IsFailure);
        Assert.Equal("UNHANDLED_EXCEPTION", result.Error!.Code);
        Assert.Equal("test", result.Error.Message);
        Assert.False(result.Error.IsRetryable);
        Assert.Null(result.Error.FailureKind);
        Assert.Equal(new KeyValuePair<string, string>(ResultMetadataKeys.ExceptionType, error.GetType().FullName!), Assert.Single(result.Error.Metadata!));
    }
    private sealed class DerivedCancellation() : OperationCanceledException("test");
}
