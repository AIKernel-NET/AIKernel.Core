namespace AIKernel.Core.Tests.Kernel;

using AIKernel.Abstractions.Kernel;
using AIKernel.Dtos.Execution;
using AIKernel.Dtos.Kernel;
using AIKernel.Dtos.Rom;
using Xunit;

public abstract class KernelContractTests
{
    protected abstract IKernel CreateKernel();

    protected abstract KernelRequest CreateValidRequest();

    protected abstract KernelRequest CreateRequestWithDeniedRom();

    protected abstract KernelRequest CreateRequestWithInvalidSignature();

    [Fact]
    public async Task ExecuteAsync_ReturnsSucceededResult_WhenTransactionCompletes()
    {
        // Arrange
        var kernel = CreateKernel();
        var request = CreateValidRequest();

        // Act
        var result = await kernel.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ExecutionStatus.Succeeded, result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.ExecutionId));
        Assert.False(string.IsNullOrWhiteSpace(result.ContextSnapshotId));
        Assert.False(string.IsNullOrWhiteSpace(result.ContextHash));
        Assert.False(string.IsNullOrWhiteSpace(result.PromptHash));
        Assert.False(string.IsNullOrWhiteSpace(result.OutputText));
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsRejectedResult_WhenGovernanceDeniesContext()
    {
        // Arrange
        var kernel = CreateKernel();
        var request = CreateRequestWithDeniedRom();

        // Act
        var result = await kernel.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ExecutionStatus.Rejected, result.Status);
        Assert.Null(result.OutputText);
        Assert.NotNull(result.Error);
        Assert.Equal("context_rejected", result.Error.Code);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsRejectedResult_WhenRomSignatureIsInvalid()
    {
        // Arrange
        var kernel = CreateKernel();
        var request = CreateRequestWithInvalidSignature();

        // Act
        var result = await kernel.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ExecutionStatus.Rejected, result.Status);
        Assert.Null(result.OutputText);
        Assert.NotNull(result.Error);
        Assert.Equal("rom_signature_verification_failed", result.Error.Code);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsCanceledResult_WhenCancellationIsRequested()
    {
        // Arrange
        var kernel = CreateKernel();
        var request = CreateValidRequest();

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        var result = await kernel.ExecuteAsync(request, cts.Token);

        // Assert
        Assert.Equal(ExecutionStatus.Canceled, result.Status);
        Assert.Null(result.OutputText);
        Assert.NotNull(result.Error);
        Assert.Equal("canceled", result.Error.Code);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotLeakTransactionState_BetweenCalls()
    {
        // Arrange
        var kernel = CreateKernel();
        var request = CreateValidRequest();

        // Act
        var first = await kernel.ExecuteAsync(request, TestContext.Current.CancellationToken);
        var second = await kernel.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(first.ContextHash, second.ContextHash);
        Assert.Equal(first.PromptHash, second.PromptHash);
        Assert.NotEqual(first.ExecutionId, second.ExecutionId);
    }
}
