namespace AIKernel.Core.Tests.Execution;

using System.Collections.Immutable;
using AIKernel.Abstractions.Context;
using AIKernel.Abstractions.Execution;
using AIKernel.Abstractions.Models;
using AIKernel.Abstractions.Providers;
using AIKernel.Common.Results;
using AIKernel.Core.Context;
using AIKernel.Core.Execution;
using AIKernel.Core.Tests.Support;
using AIKernel.Core.Time;
using AIKernel.Dtos.Core;
using AIKernel.Dtos.Execution;
using AIKernel.Dtos.Kernel;
using AIKernel.Dtos.Routing;
using Xunit;

public sealed class KernelExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsSucceededResult_WhenProviderReturnsOutput()
    {
        var executor = new KernelExecutor(
            new FixedPromptGenerator(),
            new FixedCapabilityResolver(maxOutputTokens: 8),
            new SimpleTokenizer(),
            KernelClock.Replay(DateTimeOffset.UnixEpoch));

        var result = await executor.ExecuteAsync(
            new FakeModelProvider(output: "contract output"),
            CreateExecutionRequest(),
            TestContext.Current.CancellationToken);

        Assert.Equal(ExecutionStatus.Succeeded, result.Status);
        Assert.StartsWith("exec:sha256:", result.ExecutionId, StringComparison.Ordinal);
        Assert.Equal("fake-provider", result.ProviderId);
        Assert.Equal("gpt-test", result.ModelId);
        Assert.Equal("snapshot:executor", result.ContextSnapshotId);
        Assert.Equal("sha256:executor-context", result.ContextHash);
        Assert.Equal("sha256:executor-prompt", result.PromptHash);
        Assert.Equal("contract output", result.OutputText);
        Assert.Null(result.Error);
        Assert.Equal(PromptMessageFormat.ChatMessages.ToString(), result.Metadata[ExecutionMetadataKeys.MessageFormat]);
        Assert.Equal(OriginStep.Tokenizer.ToString(), result.Metadata[ReplayMetadataKeys.OriginStep]);
        Assert.Equal(SemanticSlot.T.ToString(), result.Metadata[ReplayMetadataKeys.SemanticSlot]);
        Assert.Equal("execute", result.Metadata[PipelineStepMetadataKeys.DeltaKind]);
        ReplayMetadataAssertions.AssertReplayMetadata(
            result.Metadata,
            "kernel.tokenizer.validate-output-budget",
            "6");
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsSameExecutionId_ForSameSemanticState()
    {
        var executor = new KernelExecutor(
            new FixedPromptGenerator(),
            new FixedCapabilityResolver(maxOutputTokens: 8),
            new SimpleTokenizer(),
            KernelClock.Replay(DateTimeOffset.UnixEpoch));

        var first = await executor.ExecuteAsync(
            new FakeModelProvider(output: "contract output"),
            CreateExecutionRequest(),
            TestContext.Current.CancellationToken);
        var second = await executor.ExecuteAsync(
            new FakeModelProvider(output: "contract output"),
            CreateExecutionRequest(),
            TestContext.Current.CancellationToken);

        Assert.Equal(first.ExecutionId, second.ExecutionId);
    }

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
                ContextSnapshotId = string.Empty,
                ContextHash = string.Empty,
                ContextBlocks = [],
                UserInstruction = "hello",
                PromptOptions = TestExecutionDefaults.PromptOptions,
                ExecutionOptions = TestExecutionDefaults.ExecutionOptions,
                RequestedModelId = "gpt-test"
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(ExecutionStatus.Failed, result.Status);
        Assert.Equal("exec:failed:error", result.ExecutionId);
        Assert.Equal("unknown", result.ContextSnapshotId);
        Assert.Equal("unknown", result.ContextHash);
        Assert.Equal("execution_failed", result.Error?.Code);
        Assert.Equal(FailureKind.FailClosed.ToString(), result.Metadata[ReplayMetadataKeys.FailureKind]);
        Assert.Equal(OriginStep.Capability.ToString(), result.Metadata[ReplayMetadataKeys.OriginStep]);
        Assert.Equal(SemanticSlot.T.ToString(), result.Metadata[ReplayMetadataKeys.SemanticSlot]);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsCanceledResult_WhenCapabilityResolutionIsCanceled()
    {
        var executor = new KernelExecutor(
            new UnusedPromptGenerator(),
            new CanceledCapabilityResolver(),
            new SimpleTokenizer(),
            KernelClock.Replay(DateTimeOffset.UnixEpoch));

        var result = await executor.ExecuteAsync(
            new FakeModelProvider(),
            new KernelExecutionRequest
            {
                ContextSnapshotId = string.Empty,
                ContextHash = string.Empty,
                ContextBlocks = [],
                UserInstruction = "hello",
                PromptOptions = TestExecutionDefaults.PromptOptions,
                ExecutionOptions = TestExecutionDefaults.ExecutionOptions,
                RequestedModelId = "gpt-test"
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(ExecutionStatus.Canceled, result.Status);
        Assert.Equal("canceled", result.Error?.Code);
        Assert.Equal("unknown", result.ContextSnapshotId);
        Assert.Equal("unknown", result.ContextHash);
        Assert.Equal(FailureKind.FailClosed.ToString(), result.Metadata[ReplayMetadataKeys.FailureKind]);
        Assert.Equal(OriginStep.Capability.ToString(), result.Metadata[ReplayMetadataKeys.OriginStep]);
        Assert.Equal(SemanticSlot.T.ToString(), result.Metadata[ReplayMetadataKeys.SemanticSlot]);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsFailedResult_WhenProviderReturnsEmptyOutput()
    {
        var executor = new KernelExecutor(
            new FixedPromptGenerator(),
            new FixedCapabilityResolver(maxOutputTokens: 8),
            new SimpleTokenizer(),
            KernelClock.Replay(DateTimeOffset.UnixEpoch));

        var result = await executor.ExecuteAsync(
            new FakeModelProvider(output: string.Empty),
            CreateExecutionRequest(),
            TestContext.Current.CancellationToken);

        Assert.Equal(ExecutionStatus.Failed, result.Status);
        Assert.Equal("empty_output", result.Error?.Code);
        Assert.Equal("snapshot:executor", result.ContextSnapshotId);
        Assert.Equal("sha256:executor-context", result.ContextHash);
        Assert.Equal(FailureKind.FailClosed.ToString(), result.Metadata[ReplayMetadataKeys.FailureKind]);
        Assert.Equal(OriginStep.Provider.ToString(), result.Metadata[ReplayMetadataKeys.OriginStep]);
        Assert.Equal(SemanticSlot.T.ToString(), result.Metadata[ReplayMetadataKeys.SemanticSlot]);
        Assert.Equal("execute", result.Metadata[PipelineStepMetadataKeys.DeltaKind]);
        ReplayMetadataAssertions.AssertReplayMetadata(
            result.Metadata,
            "kernel.provider.validate-output",
            "4");
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsFailedResult_WhenPromptGeneratorThrows()
    {
        var executor = new KernelExecutor(
            new FailingPromptGenerator(),
            new FixedCapabilityResolver(maxOutputTokens: 8),
            new SimpleTokenizer(),
            KernelClock.Replay(DateTimeOffset.UnixEpoch));

        var result = await executor.ExecuteAsync(
            new FakeModelProvider(output: "unused"),
            CreateExecutionRequest(),
            TestContext.Current.CancellationToken);

        Assert.Equal(ExecutionStatus.Failed, result.Status);
        Assert.Equal("execution_failed", result.Error?.Code);
        Assert.Equal("prompt failed", result.Error?.Message);
        Assert.Equal(FailureKind.FailClosed.ToString(), result.Metadata[ReplayMetadataKeys.FailureKind]);
        Assert.Equal(OriginStep.Prompt.ToString(), result.Metadata[ReplayMetadataKeys.OriginStep]);
        Assert.Equal(SemanticSlot.T.ToString(), result.Metadata[ReplayMetadataKeys.SemanticSlot]);
        Assert.Equal(
            typeof(InvalidOperationException).FullName,
            result.Metadata[ResultMetadataKeys.ExceptionType]);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsFailedResult_WhenProviderThrows()
    {
        var executor = new KernelExecutor(
            new FixedPromptGenerator(),
            new FixedCapabilityResolver(maxOutputTokens: 8),
            new SimpleTokenizer(),
            KernelClock.Replay(DateTimeOffset.UnixEpoch));

        var result = await executor.ExecuteAsync(
            new FakeModelProvider(
                output: "unused",
                exception: new InvalidOperationException("provider failed")),
            CreateExecutionRequest(),
            TestContext.Current.CancellationToken);

        Assert.Equal(ExecutionStatus.Failed, result.Status);
        Assert.Equal("execution_failed", result.Error?.Code);
        Assert.Equal("provider failed", result.Error?.Message);
        Assert.Equal("snapshot:executor", result.ContextSnapshotId);
        Assert.Equal("sha256:executor-context", result.ContextHash);
        Assert.Equal(FailureKind.FailClosed.ToString(), result.Metadata[ReplayMetadataKeys.FailureKind]);
        Assert.Equal(OriginStep.Provider.ToString(), result.Metadata[ReplayMetadataKeys.OriginStep]);
        Assert.Equal(SemanticSlot.T.ToString(), result.Metadata[ReplayMetadataKeys.SemanticSlot]);
        Assert.Equal(
            typeof(InvalidOperationException).FullName,
            result.Metadata[ResultMetadataKeys.ExceptionType]);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsFailedResult_WhenOutputTokenBudgetIsExceeded()
    {
        var executor = new KernelExecutor(
            new FixedPromptGenerator(),
            new FixedCapabilityResolver(maxOutputTokens: 1),
            new SimpleTokenizer(),
            KernelClock.Replay(DateTimeOffset.UnixEpoch));

        var result = await executor.ExecuteAsync(
            new FakeModelProvider(output: "two tokens"),
            CreateExecutionRequest(),
            TestContext.Current.CancellationToken);

        Assert.Equal(ExecutionStatus.Failed, result.Status);
        Assert.Equal("output_token_budget_exceeded", result.Error?.Code);
        Assert.Equal("snapshot:executor", result.ContextSnapshotId);
        Assert.Equal("sha256:executor-context", result.ContextHash);
        Assert.Equal(FailureKind.FailClosed.ToString(), result.Metadata[ReplayMetadataKeys.FailureKind]);
        Assert.Equal(OriginStep.Tokenizer.ToString(), result.Metadata[ReplayMetadataKeys.OriginStep]);
        Assert.Equal(SemanticSlot.T.ToString(), result.Metadata[ReplayMetadataKeys.SemanticSlot]);
        Assert.Equal("execute", result.Metadata[PipelineStepMetadataKeys.DeltaKind]);
        ReplayMetadataAssertions.AssertReplayMetadata(
            result.Metadata,
            "kernel.tokenizer.validate-output-budget",
            "6");
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsStructuredMetadata_WhenExecutionIdGenerationFails()
    {
        var executor = new KernelExecutor(
            new NullContextPromptGenerator(),
            new FixedCapabilityResolver(maxOutputTokens: 8),
            new SimpleTokenizer(),
            KernelClock.Replay(DateTimeOffset.UnixEpoch));

        var result = await executor.ExecuteAsync(
            new FakeModelProvider(output: "contract output"),
            new KernelExecutionRequest
            {
                ContextSnapshotId = string.Empty,
                ContextHash = string.Empty,
                ContextBlocks = [],
                UserInstruction = "hello",
                PromptOptions = TestExecutionDefaults.PromptOptions,
                ExecutionOptions = TestExecutionDefaults.ExecutionOptions,
                RequestedModelId = "gpt-test"
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(ExecutionStatus.Failed, result.Status);
        Assert.Equal("execution_id_generation_failed", result.Error?.Code);
        Assert.Equal(FailureKind.FailClosed.ToString(), result.Metadata[ReplayMetadataKeys.FailureKind]);
        Assert.Equal(OriginStep.SemanticHash.ToString(), result.Metadata[ReplayMetadataKeys.OriginStep]);
        Assert.Equal(SemanticSlot.B.ToString(), result.Metadata[ReplayMetadataKeys.SemanticSlot]);
        Assert.Equal("ERROR", result.Metadata[ResultMetadataKeys.SourceErrorCode]);
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

    private sealed class CanceledCapabilityResolver : IModelPromptCapabilityResolver
    {
        public ModelPromptCapability Resolve(
            IModelProvider provider,
            KernelExecutionRequest request)
        {
            throw new OperationCanceledException();
        }
    }

    private sealed class FixedCapabilityResolver : IModelPromptCapabilityResolver
    {
        private readonly int _maxOutputTokens;

        public FixedCapabilityResolver(int maxOutputTokens)
        {
            _maxOutputTokens = maxOutputTokens;
        }

        public ModelPromptCapability Resolve(
            IModelProvider provider,
            KernelExecutionRequest request)
        {
            return new ModelPromptCapability
            {
                ProviderId = provider.ProviderId,
                ModelId = request.RequestedModelId ?? "gpt-test",
                MessageFormat = PromptMessageFormat.ChatMessages,
                MaxInputTokens = 128,
                MaxOutputTokens = _maxOutputTokens,
                SupportedRoles = ["user"],
                SupportsSystemMessages = true,
                SystemInstructionRole = "system"
            };
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

    private sealed class FailingPromptGenerator : IPromptGenerator
    {
        public Task<GeneratedPrompt> GenerateAsync(
            PromptGenerationRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("prompt failed");
        }
    }

    private sealed class NullContextPromptGenerator : IPromptGenerator
    {
        public Task<GeneratedPrompt> GenerateAsync(
            PromptGenerationRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new GeneratedPrompt
            {
                PromptId = "prompt:null-context",
                PromptHash = "sha256:null-context-prompt",
                ContextSnapshotId = "unknown",
                ContextHash = "unknown",
                Capability = request.Capability,
                Messages =
                [
                    new AIKernel.Dtos.Execution.ModelMessage("user", request.UserInstruction)
                ],
                EstimatedInputTokens = 1,
                Metadata = ImmutableDictionary<string, string>.Empty
            });
        }
    }

    private sealed class FixedPromptGenerator : IPromptGenerator
    {
        public Task<GeneratedPrompt> GenerateAsync(
            PromptGenerationRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(new GeneratedPrompt
            {
                PromptId = "prompt:executor",
                PromptHash = "sha256:executor-prompt",
                ContextSnapshotId = request.ContextSnapshotId,
                ContextHash = request.ContextHash,
                Capability = request.Capability,
                Messages =
                [
                    new AIKernel.Dtos.Execution.ModelMessage("user", request.UserInstruction)
                ],
                EstimatedInputTokens = 1,
                Metadata = ImmutableDictionary<string, string>.Empty
            });
        }
    }

    private sealed class FakeModelProvider : IModelProvider
    {
        private readonly string _output;
        private readonly Exception? _exception;

        public FakeModelProvider(
            string output = "contract output",
            Exception? exception = null)
        {
            _output = output;
            _exception = exception;
        }

        public string ProviderId => "fake-provider";

        public string Name => "Fake Provider";

        public string Version => "0.0.4";

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
            if (_exception is not null)
            {
                throw _exception;
            }

            return Task.FromResult(_output);
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

    private static KernelExecutionRequest CreateExecutionRequest()
    {
        return new KernelExecutionRequest
        {
            ContextSnapshotId = "snapshot:executor",
            ContextHash = "sha256:executor-context",
            ContextBlocks = [],
            UserInstruction = "hello",
            PromptOptions = TestExecutionDefaults.PromptOptions,
            ExecutionOptions = TestExecutionDefaults.ExecutionOptions,
            RequestedModelId = "gpt-test"
        };
    }
}
