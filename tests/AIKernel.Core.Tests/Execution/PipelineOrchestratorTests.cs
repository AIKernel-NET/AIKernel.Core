namespace AIKernel.Core.Tests.Execution;

using AIKernel.Core.Context;
using AIKernel.Core.Execution;
using AIKernel.Dtos.Context;
using AIKernel.Dtos.Execution;
using AIKernel.Enums;
using Xunit;

public sealed class PipelineOrchestratorTests
{
    [Fact]
    public async Task InitializeAsync_ReturnsDeterministicContextHash()
    {
        var orchestrator = new PipelineOrchestrator(new KernelReplayer());
        var context = CreateContext();

        var first = await orchestrator.InitializeAsync(
            context,
            TestContext.Current.CancellationToken);
        var second = await orchestrator.InitializeAsync(
            context,
            TestContext.Current.CancellationToken);

        Assert.True(first.IsInitialized);
        Assert.Empty(first.Issues);
        Assert.StartsWith("sha256:", first.PreExecutionContextHash, StringComparison.Ordinal);
        Assert.Equal(first.PreExecutionContextHash, second.PreExecutionContextHash);
    }

    [Fact]
    public async Task ExecuteAsync_FailsClosed_WhenSignatureIsInvalid()
    {
        var orchestrator = new PipelineOrchestrator(new KernelReplayer());

        var result = await orchestrator.ExecuteAsync(
            CreateContext(),
            new SignatureVerificationResult
            {
                IsValid = false,
                SignerId = "test-signer",
                TrustScore = 0,
                Message = "bad signature",
                VerificationTimestamp = DateTime.UnixEpoch
            },
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccessful);
        Assert.Equal("Signature verification failed.", result.ErrorMessage);
        Assert.StartsWith("sha256:", result.Logic.SerializedRepresentation, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_FailsClosed_WhenExecutionBackendIsNotBound()
    {
        var orchestrator = new PipelineOrchestrator(new KernelReplayer());

        var result = await orchestrator.ExecuteAsync(
            CreateContext(),
            new SignatureVerificationResult
            {
                IsValid = true,
                SignerId = "test-signer",
                TrustScore = 1,
                Message = "ok",
                VerificationTimestamp = DateTime.UnixEpoch
            },
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccessful);
        Assert.Equal("Pipeline execution requires a bound execution backend.", result.ErrorMessage);
    }

    [Fact]
    public async Task ReplayFromDumpAsync_DelegatesToKernelReplayer()
    {
        var orchestrator = new PipelineOrchestrator(new KernelReplayer());

        var result = await orchestrator.ReplayFromDumpAsync(
            CreateReplayDump(),
            new ModificationContext
            {
                Reason = "test",
                TargetPhase = "replay",
                ModificationData = string.Empty,
                ModifiedBy = "unit-test"
            },
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccessful);
        Assert.Equal("final", result.FinalOutput);
    }

    private static ContextCollectionSnapshot CreateContext()
    {
        return new ContextCollectionSnapshot(
        [
            new ContextFragment
            {
                FragmentId = "fragment-b",
                Category = ContextCategory.History,
                Content = "second",
                Priority = 2,
                CreatedAt = DateTime.UnixEpoch,
                Metadata = new Dictionary<string, string>
                {
                    ["b"] = "2"
                }
            },
            new ContextFragment
            {
                FragmentId = "fragment-a",
                Category = ContextCategory.Material,
                Content = "first\r\n",
                Priority = 1,
                CreatedAt = DateTime.UnixEpoch,
                Metadata = new Dictionary<string, string>
                {
                    ["a"] = "1"
                }
            }
        ]);
    }

    private static ReplayDump CreateReplayDump()
    {
        return new ReplayDump
        {
            DumpId = "dump-1",
            CreatedAt = DateTime.UnixEpoch,
            StructureOutput = new RawLogic("logic"),
            GenerationOutput = "final",
            OriginalResult = new ExecutionResult
            {
                Logic = new RawLogic("logic"),
                FinalOutput = "final",
                IsSuccessful = true,
                ErrorMessage = string.Empty,
                ElapsedMilliseconds = 12
            },
            HashChain = new HashChain
            {
                HashAlgorithm = "sha256",
                StructureHash = "s",
                GenerationHash = "g",
                GenerationParentHash = "s",
                PolishHash = "p",
                PolishParentHash = "g"
            }
        };
    }
}
