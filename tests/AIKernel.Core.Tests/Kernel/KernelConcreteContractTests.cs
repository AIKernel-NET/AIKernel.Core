namespace AIKernel.Core.Tests.Kernel;

using System.Collections.Immutable;
using AIKernel.Abstractions.Context;
using AIKernel.Abstractions.Execution;
using AIKernel.Abstractions.Kernel;
using AIKernel.Abstractions.Models;
using AIKernel.Abstractions.Providers;
using AIKernel.Common.Results;
using AIKernel.Core.Context;
using AIKernel.Core.Rom;
using AIKernel.Core.Tests.Support;
using AIKernel.Kernel;
using AIKernel.Dtos.Core;
using AIKernel.Dtos.Context;
using AIKernel.Dtos.Execution;
using AIKernel.Dtos.Kernel;
using AIKernel.Dtos.Rom;
using AIKernel.Dtos.Routing;
using AIKernel.Dtos.Vfs;
using AIKernel.Enums;
using AIKernel.Vfs;

public sealed class KernelConcreteContractTests : KernelContractTests
{
    protected override AIKernel.Kernel.Kernel CreateKernel()
    {
        var requestHasher = new AIKernel.Kernel.KernelRequestHasher();

        return new AIKernel.Kernel.Kernel(
            new FakeVfsSessionFactory(),
            new FakeContextAssembler(),
            new FakeModelProviderSelector(),
            new FakeKernelExecutor(),
            requestHasher,
            new AIKernel.Kernel.KernelTransactionIdFactory(requestHasher));
    }

    protected override KernelRequest CreateValidRequest()
    {
        return CreateRequest("valid-rom");
    }

    protected override KernelRequest CreateRequestWithDeniedRom()
    {
        return CreateRequest("denied-rom");
    }

