namespace AIKernel.Core.Tests.Execution;

using AIKernel.Core.Execution;
using AIKernel.Dtos.Execution;
using AIKernel.Dtos.KernelContext;
using Xunit;

public sealed class KernelReplayerTests
{
    [Fact]
    public async Task ReplayAsync_ReturnsOriginalResult_WhenDumpIsReplayable()
    {
        var replayer = new KernelReplayer();
        var dump = CreateReplayDump();

        var result = await replayer.ReplayAsync(
            dump,
            CreateTraceContext(),
            TestContext.Current.CancellationToken);

        Assert.True(replayer.CanReplay(dump));
        Assert.True(result.IsSuccessful);
        Assert.Equal("logic", result.Logic.SerializedRepresentation);
        Assert.Equal("final", result.FinalOutput);
        Assert.Equal(12, result.ElapsedMilliseconds);
    }

    [Fact]
    public async Task ReplayAsync_FailsClosed_WhenDumpIsIncomplete()
    {
        var replayer = new KernelReplayer();
        var dump = CreateReplayDump() with { HashChain = null! };

        var result = await replayer.ReplayAsync(
            dump,
            CreateTraceContext(),
            TestContext.Current.CancellationToken);

        Assert.False(replayer.CanReplay(dump));
        Assert.False(result.IsSuccessful);
        Assert.Equal("Replay dump is incomplete or fails closed.", result.ErrorMessage);
    }

    [Theory]
    [InlineData("structure")]
    [InlineData("generation")]
    [InlineData("result")]
    [InlineData("metadata")]
    [InlineData("generation-parent")]
    [InlineData("polish-parent")]
    [InlineData("digest")]
    [InlineData("algorithm")]
    public async Task ReplayAsync_RejectsTamperedDump(string tampering)
    {
        var replayer = new KernelReplayer();
        var original = CreateReplayDump();
        var tampered = tampering switch
        {
            "structure" => original with { StructureOutput = new RawLogic("tampered") },
            "generation" => original with { GenerationOutput = "tampered" },
            "result" => original with
            {
                OriginalResult = new ExecutionResult
                {
                    Logic = original.OriginalResult.Logic,
                    FinalOutput = "tampered",
                    IsSuccessful = original.OriginalResult.IsSuccessful,
                    ErrorMessage = original.OriginalResult.ErrorMessage,
                    ElapsedMilliseconds = original.OriginalResult.ElapsedMilliseconds
                }
            },
            "metadata" => original with { CreatedAt = original.CreatedAt.AddSeconds(1) },
            "generation-parent" => original with
            {
                HashChain = original.HashChain with { GenerationParentHash = original.HashChain.GenerationHash }
            },
            "polish-parent" => original with
            {
                HashChain = original.HashChain with { PolishParentHash = original.HashChain.StructureHash }
            },
            "digest" => original with
            {
                HashChain = original.HashChain with { PolishHash = original.HashChain.StructureHash }
            },
            "algorithm" => original with { HashChain = original.HashChain with { HashAlgorithm = "SHA1" } },
            _ => throw new ArgumentOutOfRangeException(nameof(tampering))
        };

        var result = await replayer.ReplayAsync(tampered, CreateTraceContext(), TestContext.Current.CancellationToken);

        Assert.False(replayer.CanReplay(tampered));
        Assert.False(result.IsSuccessful);
        Assert.Empty(result.FinalOutput);
        Assert.Empty(result.Logic.SerializedRepresentation);
    }

    [Fact]
    public async Task ReplayAsync_RequiresTraceContext()
    {
        var replayer = new KernelReplayer();

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await replayer.ReplayAsync(
                CreateReplayDump(),
                null!,
                TestContext.Current.CancellationToken));
    }

    private static ReplayDump CreateReplayDump()
    {
        const string dumpId = "dump-1";
        var createdAt = DateTime.UnixEpoch;
        var structure = new RawLogic("logic");
        const string generation = "generated";
        var original = new ExecutionResult
        {
            Logic = new RawLogic("original logic"),
            FinalOutput = "final",
            IsSuccessful = true,
            ErrorMessage = string.Empty,
            ElapsedMilliseconds = 12
        };

        return new ReplayDump
        {
            DumpId = dumpId,
            CreatedAt = createdAt,
            StructureOutput = structure,
            GenerationOutput = generation,
            OriginalResult = original,
            HashChain = ReplayDumpIntegrity.CreateHashChain(dumpId, createdAt, structure, generation, original)
        };
    }

    private static TraceContext CreateTraceContext()
    {
        return new TraceContext(
            TraceId: "trace",
            SpanId: "span",
            ParentSpanId: string.Empty,
            StartTime: DateTime.UnixEpoch,
            EndTime: null,
            Tags: new Dictionary<string, string>(),
            Logs: []);
    }
}
