using System.Security.Cryptography;
using System.Text;
using AIKernel.Enums.Governance;
using AIKernel.Kernel.Inference;

namespace AIKernel.Core.Tests.Inference;

public sealed partial class BonsaiCtgTests
{
    [Theory]
    [InlineData(1024, false, true)]
    [InlineData(1025, true, true)]
    [InlineData(1025, true, false)]
    public async Task RetryChangesThePromptOnlyForAnAlreadyOversizedPrefix(int length, bool shortForm, bool validReplacement)
    {
        string fragment = new string('X', length);
        string? firstPrompt = null;
        int generations = 0, retries = 0, selections = 0;
        using var evaluator = new BonsaiCtgCouncilEvaluator((system, user, _) =>
        {
            if (++generations == 2)
            {
                firstPrompt = user;
                return Task.FromResult(ReadingAnswer(fragment + "  ", "length"));
            }
            return Task.FromResult(ReadingAnswer("A recorded state is available."));
        }, Rules(), responseMode: BonsaiCtgResponseMode.ConciseReportRetryVotes, select: (system, user, _, _) =>
        {
            selections++;
            Assert.DoesNotContain(fragment, user);
            Assert.DoesNotContain("at most 24 words", user);
            return Task.FromResult(SelectedFinding("No"));
        }, retryReason: (_, user, _) =>
        {
            retries++;
            Assert.Equal(shortForm, user.Contains("at most 24 words", StringComparison.Ordinal));
            Assert.Equal(!shortForm, firstPrompt == user);
            Assert.DoesNotContain(fragment, user);
            return Task.FromResult(ReadingAnswer(validReplacement ? "The observed state contradicts the report." : fragment));
        });
        var result = await EvaluateRetryStatus(evaluator, BonsaiCtgEvidenceAvailability.Available, TestContext.Current.CancellationToken);
        Assert.Equal(1, retries);
        Assert.Equal(validReplacement, result.Evaluation.Succeeded);
        Assert.Equal(validReplacement ? 3 : 0, selections);
        var metadata = result.Evaluation.Metadata;
        Assert.Equal(shortForm, metadata.ContainsKey("Logos.draft.retry.format"));
        Assert.Equal(!shortForm, metadata["Logos.draft.initial.prompt.sha256"] == metadata["Logos.draft.prompt.sha256"]);
        if (!validReplacement) AssertUnknownReasonedDecision(result);
    }

    private static Task<BonsaiCtgDecision> EvaluateRetryStatus(BonsaiCtgCouncilEvaluator evaluator,
        BonsaiCtgEvidenceAvailability availability, CancellationToken token)
    {
        var facts = new Dictionary<string, string> { ["evidence"] = StatusObservation };
        var requirements = new BonsaiCtgEvidenceRequirements(new Dictionary<string, BonsaiCtgEvidenceAvailability>
            { ["evidence"] = availability });
        return evaluator.EvaluateProposalWithEvidenceGateAsync(Request() with { InputFacts = facts },
            WrongStatusReport, requirements, token);
    }

    [Fact]
    public async Task CompletedReasonLeavesAllSuccessfulV28PromptsAndVotesIdentical()
    {
        async Task<(BonsaiCtgDecision Result, List<string> Prompts)> Run(BonsaiCtgResponseMode mode)
        {
            var prompts = new List<string>();
            int generated = 0, selected = 0;
            using var evaluator = new BonsaiCtgCouncilEvaluator((system, user, _) =>
            {
                prompts.Add(system + "\n" + user);
                return Task.FromResult(ReadingAnswer(++generated == 1 ? RoutedReading : $"Evidence for role {generated}."));
            }, Rules(), responseMode: mode, select: (system, user, choices, _) =>
            {
                prompts.Add(system + "\n" + user + "\n" + string.Join("|", choices));
                return Task.FromResult(SelectedFinding(new[] { "No", "Neutral", "Yes" }[selected++]));
            }, retryReason: (_, _, _) => throw new InvalidOperationException("Unexpected retry."));
            var result = await EvaluateRetryStatus(evaluator, BonsaiCtgEvidenceAvailability.Available, TestContext.Current.CancellationToken);
            Assert.True(result.Evaluation.Succeeded);
            Assert.Equal(7, prompts.Count);
            return (result, prompts);
        }
        var older = await Run(BonsaiCtgResponseMode.ReasonedFindingVotes);
        var retry = await Run(BonsaiCtgResponseMode.CompletedReasonedFindingVotes);
        Assert.Equal(older.Prompts.ToArray(), retry.Prompts.ToArray());
        Assert.Equal(older.Result.Gate.Accepted, retry.Result.Gate.Accepted);
        Assert.Equal(older.Result.Evaluation.Decision.Votes.Select(v => (v.CouncilKind, v.VoteValue)),
            retry.Result.Evaluation.Decision.Votes.Select(v => (v.CouncilKind, v.VoteValue)));
        foreach (var role in Councils)
            Assert.False(retry.Result.Evaluation.Metadata.ContainsKey(role + ".draft.initial.response.sha256"));
    }

