namespace AIKernel.Core.Tests.Governance;

using AIKernel.Abstractions.Governance;
using AIKernel.Core.Governance;
using AIKernel.Core.Time;
using AIKernel.Dtos.Governance;
using AIKernel.Enums.Governance;
using AIKernel.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public sealed class CtgGovernanceTests
{
    private static readonly DateTimeOffset FixedNow = new(
        2026,
        6,
        14,
        0,
        0,
        0,
        TimeSpan.Zero);

    [Fact]
    public void Extract_CouncilDecision_ReturnsTriadicVoteInput()
    {
        var extractor = new CtgCouncilVoteExtractor();
        var decision = CreateCouncilDecision(
            CouncilVoteValue.Reject,
            CouncilVoteValue.Approve,
            CouncilVoteValue.Abstain);

        var input = extractor.Extract(decision);

        Assert.Equal(CouncilVoteValue.Reject, input.Logos);
        Assert.Equal(CouncilVoteValue.Approve, input.Ethos);
        Assert.Equal(CouncilVoteValue.Abstain, input.Pathos);
    }

    [Fact]
    public void Convert_CouncilEvaluationResult_MissingCouncil_ReturnsUnknownVote()
    {
        var adapter = new CtgCouncilDecisionToGateInputAdapter(new CtgCouncilVoteExtractor());
        var result = new CouncilEvaluationResult
        {
            Decision = new CouncilDecision
            {
                Votes =
                [
                    CreateVote(CouncilKind.Logos, CouncilVoteValue.Approve),
                    CreateVote(CouncilKind.Ethos, CouncilVoteValue.Abstain)
                ]
            }
        };

        var input = adapter.Convert(result);

        Assert.Equal(CouncilVoteValue.Approve, input.Logos);
        Assert.Equal(CouncilVoteValue.Abstain, input.Ethos);
        Assert.Equal(CouncilVoteValue.Unknown, input.Pathos);
    }

    [Fact]
    public async Task EvaluateAsync_EthosReject_DeniesWithEthosVeto()
    {
        var evaluator = CreateDecisionGateEvaluator();
        var canonReference = CreateCanonReference("Canon.CTG.Monolith.Gate.Decision");
        var request = new DecisionGateRequest
        {
            OperationId = "op-1",
            StepId = "step-1",
            GateInput = new GateInput
            {
                Logos = CouncilVoteValue.Approve,
                Ethos = CouncilVoteValue.Reject,
                Pathos = CouncilVoteValue.Approve
            },
            CanonReferences = [canonReference]
        };

        var result = await evaluator.EvaluateAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.False(result.Accepted);
        Assert.Equal(GateDecisionKind.Deny, result.DecisionKind);
        var reason = Assert.Single(result.RejectReasons);
        Assert.Equal(RejectReasonKind.EthosVeto, reason.Kind);
        Assert.Equal("ETHOS_VETO", reason.ReasonCode);
        Assert.Equal([canonReference], reason.CanonReferences);
    }

    [Fact]
    public async Task EvaluateAsync_TwoApprovals_Allows()
    {
        var evaluator = CreateDecisionGateEvaluator();
        var request = new DecisionGateRequest
        {
            OperationId = "op-2",
            StepId = "step-2",
            GateInput = new GateInput
            {
                Logos = CouncilVoteValue.Approve,
                Ethos = CouncilVoteValue.Approve,
                Pathos = CouncilVoteValue.Abstain
            }
        };

        var result = await evaluator.EvaluateAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.True(result.Accepted);
        Assert.Equal(GateDecisionKind.Allow, result.DecisionKind);
        Assert.Empty(result.RejectReasons);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public async Task EvaluateAsync_AllValidVoteCombinations_FollowsDiscreteTruthTable()
    {
        var evaluator = CreateDecisionGateEvaluator();
        var values = new[]
        {
            CouncilVoteValue.Approve,
            CouncilVoteValue.Abstain,
            CouncilVoteValue.Reject
        };

        foreach (var logos in values)
        {
            foreach (var ethos in values)
            {
                foreach (var pathos in values)
                {
                    var input = new GateInput
                    {
                        Logos = logos,
                        Ethos = ethos,
                        Pathos = pathos
                    };
                    var expected = ethos == CouncilVoteValue.Reject
                        ? GateDecisionKind.Deny
                        : CountApprovals(input) >= 2
                            ? GateDecisionKind.Allow
                            : GateDecisionKind.Deny;

                    var result = await evaluator.EvaluateAsync(
                        new DecisionGateRequest
                        {
                            OperationId = "truth-table",
                            StepId = $"{logos}-{ethos}-{pathos}",
                            GateInput = input
                        },
                        TestContext.Current.CancellationToken);

                    Assert.Equal(expected, result.DecisionKind);
                    Assert.Equal(expected == GateDecisionKind.Allow, result.Accepted);
                }
            }
        }
    }

    [Fact]
    public async Task EvaluateAsync_UnknownVote_DeniesWithFailClosedDiagnostic()
    {
        var evaluator = CreateDecisionGateEvaluator();
        var request = new DecisionGateRequest
        {
            OperationId = "op-3",
            StepId = "step-3",
            GateInput = new GateInput
            {
                Logos = CouncilVoteValue.Unknown,
                Ethos = CouncilVoteValue.Approve,
                Pathos = CouncilVoteValue.Approve
            }
        };

        var result = await evaluator.EvaluateAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.False(result.Accepted);
        Assert.Equal(GateDecisionKind.Deny, result.DecisionKind);
        Assert.Equal(RejectReasonKind.FailClosed, Assert.Single(result.RejectReasons).Kind);
        Assert.Equal("ctg.unknown_vote_value", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public async Task EvaluateAsync_ContinuousCarriersChange_DoesNotChangeDecision()
    {
        var extractor = new CtgCouncilVoteExtractor();
        var evaluator = CreateDecisionGateEvaluator();
        var lowConfidenceDecision = CreateCouncilDecision(
            CouncilVoteValue.Approve,
            CouncilVoteValue.Approve,
            CouncilVoteValue.Abstain,
            confidence: 0.1,
            riskScore: 0.9);
        var highConfidenceDecision = CreateCouncilDecision(
            CouncilVoteValue.Approve,
            CouncilVoteValue.Approve,
            CouncilVoteValue.Abstain,
            confidence: 0.9,
            riskScore: 0.1);

        var first = await evaluator.EvaluateAsync(
            new DecisionGateRequest
            {
                OperationId = "op-4a",
                StepId = "step-4a",
                GateInput = extractor.Extract(lowConfidenceDecision)
            },
            TestContext.Current.CancellationToken);
        var second = await evaluator.EvaluateAsync(
            new DecisionGateRequest
            {
                OperationId = "op-4b",
                StepId = "step-4b",
                GateInput = extractor.Extract(highConfidenceDecision)
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(GateDecisionKind.Allow, first.DecisionKind);
        Assert.Equal(first.DecisionKind, second.DecisionKind);
        Assert.Equal(first.Accepted, second.Accepted);
    }

    [Fact]
    public async Task EvaluateAsync_EmptySteps_HaltsWithImplicitDeny()
    {
        var evaluator = CreateTrajectoryGateEvaluator();

        var result = await evaluator.EvaluateAsync(
            new TrajectoryGateRequest
            {
                OperationId = "op-5",
                Steps = []
            },
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.False(result.Accepted);
        Assert.Equal(TrajectoryGateDecisionKind.Halt, result.DecisionKind);
        Assert.Equal(RejectReasonKind.ImplicitDeny, Assert.Single(result.RejectReasons).Kind);
    }

    [Fact]
    public async Task EvaluateAsync_FirstDeniedStep_HaltsWithStepDenied()
    {
        var evaluator = CreateTrajectoryGateEvaluator();
        var steps = new[]
        {
            CreateStepTrace("step-1", GateDecisionKind.Allow),
            CreateStepTrace("step-2", GateDecisionKind.Deny),
            CreateStepTrace("step-3", GateDecisionKind.Deny)
        };

        var result = await evaluator.EvaluateAsync(
            new TrajectoryGateRequest
            {
                OperationId = "op-6",
                Steps = steps,
                TraceId = "trace-6"
            },
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.False(result.Accepted);
        Assert.Equal(TrajectoryGateDecisionKind.Halt, result.DecisionKind);
        Assert.Equal(RejectReasonKind.StepDenied, Assert.Single(result.RejectReasons).Kind);
        Assert.Equal("1", result.Metadata["first_failing_step_index"]);
        Assert.Equal("step-2", result.Metadata["first_failing_step_id"]);
        Assert.Equal("trace-6", result.Trace.TraceId);
    }

    [Fact]
    public async Task EvaluateAsync_AllAllowedSteps_Continues()
    {
        var evaluator = CreateTrajectoryGateEvaluator();

        var result = await evaluator.EvaluateAsync(
            new TrajectoryGateRequest
            {
                OperationId = "op-7",
                Steps =
                [
                    CreateStepTrace("step-1", GateDecisionKind.Allow),
                    CreateStepTrace("step-2", GateDecisionKind.Allow)
                ]
            },
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.True(result.Accepted);
        Assert.Equal(TrajectoryGateDecisionKind.Continue, result.DecisionKind);
        Assert.Empty(result.RejectReasons);
    }

    [Fact]
    public void Resolve_MissingReference_DoesNotCreateCanonReference()
    {
        var resolver = new CtgCanonReferenceResolver();

        var references = resolver.Resolve(new CtgMergedRomDescriptor
        {
            CouncilReferences =
            [
                new CanonReference
                {
                    CanonId = " ",
                    Path = "Canon.CTG.Monolith.Council.Logos"
                }
            ]
        });

        Assert.Empty(references);
    }

    [Fact]
    public void Resolve_DuplicateReference_PreservesContentHashAndDeduplicates()
    {
        var resolver = new CtgCanonReferenceResolver();
        var first = new CanonReference
        {
            CanonId = " Canon.CTG.Monolith ",
            Path = " Canon.CTG.Monolith.Council.Logos ",
            Section = " 3.2 ",
            Anchor = " logos ",
            ContentHash = " sha256:abc "
        };
        var duplicate = first with
        {
            CanonId = "Canon.CTG.Monolith",
            Path = "Canon.CTG.Monolith.Council.Logos",
            Section = "3.2",
            Anchor = "logos",
            ContentHash = "sha256:abc"
        };

        var references = resolver.Resolve(new CtgMergedRomDescriptor
        {
            CanonReference = first,
            CouncilReferences = [duplicate]
        });

        var reference = Assert.Single(references);
        Assert.Equal("Canon.CTG.Monolith", reference.CanonId);
        Assert.Equal("Canon.CTG.Monolith.Council.Logos", reference.Path);
        Assert.Equal("3.2", reference.Section);
        Assert.Equal("logos", reference.Anchor);
        Assert.Equal("sha256:abc", reference.ContentHash);
    }

    [Fact]
    public void BuildStepTrace_CouncilAndGateCarriers_BuildsTraceWithoutDecisionLogic()
    {
        var builder = new CtgGovernanceTraceBuilder(KernelClock.Replay(FixedNow));
        var councilReason = CreateRejectReason(RejectReasonKind.FailClosed);
        var gateReason = CreateRejectReason(RejectReasonKind.StepDenied);
        var councilReference = CreateCanonReference("Canon.CTG.Monolith.Council.Logos");
        var gateReference = CreateCanonReference("Canon.CTG.Monolith.Gate.Decision");
        var councilEvaluation = new CouncilEvaluationResult
        {
            OperationId = "op-8",
            Succeeded = true,
            Decision = new CouncilDecision
            {
                DecisionId = "decision-8",
                RejectReasons = [councilReason],
                CanonReferences = [councilReference]
            },
            RejectReasons = [councilReason],
            CanonReferences = [councilReference],
            CorrelationId = "corr-8",
            TraceId = "trace-8"
        };
        var decisionGate = new DecisionGateResult
        {
            OperationId = "op-8",
            DecisionKind = GateDecisionKind.Deny,
            RejectReasons = [gateReason],
            CanonReferences = [gateReference],
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["step_id"] = "step-from-gate"
            }
        };

        var trace = builder.BuildStepTrace(councilEvaluation, decisionGate);

        Assert.Equal("step-from-gate", trace.StepId);
        Assert.Equal("trace-8", trace.TraceId);
        Assert.Contains(councilReference, trace.CanonReferences);
        Assert.Contains(gateReference, trace.CanonReferences);
        Assert.Contains(councilReason, trace.RejectReasons);
        Assert.Contains(gateReason, trace.RejectReasons);
    }

    [Fact]
    public void Assemble_DecisionGateOnly_AllowsNullCanonReferences()
    {
        var assembler = new CtgStepTraceAssembler(
            new CtgGovernanceTraceBuilder(KernelClock.Replay(FixedNow)));
        var decisionGate = new DecisionGateResult
        {
            OperationId = "op-9",
            Succeeded = true,
            DecisionKind = GateDecisionKind.Allow,
            Accepted = true,
            TraceId = "trace-9",
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["step_id"] = "step-9"
            }
        };

        var trace = assembler.Assemble(decisionGate);

        Assert.Equal("step-9", trace.StepId);
        Assert.Equal("trace-9", trace.TraceId);
        Assert.Empty(trace.CanonReferences);
        Assert.Empty(trace.RejectReasons);
        Assert.Equal(GateDecisionKind.Allow, trace.DecisionGate.DecisionKind);
    }

    [Fact]
    public void ToReasonCode_PascalCaseKind_ReturnsUpperSnakeCase()
    {
        Assert.Equal(
            "SAFETY_VIOLATION",
            CtgRejectReasonClassifier.ToReasonCode(RejectReasonKind.SafetyViolation));
        Assert.Equal(
            "IMPLICIT_DENY",
            CtgRejectReasonClassifier.ToReasonCode(RejectReasonKind.ImplicitDeny));
    }

    [Fact]
    public void ToReasonCode_AllRejectReasonKinds_ReturnsCanonicalCodes()
    {
        var expected = new Dictionary<RejectReasonKind, string>
        {
            [RejectReasonKind.Unknown] = "UNKNOWN",
            [RejectReasonKind.SafetyViolation] = "SAFETY_VIOLATION",
            [RejectReasonKind.LogicalInconsistency] = "LOGICAL_INCONSISTENCY",
            [RejectReasonKind.ContextMisalignment] = "CONTEXT_MISALIGNMENT",
            [RejectReasonKind.IrreversibleAction] = "IRREVERSIBLE_ACTION",
            [RejectReasonKind.InsufficientInformation] = "INSUFFICIENT_INFORMATION",
            [RejectReasonKind.OpaqueReasoning] = "OPAQUE_REASONING",
            [RejectReasonKind.EthosVeto] = "ETHOS_VETO",
            [RejectReasonKind.FailClosed] = "FAIL_CLOSED",
            [RejectReasonKind.StepDenied] = "STEP_DENIED",
            [RejectReasonKind.ImplicitDeny] = "IMPLICIT_DENY"
        };

        foreach (var pair in expected)
        {
            Assert.Equal(pair.Value, CtgRejectReasonClassifier.ToReasonCode(pair.Key));
        }
    }

    [Fact]
    public async Task CtgGovernanceService_EvaluatesGateAssemblesTraceAndEvaluatesTrajectory()
    {
        var service = new CtgGovernanceService(
            CreateDecisionGateEvaluator(),
            CreateTrajectoryGateEvaluator(),
            new CtgStepTraceAssembler(new CtgGovernanceTraceBuilder(KernelClock.Replay(FixedNow))));
        var councilEvaluation = new CouncilEvaluationResult
        {
            OperationId = "op-10",
            Succeeded = true,
            Decision = CreateCouncilDecision(
                CouncilVoteValue.Approve,
                CouncilVoteValue.Approve,
                CouncilVoteValue.Abstain),
            TraceId = "trace-10"
        };

        var decisionGate = await service.EvaluateDecisionGateAsync(
            new GateInput
            {
                Logos = CouncilVoteValue.Approve,
                Ethos = CouncilVoteValue.Approve,
                Pathos = CouncilVoteValue.Abstain
            },
            TestContext.Current.CancellationToken);
        var stepTrace = service.AssembleStepTrace(councilEvaluation, decisionGate);
        var trajectory = await service.EvaluateTrajectoryGateAsync(
            [stepTrace],
            TestContext.Current.CancellationToken);

        Assert.Equal(GateDecisionKind.Allow, decisionGate.DecisionKind);
        Assert.Equal(TrajectoryGateDecisionKind.Continue, trajectory.DecisionKind);
        Assert.Single(trajectory.Trace.Steps);
    }

    [Fact]
    public async Task CtgGovernanceService_CanonReferenceSource_AttachesReferencesToRequests()
    {
        var decisionReference = CreateCanonReference("Canon.CTG.Monolith.Gate.Decision");
        var trajectoryReference = CreateCanonReference("Canon.CTG.Monolith.Gate.Trajectory");
        var service = new CtgGovernanceService(
            CreateDecisionGateEvaluator(),
            CreateTrajectoryGateEvaluator(),
            new CtgStepTraceAssembler(new CtgGovernanceTraceBuilder(KernelClock.Replay(FixedNow))),
            new CtgStaticCanonReferenceSource(
                [decisionReference, trajectoryReference],
                [decisionReference],
                [trajectoryReference]));

        var decision = await service.EvaluateDecisionGateAsync(
            new GateInput
            {
                Logos = CouncilVoteValue.Approve,
                Ethos = CouncilVoteValue.Approve,
                Pathos = CouncilVoteValue.Abstain
            },
            TestContext.Current.CancellationToken);
        var trajectory = await service.EvaluateTrajectoryGateAsync(
            [CreateStepTrace("step-11", GateDecisionKind.Allow)],
            TestContext.Current.CancellationToken);

        Assert.Contains(decisionReference, decision.CanonReferences);
        Assert.Contains(trajectoryReference, trajectory.Trace.CanonReferences);
    }

    [Fact]
    public void CtgStaticCanonReferenceSource_MergedDescriptor_NormalizesReferenceSets()
    {
        var decisionReference = CreateCanonReference(" Canon.CTG.Monolith.Gate.Decision ");
        var trajectoryReference = CreateCanonReference(" Canon.CTG.Monolith.Gate.Trajectory ");
        var source = new CtgStaticCanonReferenceSource(new CtgMergedRomDescriptor
        {
            DecisionGateReference = decisionReference,
            TrajectoryGateReference = trajectoryReference
        });

        Assert.Equal("Canon.CTG.Monolith.Gate.Decision", Assert.Single(source.GetDecisionGateReferences()).Path);
        Assert.Equal("Canon.CTG.Monolith.Gate.Trajectory", Assert.Single(source.GetTrajectoryGateReferences()).Path);
        Assert.Equal(2, source.GetAllReferences().Count);
    }

    [Fact]
    public void AddAIKernelCore_CtgGovernanceServices_RegistersContractImplementations()
    {
        var services = new ServiceCollection();

        services.AddAIKernelCore(KernelClock.Replay(FixedNow));

        using var provider = services.BuildServiceProvider();
        var decisionGate = provider.GetRequiredService<CtgDecisionGateEvaluator>();
        var trajectoryGate = provider.GetRequiredService<CtgTrajectoryGateEvaluator>();

        Assert.Same(decisionGate, provider.GetRequiredService<IDecisionGate>());
        Assert.Same(trajectoryGate, provider.GetRequiredService<ITrajectoryGate>());
        Assert.NotNull(provider.GetRequiredService<CtgCouncilVoteExtractor>());
        Assert.NotNull(provider.GetRequiredService<CtgRejectReasonClassifier>());
        Assert.NotNull(provider.GetRequiredService<CtgCanonReferenceResolver>());
        Assert.NotNull(provider.GetRequiredService<CtgRomLocaleYamlAdapter>());
        Assert.NotNull(provider.GetRequiredService<CtgGovernanceTraceBuilder>());
        Assert.NotNull(provider.GetRequiredService<CtgCouncilDecisionToGateInputAdapter>());
        Assert.NotNull(provider.GetRequiredService<CtgStepTraceAssembler>());
        Assert.NotNull(provider.GetRequiredService<ICtgCanonReferenceSource>());
        Assert.NotNull(provider.GetRequiredService<ICtgGovernanceService>());
    }

    [Fact]
    public void AddCtgGovernance_ExistingDecisionGate_DoesNotReplaceRegistration()
    {
        var services = new ServiceCollection();
        var existingGate = new StubDecisionGate();

        services.AddSingleton<IDecisionGate>(existingGate);
        services.AddCtgGovernance();

        using var provider = services.BuildServiceProvider();

        Assert.Same(existingGate, provider.GetRequiredService<IDecisionGate>());
        Assert.NotNull(provider.GetRequiredService<CtgDecisionGateEvaluator>());
    }

    [Fact]
    public void Parse_EnAndJaLocaleYaml_ProducesSameCanonReferenceIds()
    {
        var adapter = new CtgRomLocaleYamlAdapter(
            new YamlDotNet.Serialization.DeserializerBuilder().Build(),
            new CtgCanonReferenceResolver(),
            KernelClock.Replay(FixedNow));

        var en = adapter.Parse(CreateLocaleYaml("en-US", suffix: string.Empty));
        var ja = adapter.Parse(CreateLocaleYaml("ja-JP", suffix: ".ja"));

        Assert.True(en.Succeeded);
        Assert.True(ja.Succeeded);
        Assert.Equal(
            en.CanonReferences.Select(reference => reference.CanonId).Order(StringComparer.Ordinal),
            ja.CanonReferences.Select(reference => reference.CanonId).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Parse_ContentHashesByPath_AttachesContentHash()
    {
        var adapter = new CtgRomLocaleYamlAdapter(
            new YamlDotNet.Serialization.DeserializerBuilder().Build(),
            new CtgCanonReferenceResolver(),
            KernelClock.Replay(FixedNow));

        var result = adapter.Parse(
            CreateLocaleYaml("en-US", suffix: string.Empty),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["rom/governance/gate.decision.monolith.md"] = "sha256:decision"
            });

        var decisionGateReference = Assert.Single(
            result.CanonReferences,
            reference => reference.CanonId == "Canon.CTG.Monolith.Gate.Decision");
        Assert.Equal("sha256:decision", decisionGateReference.ContentHash);
    }

    [Fact]
    public void Parse_MissingPathAndReference_EmitsFailClosedDiagnostics()
    {
        var adapter = new CtgRomLocaleYamlAdapter(
            new YamlDotNet.Serialization.DeserializerBuilder().Build(),
            new CtgCanonReferenceResolver(),
            KernelClock.Replay(FixedNow));
        const string yaml = """
id: "ctg.monolith.minimal"
canon:
  path: ""
  canonReference: ""
councils: []
decisionGate:
  policyPath: "rom/governance/gate.decision.monolith.md"
  canonReference: "Canon.CTG.Monolith.Gate.Decision"
trajectoryGate:
  policyPath: "rom/governance/gate.trajectory.monolith.md"
  canonReference: "Canon.CTG.Monolith.Gate.Trajectory"
rejectPolicy:
  rulesPath: "rom/governance/reject.policy.monolith.md"
  canonReference: "Canon.CTG.Monolith.RejectPolicy"
""";

        var result = adapter.Parse(yaml, "rom/locales/en-US/ctg.monolith.minimal.en.yaml");

        Assert.False(result.Succeeded);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "ctg.rom.missing_path");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "ctg.rom.missing_canon_reference");
        Assert.DoesNotContain(
            result.CanonReferences,
            reference => reference.CanonId == "Canon.CTG.Monolith.Canon");
    }

    private static CtgDecisionGateEvaluator CreateDecisionGateEvaluator()
    {
        return new CtgDecisionGateEvaluator(KernelClock.Replay(FixedNow));
    }

    private static CtgTrajectoryGateEvaluator CreateTrajectoryGateEvaluator()
    {
        return new CtgTrajectoryGateEvaluator(KernelClock.Replay(FixedNow));
    }

    private static CouncilDecision CreateCouncilDecision(
        CouncilVoteValue logos,
        CouncilVoteValue ethos,
        CouncilVoteValue pathos,
        double? confidence = null,
        double? riskScore = null)
    {
        return new CouncilDecision
        {
            DecisionId = "decision",
            DecisionKind = CouncilDecisionKind.Approved,
            Confidence = confidence,
            RiskScore = riskScore,
            Votes =
            [
                CreateVote(CouncilKind.Pathos, pathos, confidence, riskScore),
                CreateVote(CouncilKind.Logos, logos, confidence, riskScore),
                CreateVote(CouncilKind.Ethos, ethos, confidence, riskScore)
            ]
        };
    }

    private static CouncilVote CreateVote(
        CouncilKind councilKind,
        CouncilVoteValue voteValue,
        double? confidence = null,
        double? riskScore = null)
    {
        return new CouncilVote
        {
            VoteId = councilKind.ToString(),
            CouncilKind = councilKind,
            VoteValue = voteValue,
            Confidence = confidence,
            RiskScore = riskScore
        };
    }

    private static StepGovernanceTrace CreateStepTrace(
        string stepId,
        GateDecisionKind gateDecisionKind)
    {
        return new StepGovernanceTrace
        {
            TraceId = "trace",
            StepId = stepId,
            DecisionGate = new DecisionGateResult
            {
                OperationId = "op",
                Succeeded = true,
                DecisionKind = gateDecisionKind,
                Accepted = gateDecisionKind == GateDecisionKind.Allow
            }
        };
    }

    private static CanonReference CreateCanonReference(string path)
    {
        return new CanonReference
        {
            CanonId = "Canon.CTG.Monolith",
            Path = path,
            Section = "1"
        };
    }

    private static RejectReasonInfo CreateRejectReason(RejectReasonKind kind)
    {
        return new RejectReasonInfo
        {
            ReasonId = kind.ToString(),
            Kind = kind,
            ReasonCode = CtgRejectReasonClassifier.ToReasonCode(kind)
        };
    }

    private static string CreateLocaleYaml(
        string locale,
        string suffix)
    {
        return $$"""
id: "ctg.monolith.minimal"
version: "0.1.1-rc5"
canonVersion: "0.1.1-rc5"
schemaVersion: "0.1.1"
locale: "{{locale}}"
canon:
  path: "rom/governance/ctg.monolith.canon{{suffix}}.md"
  canonReference: "Canon.CTG.Monolith.Canon"
councils:
  - kind: "Logos"
    id: "council.logos"
    rulesPath: "rom/governance/council.logos.monolith{{suffix}}.md"
    canonReference: "Canon.CTG.Monolith.Council.Logos"
  - kind: "Ethos"
    id: "council.ethos"
    rulesPath: "rom/governance/council.ethos.monolith{{suffix}}.md"
    canonReference: "Canon.CTG.Monolith.Council.Ethos"
  - kind: "Pathos"
    id: "council.pathos"
    rulesPath: "rom/governance/council.pathos.monolith{{suffix}}.md"
    canonReference: "Canon.CTG.Monolith.Council.Pathos"
decisionGate:
  id: "gate.decision"
  policyPath: "rom/governance/gate.decision.monolith{{suffix}}.md"
  canonReference: "Canon.CTG.Monolith.Gate.Decision"
trajectoryGate:
  id: "gate.trajectory"
  policyPath: "rom/governance/gate.trajectory.monolith{{suffix}}.md"
  canonReference: "Canon.CTG.Monolith.Gate.Trajectory"
rejectPolicy:
  id: "reject.minimal"
  rulesPath: "rom/governance/reject.policy.monolith{{suffix}}.md"
  canonReference: "Canon.CTG.Monolith.RejectPolicy"
telemetry:
  includeGateContinuousValues: false
""";
    }

    private static int CountApprovals(GateInput input)
    {
        var count = 0;

        if (input.Logos == CouncilVoteValue.Approve)
        {
            count++;
        }

        if (input.Ethos == CouncilVoteValue.Approve)
        {
            count++;
        }

        if (input.Pathos == CouncilVoteValue.Approve)
        {
            count++;
        }

        return count;
    }

    private sealed class StubDecisionGate : IDecisionGate
    {
        public ValueTask<DecisionGateResult> EvaluateAsync(
            DecisionGateRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return ValueTask.FromResult(new DecisionGateResult
            {
                OperationId = request.OperationId,
                Succeeded = true,
                DecisionKind = GateDecisionKind.Deny,
                Accepted = false
            });
        }
    }
}
