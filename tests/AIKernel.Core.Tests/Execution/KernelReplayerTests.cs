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
        var dump = CreateReplayDumpWithoutHashChain();

        var result = await replayer.ReplayAsync(
            dump,
            CreateTraceContext(),
            TestContext.Current.CancellationToken);

        Assert.False(replayer.CanReplay(dump));
        Assert.False(result.IsSuccessful);
        Assert.Equal("Replay dump is incomplete or fails closed.", result.ErrorMessage);
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

    private static ReplayDump CreateReplayDumpWithoutHashChain()
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
            HashChain = null!
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
