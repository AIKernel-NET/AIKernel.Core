namespace AIKernel.Core.Tests.Common;

using AIKernel.Common.Results;
using AIKernel.Core.Tests.Support;
using Xunit;

public sealed class PipelineStepTests
{
    [Fact]
    public void Loop_ExpandsFiniteIterationsIntoReplayLog()
    {
        var step = PipelineStep.Loop(
            ResultStep<string, int>.Success("agent", 0),
            maxIterations: 3,
            static (iteration, value) => ResultStep<string, int>
                .Success($"agent:{iteration}", value + 1));

        Assert.True(step.IsSuccess);
        Assert.Equal(3, step.Value);
        Assert.Equal(3, step.ReplayLog.Count);
        Assert.Equal("max_iterations_reached", step.SemanticDelta.Metadata!["loop_decision"]);
        Assert.Equal("2", step.SemanticDelta.Metadata!["loop_iteration"]);
        Assert.All(step.ReplayLog, entry =>
        {
            Assert.Equal("loop", entry.SemanticDelta.Kind);
            Assert.Equal("loop", entry.SemanticDelta.Metadata!["delta.kind"]);
            Assert.True(entry.IsSuccess);
        });
        Assert.Equal(step.ReplayLog[0].StepId, step.ReplayLog[1].ParentStepId);
        Assert.Equal(step.ReplayLog[1].StepId, step.ReplayLog[2].ParentStepId);
        ReplayMetadataAssertions.AssertReplayLogHash(step.ReplayLogHash);
    }

    [Fact]
    public void Loop_ShortCircuitsOnFailureAndRecordsFailedIteration()
    {
        var error = new ErrorContext("blocked", "BLOCKED", false);

        var step = PipelineStep.Loop(
            ResultStep<string, int>.Success("agent", 0),
            maxIterations: 3,
            (iteration, value) => iteration == 1
                ? ResultStep<string, int>.Fail($"agent:{iteration}", error)
                : ResultStep<string, int>.Success($"agent:{iteration}", value + 1));

        Assert.True(step.IsFailure);
        Assert.Same(error, step.Error);
        Assert.Equal(2, step.ReplayLog.Count);
        Assert.False(step.ReplayLog[^1].IsSuccess);
        Assert.Equal("BLOCKED", step.ReplayLog[^1].ErrorCode);
        Assert.Equal("1", step.SemanticDelta.Metadata!["loop_iteration"]);
    }

    [Fact]
    public void LoopUntil_StopsAtTimeoutWithTimestampMetadata()
    {
        var start = DateTimeOffset.UnixEpoch;
        var timestamps = new Queue<DateTimeOffset>(
        [
            start,
            start.AddSeconds(1),
            start.AddSeconds(3)
        ]);

        var step = PipelineStep.LoopUntil(
            ResultStep<string, int>.Success("agent", 0),
            timeout: TimeSpan.FromSeconds(2),
            startedAtUtc: start,
            nowProvider: () => timestamps.Dequeue(),
            maxIterations: 5,
            static (iteration, _, value) => ResultStep<string, int>
                .Success($"agent:{iteration}", value + 1));

        Assert.True(step.IsSuccess);
        Assert.Equal(2, step.Value);
        Assert.Equal("timeout_reached", step.SemanticDelta.Metadata!["loop_decision"]);
        Assert.Equal("2", step.SemanticDelta.Metadata!["loop_iteration"]);
        Assert.Equal(start.AddSeconds(3).ToString("O"), step.SemanticDelta.Metadata!["loop_timestamp"]);
        Assert.Equal(3, step.ReplayLog.Count);
    }

    [Fact]
    public void Suspend_CreatesDeterministicQuarantineStopPoint()
    {
        var step = PipelineStep.Suspend<string, int>(
            "awaiting-approval",
            "Needs user approval.");

        Assert.True(step.IsFailure);
        Assert.True(step.IsSuspended);
        Assert.Equal(PipelineStep.SuspendErrorCode, step.Error!.Code);
        Assert.Equal(FailureKind.Quarantine, step.Error.FailureKind);
        Assert.Equal("suspend", step.SemanticDelta.Kind);
        Assert.Equal("suspend", step.SemanticDelta.Metadata!["delta.kind"]);
        Assert.Equal("Needs user approval.", step.SemanticDelta.Metadata!["suspend_reason"]);
        var entry = Assert.Single(step.ReplayLog);
        Assert.False(entry.IsSuccess);
        Assert.Equal(PipelineStep.SuspendErrorCode, entry.ErrorCode);
    }

    [Fact]
    public void Resume_AppendsToPreviousReplayLogWithParentChain()
    {
        var suspended = PipelineStep.Suspend<string, int>(
            "awaiting-approval",
            "Needs user approval.");

        var resumed = PipelineStep.Resume(
            suspended.ReplayLog,
            "approved",
            42,
            "User approved.");

        Assert.True(resumed.IsSuccess);
        Assert.Equal(42, resumed.Value);
        Assert.Equal(2, resumed.ReplayLog.Count);
        Assert.Equal(suspended.ReplayLogHash, resumed.SemanticDelta.Metadata!["previous_replay_log_hash"]);
        Assert.Equal(suspended.StepId, resumed.ReplayLog[^1].ParentStepId);
        Assert.Equal("resume", resumed.SemanticDelta.Kind);
        ReplayMetadataAssertions.AssertReplayLogHash(resumed.ReplayLogHash);
    }

    [Fact]
    public void LinqQuery_ComposesLoopSuspendAndResume()
    {
        var suspended = PipelineStep.Suspend<string, int>(
            "awaiting-approval",
            "Needs user approval.");

        var resumed =
            from approval in PipelineStep.Resume(
                suspended.ReplayLog,
                "approved",
                1,
                "User approved.")
            from looped in PipelineStep.Loop(
                ResultStep<string, int>.Success("agent", approval),
                maxIterations: 2,
                static (iteration, value) => ResultStep<string, int>
                    .Success($"agent:{iteration}", value + 1))
            select looped;

        Assert.True(resumed.IsSuccess);
        Assert.Equal(3, resumed.Value);
        Assert.Equal(4, resumed.ReplayLog.Count);
        Assert.Equal("resume", resumed.ReplayLog[1].SemanticDelta.Kind);
        Assert.Equal("loop", resumed.ReplayLog[2].SemanticDelta.Kind);
        Assert.Equal(resumed.ReplayLog[2].StepId, resumed.ReplayLog[3].ParentStepId);
    }
}