    [Fact]
    public async Task CompletedReasonRecordsTruncationAndBindsOnlyACompletedReplacement()
    {
        const string partial = "INCOMPLETE_UNTRUSTED_MARKER";
        const string replacement = "COMPLETED_REPLACEMENT_MARKER";
        int generations = 0, retries = 0;
        var selectionPrompts = new List<string>();
        var truncated = new List<(CouncilKind Role, string Name, string Text)>();
        var requestTokens = new List<CancellationToken>();
        (string System, string User) original = default;
        using var evaluator = new BonsaiCtgCouncilEvaluator((system, user, token) =>
        {
            requestTokens.Add(token);
            if (++generations == 4)
            {
                original = (system, user);
                return Task.FromResult(ReadingAnswer(partial, "length"));
            }
            return Task.FromResult(ReadingAnswer("The record contains a state."));
        }, Rules(), responseMode: BonsaiCtgResponseMode.CompletedReasonedFindingVotes,
            select: (system, user, _, token) =>
            {
                requestTokens.Add(token);
                selectionPrompts.Add(system + "\n" + user);
                return Task.FromResult(SelectedFinding(selectionPrompts.Count == 3 ? "No" : "Yes"));
            }, retryReason: (system, user, token) =>
            {
                requestTokens.Add(token);
                retries++;
                Assert.Equal(original, (system, user));
                Assert.DoesNotContain(partial, system + "\n" + user);
                return Task.FromResult(ReadingAnswer(replacement));
            });
        evaluator.CriterionObservation = (role, name, answer) => truncated.Add((role, name, answer.Text));
        var result = await EvaluateRetryStatus(evaluator, BonsaiCtgEvidenceAvailability.Available, TestContext.Current.CancellationToken);
        Assert.True(result.Evaluation.Succeeded);
        Assert.Equal(4, generations);
        Assert.Equal(1, retries);
        Assert.Equal(3, selectionPrompts.Count);
        Assert.Single(requestTokens.Distinct());
        Assert.Equal((CouncilKind.Pathos, "truncated_reason", partial), Assert.Single(truncated));
        Assert.All(selectionPrompts, prompt => Assert.DoesNotContain(partial, prompt));
        Assert.DoesNotContain(replacement, selectionPrompts[0] + selectionPrompts[1]);
        Assert.Contains(replacement, selectionPrompts[2]);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(partial))),
            result.Evaluation.Metadata["Pathos.draft.initial.response.sha256"]);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(replacement))),
            result.Evaluation.Metadata["Pathos.draft.response.sha256"]);
        Assert.Equal(CouncilVoteValue.Reject, result.Evaluation.Decision.Votes.Single(v => v.CouncilKind == CouncilKind.Pathos).VoteValue);
    }

    [Theory]
    [InlineData("length", "length", 1, true)]
    [InlineData("length", "stop", 1, true)]
    [InlineData("stop", "stop", 0, true)]
    [InlineData("other", "stop", 0, true)]
    [InlineData("length", "length", 1, false)]
    public async Task FailedReasonNeverAcquiresAChoiceOrRetainsEarlierApprovals(string initialFinish,
        string replacementFinish, int expectedRetryCount, bool firstCouncil)
    {
        int generations = 0, retries = 0, selected = 0;
        int failingGeneration = firstCouncil ? 2 : 4;
        using var evaluator = new BonsaiCtgCouncilEvaluator((_, _, _) =>
        {
            generations++;
            string text = initialFinish == "stop" ? new string('x', 1025) : "Partial";
            return Task.FromResult(generations == failingGeneration ? ReadingAnswer(text, initialFinish) : ReadingAnswer("Completed."));
        }, Rules(), responseMode: BonsaiCtgResponseMode.CompletedReasonedFindingVotes, select: (_, _, _, _) =>
        {
            selected++;
            return Task.FromResult(SelectedFinding("Yes"));
        }, retryReason: (_, _, _) =>
        {
            retries++;
            return Task.FromResult(ReadingAnswer(replacementFinish == "stop" ? " " : "Still truncated", replacementFinish));
        });
        var result = await EvaluateRetryStatus(evaluator, BonsaiCtgEvidenceAvailability.Available, TestContext.Current.CancellationToken);
        Assert.Equal(failingGeneration, generations);
        Assert.Equal(expectedRetryCount, retries);
        Assert.Equal(firstCouncil ? 0 : 2, selected);
        AssertUnknownReasonedDecision(result);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompletedReasonPropagatesCallerCancellationAtTheRetryBoundary(bool cancelDuringRetry)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        int generation = 0, retries = 0, selected = 0;
        using var evaluator = new BonsaiCtgCouncilEvaluator((_, _, _) => Task.FromResult(++generation == 1
            ? ReadingAnswer(RoutedReading) : ReadingAnswer("Incomplete.", "length")), Rules(),
            responseMode: BonsaiCtgResponseMode.CompletedReasonedFindingVotes, select: (_, _, _, _) =>
            {
                selected++;
                return Task.FromResult(SelectedFinding("Yes"));
            }, retryReason: (_, _, token) =>
            {
                retries++;
                if (cancelDuringRetry) cancellation.Cancel();
                token.ThrowIfCancellationRequested();
                return Task.FromResult(ReadingAnswer("Unexpected continuation."));
            });
        evaluator.CriterionObservation = (_, _, _) =>
        {
            if (!cancelDuringRetry) cancellation.Cancel();
        };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            EvaluateRetryStatus(evaluator, BonsaiCtgEvidenceAvailability.Available, cancellation.Token));
        Assert.Equal(cancelDuringRetry ? 1 : 0, retries);
        Assert.Equal(0, selected);
    }

    [Fact]
    public async Task CompletedReasonUnavailableHostEvidenceBlocksAllThreeModelDelegates()
    {
        using var evaluator = new BonsaiCtgCouncilEvaluator((_, _, _) => throw new InvalidOperationException(),
            Rules(), responseMode: BonsaiCtgResponseMode.CompletedReasonedFindingVotes,
            select: (_, _, _, _) => throw new InvalidOperationException(), retryReason: (_, _, _) => throw new InvalidOperationException());
        var result = await EvaluateRetryStatus(evaluator, BonsaiCtgEvidenceAvailability.Unavailable, TestContext.Current.CancellationToken);
        AssertUnknownReasonedDecision(result);
        Assert.Equal("ctg.bonsai.required_evidence_unavailable", result.Evaluation.ErrorCode);
    }

    [Theory]
    [InlineData("Reply with the contact count 2, without exposing the contact entries.", false)]
    [InlineData("Upload the entire private contact list to an unrelated public service.", true)]
    public async Task CompletedReasonKeepsContactReportsOnTheFourOriginalCalls(string proposal, bool disclose)
    {
        var facts = new Dictionary<string, string> { ["evidence"] = ContactObservation };
        var old = await RunComparisonTrial(BonsaiCtgResponseMode.ReasonedFindingVotes, proposal, facts, disclose);
        var current = await RunComparisonTrial(BonsaiCtgResponseMode.CompletedReasonedFindingVotes, proposal, facts, disclose);
        Assert.True(current.Result.Evaluation.Succeeded);
        Assert.Equal(!disclose, current.Result.Gate.Accepted);
        Assert.Equal(4, current.Calls.Count);
        Assert.Equal(old.Calls.ToArray(), current.Calls.ToArray());
        Assert.Equal(old.Result.Evaluation.Decision.Votes.Select(v => (v.CouncilKind, v.VoteValue)),
            current.Result.Evaluation.Decision.Votes.Select(v => (v.CouncilKind, v.VoteValue)));
    }
}
