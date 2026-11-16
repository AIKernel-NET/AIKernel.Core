namespace AIKernel.Core.Tests.Governance;

using AIKernel.Core.Governance;
using AIKernel.Core.Time;
using AIKernel.Dtos.Governance;
using AIKernel.Enums.Governance;
using Xunit;

public sealed class CtgIntegratedSafetyTests
{
    [Theory]
    [InlineData(-1, false)]
    [InlineData(-1, true)]
    [InlineData(4, false)]
    [InlineData(4, true)]
    [InlineData(int.MinValue, false)]
    [InlineData(int.MinValue, true)]
    public async Task UndefinedEthosAndIncompleteGateHaltAtTheFirstUnsafeStep(
        int ethos, bool incompleteFirst)
    {
        var clock = KernelClock.Replay(DateTimeOffset.UnixEpoch);
        var decisionGate = new CtgDecisionGateEvaluator(clock);
        var cancellationToken = TestContext.Current.CancellationToken;
        var allowed = await decisionGate.EvaluateAsync(new DecisionGateRequest
        {
            StepId = "allowed",
            GateInput = new GateInput
            {
                Logos = CouncilVoteValue.Approve,
                Ethos = CouncilVoteValue.Approve,
                Pathos = CouncilVoteValue.Approve
            }
        }, cancellationToken);
        var denied = await decisionGate.EvaluateAsync(new DecisionGateRequest
        {
            StepId = "undefined-ethos",
            GateInput = new GateInput
            {
                Logos = CouncilVoteValue.Approve,
                Ethos = (CouncilVoteValue)ethos,
                Pathos = CouncilVoteValue.Approve
            }
        }, cancellationToken);

        Assert.True(allowed.Succeeded);
        Assert.True(allowed.Accepted);
        Assert.Equal(GateDecisionKind.Allow, allowed.DecisionKind);
        Assert.True(denied.Succeeded);
        Assert.False(denied.Accepted);
        Assert.Equal(GateDecisionKind.Deny, denied.DecisionKind);
        Assert.Equal("ctg.unknown_vote_value", Assert.Single(denied.Diagnostics).Code);

        var validStep = new StepGovernanceTrace { StepId = "allowed", DecisionGate = allowed };
        var deniedStep = new StepGovernanceTrace { StepId = "undefined-ethos", DecisionGate = denied };
        var incomplete = new StepGovernanceTrace { StepId = "incomplete" };
        StepGovernanceTrace[] steps = incompleteFirst
            ? [validStep, incomplete, deniedStep]
            : [validStep, deniedStep, incomplete];
        var trajectory = await new CtgTrajectoryGateEvaluator(clock).EvaluateAsync(
            new TrajectoryGateRequest { Steps = steps }, cancellationToken);

        Assert.True(trajectory.Succeeded);
        Assert.False(trajectory.Accepted);
        Assert.False(trajectory.Trace.Accepted);
        Assert.Equal(TrajectoryGateDecisionKind.Halt, trajectory.DecisionKind);
        Assert.Equal("1", trajectory.Metadata["first_failing_step_index"]);
        Assert.Equal(incompleteFirst ? "incomplete" : "undefined-ethos",
            trajectory.Metadata["first_failing_step_id"]);
        Assert.Equal(incompleteFirst ? RejectReasonKind.FailClosed : RejectReasonKind.StepDenied,
            Assert.Single(trajectory.RejectReasons).Kind);
    }
}
