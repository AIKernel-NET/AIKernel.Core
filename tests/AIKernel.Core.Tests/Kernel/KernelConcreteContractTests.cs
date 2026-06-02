namespace AIKernel.Core.Tests.Kernel;

using System.Collections.Immutable;
using AIKernel.Abstractions.Context;
using AIKernel.Abstractions.Execution;
using AIKernel.Abstractions.Kernel;
using AIKernel.Abstractions.Models;
using AIKernel.Abstractions.Providers;
using AIKernel.Core.Context;
using AIKernel.Core.Rom;
using AIKernel.Dtos.Core;
using AIKernel.Dtos.Execution;
using AIKernel.Dtos.Kernel;
using AIKernel.Dtos.Rom;
using AIKernel.Dtos.Routing;
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

    private static KernelRequest CreateRequest(string rootRomId)
    {
        return new KernelRequest
        {
            Input = "Summarize the context.",
            RootRomId = new RomId(rootRomId),
            VfsProviderId = "memory-file",
            VfsCredentials = new FakeVfsCredentials(),
            Scope = new ContextAssemblyScope
            {
                Purpose = "contract-test",
                Capabilities = ["summarize"],
                Metadata = ImmutableDictionary<string, string>.Empty
            },
            PromptOptions = PromptGenerationOptions.Default,
            ExecutionOptions = ExecutionOptions.DeterministicDefault,
            RequestedModelId = "gpt-test",
            Metadata = ImmutableDictionary<string, string>.Empty
        };
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
                ContextSnapshotId = request.ContextSnapshot.SnapshotId,
                ContextHash = request.ContextSnapshot.ContextHash,
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
