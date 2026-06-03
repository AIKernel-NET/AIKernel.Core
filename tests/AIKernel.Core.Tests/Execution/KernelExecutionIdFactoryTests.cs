namespace AIKernel.Core.Tests.Execution;

using System.Collections.Immutable;
using AIKernel.Abstractions.Context;
using AIKernel.Common.Results;
using AIKernel.Core.Context;
using AIKernel.Core.Execution;
using AIKernel.Dtos.Execution;
using AIKernel.Dtos.Kernel;
using AIKernel.Dtos.Rom;
using AIKernel.Vfs;
using Xunit;

public sealed class KernelExecutionIdFactoryTests
{
    [Fact]
    public void SemanticStateHash_IsDeterministic_ForSameMaterial()
    {
        var hasher = new SemanticStateHasher();
        var material = SemanticStateMaterial.FromKernelExecution(
            CreateExecutionRequest(),
            ExecutionStatus.Succeeded,
            promptHash: "sha256:prompt",
            resultDiscriminator: "output",
            DateTimeOffset.UnixEpoch,
            executionSequence: 1);

        var first = hasher.ComputeHash(material);
        var second = hasher.ComputeHash(material);

        Assert.Equal(first, second);
        Assert.Equal("sha256", first.Algorithm);
        Assert.Equal("exec:" + first, first.ToExecutionId());
    }

    [Fact]
    public void SemanticStateMaterial_SeparatesExecutionAndFallbackDomains()
    {
        var execution = SemanticStateMaterial.FromKernelExecution(
            CreateExecutionRequest(),
            ExecutionStatus.Succeeded,
            promptHash: "sha256:prompt",
            resultDiscriminator: "output",
            DateTimeOffset.UnixEpoch,
            executionSequence: 1);

        var fallback = SemanticStateMaterial.FromKernelFallback(
            CreateKernelRequest(),
            ExecutionStatus.Succeeded);

        Assert.Equal("kernel.execution", execution.Domain);
        Assert.Equal("kernel.fallback", fallback.Domain);
        Assert.NotEqual(execution.CanonicalPayload, fallback.CanonicalPayload);
    }

    [Fact]
    public void SemanticStateMaterial_ReturnsFailure_WhenExecutionRequestIsMissing()
    {
        var result = SemanticStateMaterial.CreateKernelExecutionResult(
            request: null!,
            ExecutionStatus.Failed,
            promptHash: string.Empty,
            resultDiscriminator: "failed",
            DateTimeOffset.UnixEpoch,
            executionSequence: 1);

        Assert.True(result.IsFailure);
        Assert.Equal("KernelExecutionRequest is required.", result.Error!.Message);
        AssertSemanticHashFailure(result.Error);
    }

