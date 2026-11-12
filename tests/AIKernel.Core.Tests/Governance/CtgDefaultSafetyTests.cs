namespace AIKernel.Core.Tests.Governance;

using System.Text.Json;
using AIKernel.Core.Governance;
using AIKernel.Core.Time;
using AIKernel.Dtos.Governance;
using AIKernel.Enums.Governance;
using Xunit;

public sealed class CtgDefaultSafetyTests
{
    private static CtgDecisionGateEvaluator DecisionGate() => new(KernelClock.Replay(DateTimeOffset.UnixEpoch));
    private static CtgTrajectoryGateEvaluator TrajectoryGate() => new(KernelClock.Replay(DateTimeOffset.UnixEpoch));

    [Fact]
    public void UninitializedDtosAndEnumValuesDoNotClaimSuccessOrPermission()
    {
        Assert.Equal(CouncilVoteValue.Unknown, default);
        Assert.Equal(GateDecisionKind.Unknown, default);
        Assert.Equal(TrajectoryGateDecisionKind.Unknown, default);
        foreach (var gate in new[] { new DecisionGateResult(), JsonSerializer.Deserialize<DecisionGateResult>("{}")! })
        {
            Assert.False(gate.Succeeded);
            Assert.False(gate.Accepted);
            Assert.Equal(GateDecisionKind.Unknown, gate.DecisionKind);
        }
        var trajectory = new TrajectoryGateResult();
        Assert.False(trajectory.Succeeded);
        Assert.False(trajectory.Accepted);
        Assert.Equal(TrajectoryGateDecisionKind.Unknown, trajectory.DecisionKind);
        Assert.False(trajectory.Trace.Accepted);
    }

    [Theory]
    [InlineData("new")]
    [InlineData("json")]
    [InlineData("null_input")]
    [InlineData("missing_logos")]
    [InlineData("missing_ethos")]
    [InlineData("missing_pathos")]
    public async Task MissingVotesAreDeniedEvenWhenOtherCouncilsApprove(string source)
    {
        var request = source switch
        {
            "json" => JsonSerializer.Deserialize<DecisionGateRequest>("{}")!,
            "null_input" => new DecisionGateRequest { GateInput = null! },
            "missing_logos" => new DecisionGateRequest { GateInput = new() { Ethos = CouncilVoteValue.Approve, Pathos = CouncilVoteValue.Approve } },
            "missing_ethos" => new DecisionGateRequest { GateInput = new() { Logos = CouncilVoteValue.Approve, Pathos = CouncilVoteValue.Approve } },
            "missing_pathos" => new DecisionGateRequest { GateInput = new() { Logos = CouncilVoteValue.Approve, Ethos = CouncilVoteValue.Approve } },
            _ => new DecisionGateRequest()
        };
        var result = await DecisionGate().EvaluateAsync(request, TestContext.Current.CancellationToken);
        Assert.True(result.Succeeded); // Evaluation completed; this is not authorization.
        Assert.False(result.Accepted);
        Assert.Equal(GateDecisionKind.Deny, result.DecisionKind);
        Assert.Equal(RejectReasonKind.FailClosed, Assert.Single(result.RejectReasons).Kind);
    }

    [Fact]
    public void DefaultCouncilEvaluationDoesNotSupplyApprovals()
    {
        var adapter = new CtgCouncilDecisionToGateInputAdapter(new CtgCouncilVoteExtractor());
        var input = adapter.Convert(new CouncilEvaluationResult());
        Assert.Equal(GateDecisionKind.Deny, CtgDecisionGateEvaluator.EvaluateDecision(input));
    }

