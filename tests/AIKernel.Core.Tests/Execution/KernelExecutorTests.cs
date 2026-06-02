namespace AIKernel.Core.Tests.Execution;

using AIKernel.Abstractions.Execution;
using AIKernel.Abstractions.Models;
using AIKernel.Abstractions.Providers;
using AIKernel.Core.Execution;
using AIKernel.Core.Time;
using AIKernel.Dtos.Core;
using AIKernel.Dtos.Execution;
using AIKernel.Dtos.Kernel;
using AIKernel.Dtos.Routing;
using Xunit;

public sealed class KernelExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsFailedResult_WhenContextSnapshotIsMissing()
    {
        var executor = new KernelExecutor(
            new UnusedPromptGenerator(),
            new FailingCapabilityResolver(),
            new SimpleTokenizer(),
            KernelClock.Replay(DateTimeOffset.UnixEpoch));

        var result = await executor.ExecuteAsync(
            new FakeModelProvider(),
            new KernelExecutionRequest
            {
                ContextSnapshot = null!,
                UserInstruction = "hello",
                PromptOptions = PromptGenerationOptions.Default,
                ExecutionOptions = ExecutionOptions.DeterministicDefault,
                RequestedModelId = "gpt-test"
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(ExecutionStatus.Failed, result.Status);
        Assert.Equal("exec:failed:error", result.ExecutionId);
        Assert.Equal("unknown", result.ContextSnapshotId);
        Assert.Equal("unknown", result.ContextHash);
        Assert.Equal("execution_failed", result.Error?.Code);
    }

    private sealed class FailingCapabilityResolver : IModelPromptCapabilityResolver
    {
        public ModelPromptCapability Resolve(
            IModelProvider provider,
            KernelExecutionRequest request)
        {
            throw new UnsupportedPromptCapabilityException("Capability resolution failed.");
        }
    }

    private sealed class UnusedPromptGenerator : IPromptGenerator
    {
        public Task<GeneratedPrompt> GenerateAsync(
            PromptGenerationRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Prompt generation should not run.");
        }
    }

    private sealed class FakeModelProvider : IModelProvider
    {
        public string ProviderId => "fake-provider";

        public string Name => "Fake Provider";

        public string Version => "0.0.3";

        public IProviderCapabilities GetCapabilities()
        {
            return new FakeProviderCapabilities();
        }

        public Task<bool> IsAvailableAsync()
        {
            return Task.FromResult(true);
        }

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public Task ShutdownAsync()
        {
            return Task.CompletedTask;
        }

        public Task<ProviderHealthStatus> GetHealthAsync()
        {
            return Task.FromResult(new ProviderHealthStatus(
                IsHealthy: true,
                Message: "OK",
                CheckedAt: DateTime.UnixEpoch,
                ResponseTimeMs: 0));
        }

        public Task<string> GenerateAsync(
            IReadOnlyList<IModelMessage> messages,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult("contract output");
        }

        public Task StreamGenerateAsync(
            IReadOnlyList<IModelMessage> messages,
            Func<string, Task> onChunk,
            CancellationToken cancellationToken = default)
        {
            return onChunk("contract output");
        }

        public Task<string> AnswerAsync(
            string question,
            string? context = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult("contract output");
        }
    }

    private sealed class FakeProviderCapabilities : IProviderCapabilities
    {
        public IReadOnlyList<string> SupportedOperations => [];

        public IReadOnlyList<string> SupportedDataTypes => [];

        public int MaxConcurrentConnections => 1;

        public RateLimitInfo? RateLimit => null;

        public ModelCapacityVector Vector => new();

        public IDictionary<string, float>? GetDynamicCapacities(
            IExecutionConstraints constraints)
        {
            return null;
        }

        public ICapabilityProfile? GetCapabilityProfile()
        {
            return null;
        }

        public bool SupportsOperation(string operation)
        {
            return false;
        }

        public bool SupportsDataType(string dataType)
        {
            return false;
        }

        public bool SupportsQuantization(string quantizationLevel)
        {
            return false;
        }

        public bool SupportsQueryAugmentation => false;

        public bool SupportsQueryDecomposition => false;

        public bool SupportsQueryRouting => false;

        public int MaxQueryParts => 0;

        public IReadOnlyList<string> SupportedQueryProcessingOperations => [];

        public bool SupportsQueryProcessingOperation(string operation)
        {
            return false;
        }

        public bool SupportsEmbedding => false;

        public int? EmbeddingDimensions => null;

        public IReadOnlyList<string> SupportedEmbeddingModels => [];
    }
}