    [Fact]
    public void CreateExecutionId_IsDeterministic_ForSameCanonicalState()
    {
        var factory = new KernelExecutionIdFactory();
        var request = CreateExecutionRequest();
        var startedAt = DateTimeOffset.UnixEpoch;

        var first = factory.CreateExecutionId(
            request,
            ExecutionStatus.Succeeded,
            promptHash: "sha256:prompt",
            resultDiscriminator: "output",
            startedAt,
            executionSequence: 1);

        var second = factory.CreateExecutionId(
            request,
            ExecutionStatus.Succeeded,
            promptHash: "sha256:prompt",
            resultDiscriminator: "output",
            startedAt,
            executionSequence: 1);

        Assert.Equal(first, second);
        Assert.StartsWith("exec:sha256:", first, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateExecutionId_Changes_WhenExecutionSequenceChanges()
    {
        var factory = new KernelExecutionIdFactory();
        var request = CreateExecutionRequest();

        var first = factory.CreateExecutionId(
            request,
            ExecutionStatus.Succeeded,
            promptHash: "sha256:prompt",
            resultDiscriminator: "output",
            DateTimeOffset.UnixEpoch,
            executionSequence: 1);

        var second = factory.CreateExecutionId(
            request,
            ExecutionStatus.Succeeded,
            promptHash: "sha256:prompt",
            resultDiscriminator: "output",
            DateTimeOffset.UnixEpoch,
            executionSequence: 2);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void TryCreateExecutionId_ReturnsFailure_WhenContextSnapshotIsMissing()
    {
        var factory = new KernelExecutionIdFactory();

        var result = factory.TryCreateExecutionId(
            new KernelExecutionRequest
            {
                ContextSnapshot = null!,
                UserInstruction = "hello",
                PromptOptions = PromptGenerationOptions.Default,
                ExecutionOptions = ExecutionOptions.DeterministicDefault,
                RequestedModelId = "gpt-test"
            },
            ExecutionStatus.Failed,
            promptHash: string.Empty,
            resultDiscriminator: "failed",
            DateTimeOffset.UnixEpoch,
            executionSequence: 1);

        Assert.True(result.IsFailure);
        Assert.Equal("ContextSnapshot is required.", result.Error!.Message);
        AssertSemanticHashFailure(result.Error);
    }

    [Fact]
    public void CreateFallbackExecutionId_Changes_WhenStatusChanges()
    {
        var factory = new KernelExecutionIdFactory();
        var request = CreateKernelRequest();

        var rejected = factory.CreateFallbackExecutionId(
            request,
            ExecutionStatus.Rejected);

        var failed = factory.CreateFallbackExecutionId(
            request,
            ExecutionStatus.Failed);

        Assert.NotEqual(rejected, failed);
    }

    [Fact]
    public void TryCreateFallbackExecutionId_ReturnsFailure_WhenRequestIsMissing()
    {
        var factory = new KernelExecutionIdFactory();

        var result = factory.TryCreateFallbackExecutionId(
            request: null!,
            ExecutionStatus.Failed);

        Assert.True(result.IsFailure);
        Assert.Equal("KernelRequest is required.", result.Error!.Message);
        AssertSemanticHashFailure(result.Error);
    }

    private static KernelExecutionRequest CreateExecutionRequest()
    {
        IContextSnapshot snapshot = new AssembledContextSnapshot(
            snapshotId: "snapshot:1",
            parentSnapshotId: null,
            createdAtUtc: DateTimeOffset.UnixEpoch,
            contextHash: "sha256:context",
            context: new ContextCollectionSnapshot([]));

        return new KernelExecutionRequest
        {
            ContextSnapshot = snapshot,
            UserInstruction = "hello",
            PromptOptions = PromptGenerationOptions.Default,
            ExecutionOptions = ExecutionOptions.DeterministicDefault,
            RequestedModelId = "gpt-test"
        };
    }

    private static KernelRequest CreateKernelRequest()
    {
        return new KernelRequest
        {
            Input = "hello",
            RootRomId = new RomId("root"),
            VfsProviderId = "memory-file",
            VfsCredentials = new TestVfsCredentials(),
            Scope = new ContextAssemblyScope
            {
                Purpose = "test",
                Capabilities = ["execute"],
                Metadata = ImmutableDictionary<string, string>.Empty
            },
            PromptOptions = PromptGenerationOptions.Default,
            ExecutionOptions = ExecutionOptions.DeterministicDefault,
            RequestedModelId = "gpt-test",
            Metadata = ImmutableDictionary<string, string>.Empty
        };
    }

    private static void AssertSemanticHashFailure(ErrorContext? error)
    {
        Assert.NotNull(error);
        Assert.Equal("ERROR", error.Code);
        Assert.Equal(FailureKind.FailClosed, error.FailureKind);
        Assert.Equal(OriginStep.SemanticHash, error.OriginStep);
        Assert.Equal(SemanticSlot.B, error.SemanticSlot);
    }

    private sealed class TestVfsCredentials : IVfsCredentials
    {
        public string? Username => null;

        public string? ApiKey => null;

        public string? Token => null;

        public IReadOnlyDictionary<string, object> Parameters =>
            ImmutableDictionary<string, object>.Empty;
    }
}