    protected override KernelRequest CreateRequestWithInvalidSignature()
    {
        return CreateRequest("invalid-signature-rom");
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsFailedMetadata_WhenExecutorFails()
    {
        var kernel = CreateKernel(new FailingKernelExecutor());

        var result = await kernel.ExecuteAsync(
            CreateValidRequest(),
            TestContext.Current.CancellationToken);

        Assert.Equal(ExecutionStatus.Failed, result.Status);
        Assert.Equal("kernel_transaction_failed", result.Error?.Code);
        Assert.Equal("fake-provider", result.ProviderId);
        Assert.Equal("fake-provider", result.Metadata[KernelFacadeMetadataKeys.ProviderId]);
        Assert.Equal(FailureKind.FailClosed.ToString(), result.Metadata[ReplayMetadataKeys.FailureKind]);
        Assert.Equal(OriginStep.KernelFacade.ToString(), result.Metadata[ReplayMetadataKeys.OriginStep]);
        Assert.Equal(SemanticSlot.T.ToString(), result.Metadata[ReplayMetadataKeys.SemanticSlot]);
        ReplayMetadataAssertions.AssertReplayMetadata(
            result.Metadata,
            "kernel.facade.fail",
            "1");
    }

    [Fact]
    public async Task ExecuteAsync_PreservesStructuredFailureMetadata_WhenRequestMetadataConflicts()
    {
        var kernel = CreateKernel(new FailingKernelExecutor());
        var request = CreateRequest(
            "valid-rom",
            ImmutableDictionary<string, string>.Empty
                .Add(ReplayMetadataKeys.FailureKind, "user-value")
                .Add(ReplayMetadataKeys.OriginStep, "user-value")
                .Add(ReplayMetadataKeys.SemanticSlot, "user-value")
                .Add(KernelFacadeMetadataKeys.RootRomId, "user-value")
                .Add(KernelFacadeMetadataKeys.ProviderId, "user-value")
                .Add(KernelFacadeMetadataKeys.VfsProviderId, "user-value")
                .Add(KernelFacadeMetadataKeys.RequestedModelId, "user-value")
                .Add(KernelFacadeMetadataKeys.TransactionId, "user-value")
                .Add(KernelFacadeMetadataKeys.InputHash, "user-value")
                .Add("custom_key", "custom-value"));

        var result = await kernel.ExecuteAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(ExecutionStatus.Failed, result.Status);
        Assert.Equal(FailureKind.FailClosed.ToString(), result.Metadata[ReplayMetadataKeys.FailureKind]);
        Assert.Equal(OriginStep.KernelFacade.ToString(), result.Metadata[ReplayMetadataKeys.OriginStep]);
        Assert.Equal(SemanticSlot.T.ToString(), result.Metadata[ReplayMetadataKeys.SemanticSlot]);
        Assert.Equal("valid-rom", result.Metadata[KernelFacadeMetadataKeys.RootRomId]);
        Assert.Equal("fake-provider", result.Metadata[KernelFacadeMetadataKeys.ProviderId]);
        Assert.Equal("memory-file", result.Metadata[KernelFacadeMetadataKeys.VfsProviderId]);
        Assert.Equal("gpt-test", result.Metadata[KernelFacadeMetadataKeys.RequestedModelId]);
        Assert.Equal(result.ExecutionId, result.Metadata[KernelFacadeMetadataKeys.TransactionId]);
        Assert.StartsWith("sha256:", result.Metadata[KernelFacadeMetadataKeys.InputHash], StringComparison.Ordinal);
        Assert.Equal("custom-value", result.Metadata["custom_key"]);
        Assert.Equal("kernel.facade.fail", result.Metadata[ReplayMetadataKeys.SemanticDelta]);
    }

    [Fact]
    public async Task ExecuteAsync_PreservesRoutingMetadata_WhenTransactionSucceeds()
    {
        var kernel = CreateKernel();
        var request = CreateRequest(
            "valid-rom",
            ImmutableDictionary<string, string>.Empty
                .Add(KernelFacadeMetadataKeys.ProviderId, "user-provider")
                .Add(KernelFacadeMetadataKeys.ProviderTier, "capability")
                .Add(KernelFacadeMetadataKeys.CapabilityModuleId, "AIKernel.Tools.Cli")
                .Add(KernelFacadeMetadataKeys.RouteReason, "aik-prefix")
                .Add("custom_key", "custom-value"));

        var result = await kernel.ExecuteAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(ExecutionStatus.Succeeded, result.Status);
        Assert.Equal("fake-provider", result.Metadata[KernelFacadeMetadataKeys.ProviderId]);
        Assert.Equal("capability", result.Metadata[KernelFacadeMetadataKeys.ProviderTier]);
        Assert.Equal("AIKernel.Tools.Cli", result.Metadata[KernelFacadeMetadataKeys.CapabilityModuleId]);
        Assert.Equal("aik-prefix", result.Metadata[KernelFacadeMetadataKeys.RouteReason]);
        Assert.Equal("custom-value", result.Metadata["custom_key"]);
        Assert.Equal("valid-rom", result.Metadata[KernelFacadeMetadataKeys.RootRomId]);
        Assert.Equal("memory-file", result.Metadata[KernelFacadeMetadataKeys.VfsProviderId]);
        Assert.Equal("gpt-test", result.Metadata[KernelFacadeMetadataKeys.RequestedModelId]);
        Assert.StartsWith("ktx:sha256:", result.Metadata[KernelFacadeMetadataKeys.TransactionId], StringComparison.Ordinal);
        Assert.StartsWith("sha256:", result.Metadata[KernelFacadeMetadataKeys.InputHash], StringComparison.Ordinal);
        ReplayMetadataAssertions.AssertReplayMetadata(
            result.Metadata,
            "kernel.executor.succeeded",
            "3");
    }

    [Fact]
    public async Task ExecuteAsync_AddsCanonicalMetadata_WhenExecutorMetadataIsNull()
    {
        var kernel = CreateKernel(new NullMetadataKernelExecutor());
        var request = CreateRequest(
            "valid-rom",
            ImmutableDictionary<string, string>.Empty
                .Add(KernelFacadeMetadataKeys.ProviderTier, "capability")
                .Add(KernelFacadeMetadataKeys.RouteReason, "aik-prefix"));

        var result = await kernel.ExecuteAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(ExecutionStatus.Succeeded, result.Status);
        Assert.Equal("capability", result.Metadata[KernelFacadeMetadataKeys.ProviderTier]);
        Assert.Equal("aik-prefix", result.Metadata[KernelFacadeMetadataKeys.RouteReason]);
        Assert.Equal("valid-rom", result.Metadata[KernelFacadeMetadataKeys.RootRomId]);
        Assert.Equal("fake-provider", result.Metadata[KernelFacadeMetadataKeys.ProviderId]);
        Assert.Equal("memory-file", result.Metadata[KernelFacadeMetadataKeys.VfsProviderId]);
        Assert.Equal("gpt-test", result.Metadata[KernelFacadeMetadataKeys.RequestedModelId]);
        Assert.StartsWith("ktx:sha256:", result.Metadata[KernelFacadeMetadataKeys.TransactionId], StringComparison.Ordinal);
        Assert.StartsWith("sha256:", result.Metadata[KernelFacadeMetadataKeys.InputHash], StringComparison.Ordinal);
    }

    [Fact]
    public void KernelTransactionIdFactory_ReturnsDeterministicId_ForSameRequest()
    {
        var requestHasher = new AIKernel.Kernel.KernelRequestHasher();
        var factory = new AIKernel.Kernel.KernelTransactionIdFactory(requestHasher);
        var request = CreateRequest("valid-rom");

        var first = factory.CreateTransactionId(request);
        var second = factory.CreateTransactionId(request);

        Assert.Equal(first, second);
        Assert.Equal(
            $"ktx:{requestHasher.ComputeHash(request)}",
            first);
    }

    [Fact]
    public async Task ExecuteAsync_UsesSelectedProviderAndRequestedModel_WhenExecutorResultDiffers()
    {
        var kernel = CreateKernel(new MismatchedIdentityKernelExecutor());
        var request = CreateRequest("valid-rom");

        var result = await kernel.ExecuteAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(ExecutionStatus.Succeeded, result.Status);
        Assert.Equal("fake-provider", result.ProviderId);
        Assert.Equal("gpt-test", result.ModelId);
        Assert.Equal("fake-provider", result.Metadata[KernelFacadeMetadataKeys.ProviderId]);
        Assert.Equal("gpt-test", result.Metadata[KernelFacadeMetadataKeys.RequestedModelId]);
    }

    [Fact]
    public async Task ExecuteAsync_UsesAssembledContextIdentity_WhenExecutorResultDiffers()
    {
        var kernel = CreateKernel(new MismatchedContextKernelExecutor());
        var request = CreateRequest("valid-rom");

        var result = await kernel.ExecuteAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(ExecutionStatus.Succeeded, result.Status);
        Assert.Equal("snapshot:contract", result.ContextSnapshotId);
        Assert.Equal("sha256:context", result.ContextHash);
    }

    [Fact]
    public async Task ExecuteAsync_ClearsExecutorError_WhenTransactionSucceeds()
    {
        var kernel = CreateKernel(new SuccessfulResultWithErrorKernelExecutor());
        var request = CreateRequest("valid-rom");

        var result = await kernel.ExecuteAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(ExecutionStatus.Succeeded, result.Status);
        Assert.Null(result.Error);
        Assert.Equal("contract output", result.OutputText);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsCanceledMetadata_WhenExecutorCancels()
    {
        var kernel = CreateKernel(new CanceledKernelExecutor());

        var result = await kernel.ExecuteAsync(
            CreateValidRequest(),
            TestContext.Current.CancellationToken);

        Assert.Equal(ExecutionStatus.Canceled, result.Status);
        Assert.Equal("canceled", result.Error?.Code);
        Assert.Equal("fake-provider", result.ProviderId);
        Assert.Equal("fake-provider", result.Metadata[KernelFacadeMetadataKeys.ProviderId]);
        Assert.Equal(FailureKind.FailClosed.ToString(), result.Metadata[ReplayMetadataKeys.FailureKind]);
        Assert.Equal(OriginStep.KernelFacade.ToString(), result.Metadata[ReplayMetadataKeys.OriginStep]);
        Assert.Equal(SemanticSlot.T.ToString(), result.Metadata[ReplayMetadataKeys.SemanticSlot]);
        ReplayMetadataAssertions.AssertReplayMetadata(
            result.Metadata,
            "kernel.facade.cancel",
            "1");
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsRejectedResult_WhenMetadataIsMissing()
    {
        var kernel = CreateKernel();
        var request = CreateRequest(
            "valid-rom",
            useNullMetadata: true);

        var result = await kernel.ExecuteAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(ExecutionStatus.Rejected, result.Status);
        Assert.Equal("invalid_kernel_request", result.Error?.Code);
        Assert.Equal("Metadata is required.", result.Error?.Message);
        Assert.Equal(string.Empty, result.Metadata[KernelFacadeMetadataKeys.ProviderId]);
        Assert.Equal(FailureKind.Reject.ToString(), result.Metadata[ReplayMetadataKeys.FailureKind]);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsRejectedResult_WhenScopeMetadataIsMissing()
    {
        var kernel = CreateKernel();
        var request = CreateRequest(
            "valid-rom",
            useNullScopeMetadata: true);

        var result = await kernel.ExecuteAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(ExecutionStatus.Rejected, result.Status);
        Assert.Equal("invalid_kernel_request", result.Error?.Code);
        Assert.Equal("Scope.Metadata is required.", result.Error?.Message);
        Assert.Equal(FailureKind.Reject.ToString(), result.Metadata[ReplayMetadataKeys.FailureKind]);
    }

    private static KernelRequest CreateRequest(
        string rootRomId,
        ImmutableDictionary<string, string>? metadata = null,
        bool useNullMetadata = false,
        bool useNullScopeMetadata = false)
    {
        return new KernelRequest
        {
            Input = "Summarize the context.",
            RootRomId = new RomId(rootRomId),
            VfsProviderId = "memory-file",
            Credentials = new VfsCredentials(),
            Scope = new ContextAssemblyScope
            {
                Purpose = "contract-test",
                Capabilities = ["summarize"],
                Metadata = useNullScopeMetadata
                    ? null!
                    : ImmutableDictionary<string, string>.Empty
            },
            PromptOptions = CreatePromptOptions(),
            ExecutionOptions = CreateExecutionOptions(),
            RequestedModelId = "gpt-test",
            Metadata = useNullMetadata
                ? null!
                : metadata ?? ImmutableDictionary<string, string>.Empty
        };
    }

    private static AIKernel.Kernel.Kernel CreateKernel(
        AIKernel.Abstractions.Execution.IKernelExecutor kernelExecutor)
    {
        var requestHasher = new AIKernel.Kernel.KernelRequestHasher();

        return new AIKernel.Kernel.Kernel(
            new FakeVfsSessionFactory(),
            new FakeContextAssembler(),
            new FakeModelProviderSelector(),
            kernelExecutor,
            requestHasher,
            new AIKernel.Kernel.KernelTransactionIdFactory(requestHasher));
    }

    private sealed class FakeVfsSessionFactory : IKernelVfsSessionFactory
    {
        public Task<IVfsSession> OpenSessionAsync(
            KernelRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IVfsSession>(new FakeVfsSession());
        }
    }

    private sealed class FakeContextAssembler : IContextAssembler
    {
        public Task<IContextSnapshot> AssembleAsync(
            IVfsSession session,
            ContextAssemblyRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (request.RootRomId.Value == "denied-rom")
            {
                throw new ContextAssemblyGovernanceException(
                    request.RootRomId,
                    "Denied by contract test policy.");
            }

            if (request.RootRomId.Value == "invalid-signature-rom")
            {
                throw new RomSignatureVerificationException(
                    "rom://invalid-signature-rom",
                    "sha256:expected",
                    "sha256:actual");
            }

            IContextSnapshot snapshot = new AssembledContextSnapshot(
                snapshotId: "snapshot:contract",
                parentSnapshotId: null,
                createdAtUtc: DateTimeOffset.UnixEpoch,
                contextHash: "sha256:context",
                context: new ContextCollectionSnapshot([]));

            return Task.FromResult(snapshot);
        }
    }

    private sealed class FakeModelProviderSelector : IKernelModelProviderSelector
    {
        public Task<IModelProvider> SelectAsync(
            KernelRequest request,
            IContextSnapshot contextSnapshot,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IModelProvider>(new FakeModelProvider());
        }
    }

    private sealed class FakeKernelExecutor : AIKernel.Abstractions.Execution.IKernelExecutor
    {
        private long _sequence;

        public Task<KernelRequestExecutionResult> ExecuteAsync(
            IModelProvider provider,
            KernelExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sequence = Interlocked.Increment(ref _sequence);

            return Task.FromResult(new KernelRequestExecutionResult
            {
                ExecutionId = $"exec:contract:{sequence:D8}",
                Status = ExecutionStatus.Succeeded,
                ProviderId = provider.ProviderId,
                ModelId = request.RequestedModelId ?? "gpt-test",
                ContextSnapshotId = request.ContextSnapshotId,
                ContextHash = request.ContextHash,
                PromptHash = "sha256:prompt",
                OutputText = "contract output",
                Usage = new ExecutionUsage(
                    InputTokens: 1,
                    OutputTokens: 1,
                    TotalTokens: 2),
                Error = null,
                StartedAtUtc = DateTimeOffset.UnixEpoch,
                CompletedAtUtc = DateTimeOffset.UnixEpoch,
                Metadata = ImmutableDictionary<string, string>.Empty
                    .Add(ReplayMetadataKeys.StepId, "step:sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")
                    .Add(ReplayMetadataKeys.SemanticDelta, "kernel.executor.succeeded")
                    .Add(ReplayMetadataKeys.ReplayLogCount, "3")
                    .Add(ReplayMetadataKeys.ReplayLogHash, "replay:sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb")
            });
        }
    }

    private sealed class NullMetadataKernelExecutor : AIKernel.Abstractions.Execution.IKernelExecutor
    {
        public Task<KernelRequestExecutionResult> ExecuteAsync(
            IModelProvider provider,
            KernelExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(new KernelRequestExecutionResult
            {
                ExecutionId = "exec:contract:null-metadata",
                Status = ExecutionStatus.Succeeded,
                ProviderId = provider.ProviderId,
                ModelId = request.RequestedModelId ?? "gpt-test",
                ContextSnapshotId = request.ContextSnapshotId,
                ContextHash = request.ContextHash,
                PromptHash = "sha256:prompt",
                OutputText = "contract output",
                Usage = new ExecutionUsage(
                    InputTokens: 1,
                    OutputTokens: 1,
                    TotalTokens: 2),
                Error = null,
                StartedAtUtc = DateTimeOffset.UnixEpoch,
                CompletedAtUtc = DateTimeOffset.UnixEpoch,
                Metadata = null!
            });
        }
    }

    private sealed class MismatchedIdentityKernelExecutor : AIKernel.Abstractions.Execution.IKernelExecutor
    {
        public Task<KernelRequestExecutionResult> ExecuteAsync(
            IModelProvider provider,
            KernelExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(new KernelRequestExecutionResult
            {
                ExecutionId = "exec:contract:mismatched-identity",
                Status = ExecutionStatus.Succeeded,
                ProviderId = "executor-provider",
                ModelId = "executor-model",
                ContextSnapshotId = request.ContextSnapshotId,
                ContextHash = request.ContextHash,
                PromptHash = "sha256:prompt",
                OutputText = "contract output",
                Usage = new ExecutionUsage(
                    InputTokens: 1,
                    OutputTokens: 1,
                    TotalTokens: 2),
                Error = null,
                StartedAtUtc = DateTimeOffset.UnixEpoch,
                CompletedAtUtc = DateTimeOffset.UnixEpoch,
                Metadata = ImmutableDictionary<string, string>.Empty
                    .Add(KernelFacadeMetadataKeys.ProviderId, "executor-provider")
                    .Add(KernelFacadeMetadataKeys.RequestedModelId, "executor-model")
            });
        }
    }

    private sealed class MismatchedContextKernelExecutor : AIKernel.Abstractions.Execution.IKernelExecutor
    {
        public Task<KernelRequestExecutionResult> ExecuteAsync(
            IModelProvider provider,
            KernelExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(new KernelRequestExecutionResult
            {
                ExecutionId = "exec:contract:mismatched-context",
                Status = ExecutionStatus.Succeeded,
                ProviderId = provider.ProviderId,
                ModelId = request.RequestedModelId ?? "gpt-test",
                ContextSnapshotId = "snapshot:executor",
                ContextHash = "sha256:executor",
                PromptHash = "sha256:prompt",
                OutputText = "contract output",
                Usage = new ExecutionUsage(
                    InputTokens: 1,
                    OutputTokens: 1,
                    TotalTokens: 2),
                Error = null,
                StartedAtUtc = DateTimeOffset.UnixEpoch,
                CompletedAtUtc = DateTimeOffset.UnixEpoch,
                Metadata = ImmutableDictionary<string, string>.Empty
            });
        }
    }

    private sealed class SuccessfulResultWithErrorKernelExecutor : AIKernel.Abstractions.Execution.IKernelExecutor
    {
        public Task<KernelRequestExecutionResult> ExecuteAsync(
            IModelProvider provider,
            KernelExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(new KernelRequestExecutionResult
            {
                ExecutionId = "exec:contract:success-with-error",
                Status = ExecutionStatus.Succeeded,
                ProviderId = provider.ProviderId,
                ModelId = request.RequestedModelId ?? "gpt-test",
                ContextSnapshotId = request.ContextSnapshotId,
                ContextHash = request.ContextHash,
                PromptHash = "sha256:prompt",
                OutputText = "contract output",
                Usage = new ExecutionUsage(
                    InputTokens: 1,
                    OutputTokens: 1,
                    TotalTokens: 2),
                Error = new ExecutionError(
                    Code: "executor_error",
                    Message: "Executor returned a stale error.",
                    Detail: null),
                StartedAtUtc = DateTimeOffset.UnixEpoch,
                CompletedAtUtc = DateTimeOffset.UnixEpoch,
                Metadata = ImmutableDictionary<string, string>.Empty
            });
        }
    }

    private sealed class FailingKernelExecutor : AIKernel.Abstractions.Execution.IKernelExecutor
    {
        public Task<KernelRequestExecutionResult> ExecuteAsync(
            IModelProvider provider,
            KernelExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("executor failed");
        }
    }

    private sealed class CanceledKernelExecutor : AIKernel.Abstractions.Execution.IKernelExecutor
    {
        public Task<KernelRequestExecutionResult> ExecuteAsync(
            IModelProvider provider,
            KernelExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new OperationCanceledException();
        }
    }

    private sealed class FakeModelProvider : IModelProvider
    {
        public string ProviderId => "fake-provider";

        public string Name => "Fake Provider";

        public string Version => "0.0.5";

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

        public IDictionary<string, float>? GetDynamicCapacities(IExecutionConstraints constraints)
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

    private sealed class FakeVfsCredentials : IVfsCredentials
    {
        public string? Username => null;

        public string? ApiKey => null;

        public string? Token => null;

        public IReadOnlyDictionary<string, object> Parameters =>
            ImmutableDictionary<string, object>.Empty;
    }

    private static PromptGenerationOptions CreatePromptOptions()
    {
        return new PromptGenerationOptions
        {
            OverflowPolicy = PromptOverflowPolicy.FailClosed,
            IncludeContextHash = true,
            IncludeSourceMetadata = true
        };
    }

    private static ExecutionOptions CreateExecutionOptions()
    {
        return new ExecutionOptions
        {
            Temperature = 0,
            TopP = 1,
            MaxOutputTokens = 128,
            StopSequences = []
        };
    }

    private sealed class FakeVfsSession : IVfsSession
    {
        public string SessionId => "vfs-session:contract";

        public Task<IReadableVfsFile> ReadReadableFileAsync(string path)
        {
            throw new FileNotFoundException(path);
        }

        public Task<IVfsFile> ReadFileAsync(string path)
        {
            throw new FileNotFoundException(path);
        }

        public Task<bool> ExistsAsync(string path)
        {
            return Task.FromResult(false);
        }

        public Task WriteFileAsync(string path, byte[] content)
        {
            throw new UnauthorizedAccessException("Fake VFS session is read-only.");
        }

        public Task DeleteAsync(string path)
        {
            throw new UnauthorizedAccessException("Fake VFS session is read-only.");
        }

        public Task<IVfsDirectory> GetDirectoryAsync(string path)
        {
            throw new DirectoryNotFoundException(path);
        }

        public Task<INavigableVfsDirectory> GetNavigableDirectoryAsync(string path)
        {
            throw new DirectoryNotFoundException(path);
        }

        public Task<IVfsQueryResult> QueryAsync(IVfsQuery query)
        {
            throw new NotSupportedException("Fake VFS session does not support queries.");
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