    public static TheoryData<string, int> IncompleteSteps
    {
        get
        {
            var data = new TheoryData<string, int>();
            foreach (var source in new[] { "default_step", "json_gate", "null_step", "null_gate", "allow_only",
                "succeeded_only", "accepted_only", "missing_accepted", "missing_succeeded", "missing_decision", "invalid_decision" })
                foreach (var position in new[] { 0, 1, 2 })
                    data.Add(source, position);
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(IncompleteSteps))]
    public async Task IncompleteStepNeverAuthorizesTrajectory(string source, int position)
    {
        var steps = new[] { Allowed("first"), Allowed("second"), Allowed("third"), Denied("later-denied") };
        steps[position] = Incomplete(source)!;
        Assert.Equal(TrajectoryGateDecisionKind.Halt, CtgTrajectoryGateEvaluator.EvaluateDecision(steps[..3]));
        Assert.Equal(TrajectoryGateDecisionKind.Halt, CtgTrajectoryGateEvaluator.EvaluateDecision(steps));

        var result = await TrajectoryGate().EvaluateAsync(new TrajectoryGateRequest { Steps = steps }, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.False(result.Accepted);
        Assert.Equal(TrajectoryGateDecisionKind.Halt, result.DecisionKind);
        Assert.False(result.Trace.Accepted);
        Assert.Equal(result.DecisionKind, result.Trace.DecisionKind);
        Assert.Equal(position.ToString(System.Globalization.CultureInfo.InvariantCulture), result.Metadata["first_failing_step_index"]);
        var reason = Assert.Single(result.RejectReasons);
        Assert.Equal(RejectReasonKind.FailClosed, reason.Kind);
        Assert.Equal("FAIL_CLOSED", reason.ReasonCode);
        Assert.Equal(result.RejectReasons, result.Trace.RejectReasons);
    }

    [Theory]
    [InlineData("new")]
    [InlineData("json")]
    [InlineData("null_steps")]
    public async Task MissingTrajectoryStepsNeverAuthorizeContinuation(string source)
    {
        var request = source switch
        {
            "json" => JsonSerializer.Deserialize<TrajectoryGateRequest>("{}")!,
            "null_steps" => new TrajectoryGateRequest { Steps = null! },
            _ => new TrajectoryGateRequest()
        };
        var result = await TrajectoryGate().EvaluateAsync(request, TestContext.Current.CancellationToken);
        Assert.False(result.Accepted);
        Assert.Equal(TrajectoryGateDecisionKind.Halt, result.DecisionKind);
        Assert.Equal(RejectReasonKind.ImplicitDeny, Assert.Single(result.RejectReasons).Kind);
    }

    [Fact]
    public async Task AssemblingAnUninitializedGateDoesNotMakeItAnAllow()
    {
        var assembler = new CtgStepTraceAssembler(new CtgGovernanceTraceBuilder(KernelClock.Replay(DateTimeOffset.UnixEpoch)));
        var step = assembler.Assemble(new DecisionGateResult());
        var result = await TrajectoryGate().EvaluateAsync(new TrajectoryGateRequest { Steps = [step] }, TestContext.Current.CancellationToken);
        Assert.False(result.Accepted);
        Assert.Equal(TrajectoryGateDecisionKind.Halt, result.DecisionKind);
    }

    [Fact]
    public async Task ExplicitAllowsAndDenialsKeepTheirExistingResults()
    {
        var allowed = await TrajectoryGate().EvaluateAsync(new TrajectoryGateRequest { Steps = [Allowed("first"), Allowed("second")] }, TestContext.Current.CancellationToken);
        Assert.True(allowed.Succeeded);
        Assert.True(allowed.Accepted);
        Assert.Equal(TrajectoryGateDecisionKind.Continue, allowed.DecisionKind);
        Assert.Empty(allowed.RejectReasons);

        var denied = await TrajectoryGate().EvaluateAsync(new TrajectoryGateRequest { Steps = [Allowed("first"), Denied("second"), Incomplete("default_step")!] }, TestContext.Current.CancellationToken);
        Assert.False(denied.Accepted);
        Assert.Equal(TrajectoryGateDecisionKind.Halt, denied.DecisionKind);
        Assert.Equal("1", denied.Metadata["first_failing_step_index"]);
        Assert.Equal("second", denied.Metadata["first_failing_step_id"]);
        Assert.Equal("STEP_DENIED", Assert.Single(denied.RejectReasons).ReasonCode);
    }

    [Fact]
    public async Task NullRequestsFailBeforeProducingADecision()
    {
        Assert.Throws<ArgumentNullException>(() => CtgDecisionGateEvaluator.EvaluateDecision(null!));
        Assert.Throws<ArgumentNullException>(() => DecisionGate().Evaluate(null!));
        Assert.Throws<ArgumentNullException>(() => CtgTrajectoryGateEvaluator.EvaluateDecision(null!));
        Assert.Throws<ArgumentNullException>(() => TrajectoryGate().Evaluate(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => DecisionGate().EvaluateAsync(null!, TestContext.Current.CancellationToken).AsTask());
        await Assert.ThrowsAsync<ArgumentNullException>(() => TrajectoryGate().EvaluateAsync(null!, TestContext.Current.CancellationToken).AsTask());
    }

    private static StepGovernanceTrace Allowed(string id) => new()
    {
        StepId = id,
        DecisionGate = new() { Succeeded = true, Accepted = true, DecisionKind = GateDecisionKind.Allow }
    };

    private static StepGovernanceTrace Denied(string id) => new()
    {
        StepId = id,
        DecisionGate = new() { Succeeded = true, Accepted = false, DecisionKind = GateDecisionKind.Deny }
    };

    private static StepGovernanceTrace? Incomplete(string source)
    {
        if (source == "null_step") return null;
        if (source == "default_step") return new StepGovernanceTrace();
        return new StepGovernanceTrace
        {
            StepId = "incomplete",
            DecisionGate = source switch
            {
                "json_gate" => JsonSerializer.Deserialize<DecisionGateResult>("{}")!,
                "null_gate" => null!,
                "allow_only" => new() { DecisionKind = GateDecisionKind.Allow },
                "succeeded_only" => new() { Succeeded = true },
                "accepted_only" => new() { Accepted = true },
                "missing_accepted" => new() { Succeeded = true, DecisionKind = GateDecisionKind.Allow },
                "missing_succeeded" => new() { Accepted = true, DecisionKind = GateDecisionKind.Allow },
                "missing_decision" => new() { Succeeded = true, Accepted = true },
                "invalid_decision" => new() { Succeeded = true, Accepted = true, DecisionKind = (GateDecisionKind)(-1) },
                _ => throw new ArgumentOutOfRangeException(nameof(source))
            }
        };
    }
}
