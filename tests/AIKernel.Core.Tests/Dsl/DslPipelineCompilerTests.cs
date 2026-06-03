namespace AIKernel.Core.Tests.Dsl;

using AIKernel.Common.Results;
using AIKernel.Core.Dsl;
using AIKernel.Core.Time;
using AIKernel.Core.Tests.Support;
using Xunit;

public sealed class DslPipelineCompilerTests
{
    [Fact]
    public void FromJson_ParsesPipelineTree()
    {
        var document = DslDocument.FromJson("""
        {
          "type": "Pipeline",
          "steps": [
            { "type": "Step", "name": "start" },
            { "type": "CallCapability", "name": "Observe", "args": { "mode": "fast" } }
          ]
        }
        """);

        Assert.True(document.IsSuccess);
        var root = Assert.IsType<PipelineRootNode>(document.Value!.Root);
        Assert.Equal(2, root.Steps.Count);
        var call = Assert.IsType<CallCapabilityNode>(root.Steps[1]);
        Assert.Equal("Observe", call.Name);
        Assert.Equal("fast", call.Args["mode"]);
    }

    [Fact]
    public void FromJson_RejectsNonPipelineRoot()
    {
        var document = DslDocument.FromJson("""
        { "type": "Step", "name": "start" }
        """);

        Assert.True(document.IsFailure);
        Assert.Equal("INVALID_DSL", document.Error!.Code);
        Assert.Equal(FailureKind.Reject, document.Error.FailureKind);
    }

    [Fact]
    public void FromJson_RejectsNonObjectArgs()
    {
        var document = DslDocument.FromJson("""
        {
          "type": "Pipeline",
          "steps": [
            { "type": "CallCapability", "name": "Observe", "args": ["bad"] }
          ]
        }
        """);

        Assert.True(document.IsFailure);
        Assert.Equal("INVALID_DSL", document.Error!.Code);
        Assert.Equal(FailureKind.Reject, document.Error.FailureKind);
    }

    [Fact]
    public void FromJson_RejectsEmptyArgKey()
    {
        var document = DslDocument.FromJson("""
        {
          "type": "Pipeline",
          "steps": [
            { "type": "CallCapability", "name": "Observe", "args": { "": "bad" } }
          ]
        }
        """);

        Assert.True(document.IsFailure);
        Assert.Equal("INVALID_DSL", document.Error!.Code);
        Assert.Equal(FailureKind.Reject, document.Error.FailureKind);
    }

    [Fact]
    public void FromJson_RejectsOutOfRangeNumericTimeout()
    {
        var document = DslDocument.FromJson("""
        {
          "type": "Pipeline",
          "steps": [
            {
              "type": "LoopUntil",
              "timeout": 1000000000000000,
              "maxIterations": 1,
              "body": []
            }
          ]
        }
        """);

        Assert.True(document.IsFailure);
        Assert.Equal("INVALID_DSL", document.Error!.Code);
        Assert.Equal(FailureKind.Reject, document.Error.FailureKind);
    }

    [Fact]
    public void Compile_RejectsUnknownCapability()
    {
        var document = DslDocument.FromJson("""
        {
          "type": "Pipeline",
          "steps": [
            { "type": "CallCapability", "name": "Missing" }
          ]
        }
        """).Value!;
        var compiler = new DslPipelineCompiler(new TestCapabilityRegistry());

        var result = compiler.Compile(document);

        Assert.True(result.IsFailure);
        Assert.Equal("DSL_COMPILE_ERROR", result.Error!.Code);
        Assert.Equal(FailureKind.Reject, result.Error.FailureKind);
    }

    [Fact]
    public void Compile_NullDocumentReturnsFailClosedResult()
    {
        var compiler = new DslPipelineCompiler(new TestCapabilityRegistry());

        var result = compiler.Compile(null!);

        Assert.True(result.IsFailure);
        Assert.Equal("DSL_COMPILE_ERROR", result.Error!.Code);
        Assert.Equal(FailureKind.FailClosed, result.Error.FailureKind);
        Assert.Equal(OriginStep.KernelFacade, result.Error.OriginStep);
    }

    [Fact]
    public void Compile_NullRootReturnsFailClosedResult()
    {
        var compiler = new DslPipelineCompiler(new TestCapabilityRegistry());
        var document = new DslDocument(null!);

        var result = compiler.Compile(document);

        Assert.True(result.IsFailure);
        Assert.Equal("DSL_COMPILE_ERROR", result.Error!.Code);
        Assert.Equal(FailureKind.FailClosed, result.Error.FailureKind);
        Assert.Equal(OriginStep.KernelFacade, result.Error.OriginStep);
    }

    [Fact]
    public void Compile_RejectsNonPipelineRoot()
    {
        var compiler = new DslPipelineCompiler(new TestCapabilityRegistry());
        var document = new DslDocument(new StepNode("start"));

        var result = compiler.Compile(document);

        Assert.True(result.IsFailure);
        Assert.Equal("DSL_COMPILE_ERROR", result.Error!.Code);
        Assert.Equal(FailureKind.Reject, result.Error.FailureKind);
    }

    [Fact]
    public void Compile_CapabilityResolutionExceptionReturnsFailClosedResult()
    {
        var document = DslDocument.FromJson("""
        {
          "type": "Pipeline",
          "steps": [
            { "type": "CallCapability", "name": "ProbeFailure" }
          ]
        }
        """).Value!;
        var compiler = new DslPipelineCompiler(
            new TestCapabilityRegistry("ProbeFailure"));

        var result = compiler.Compile(document);

        Assert.True(result.IsFailure);
        Assert.Equal("UNHANDLED_EXCEPTION", result.Error!.Code);
        Assert.Equal(FailureKind.FailClosed, result.Error.FailureKind);
        Assert.Equal(OriginStep.Capability, result.Error.OriginStep);
        Assert.Equal(
            typeof(InvalidOperationException).FullName,
            result.Error.Metadata![ResultMetadataKeys.ExceptionType]);
        Assert.Equal("ProbeFailure", result.Error.Metadata["dsl.capability_name"]);
    }

    [Fact]
    public void Compile_NullPipelineStepsReturnsFailClosedResult()
    {
        var compiler = new DslPipelineCompiler(new TestCapabilityRegistry());
        var document = new DslDocument(new PipelineRootNode(null!));

        var result = compiler.Compile(document);

        Assert.True(result.IsFailure);
        Assert.Equal("DSL_COMPILE_ERROR", result.Error!.Code);
        Assert.Equal(FailureKind.FailClosed, result.Error.FailureKind);
    }

    [Fact]
    public void Compile_NullLoopBodyReturnsFailClosedResult()
    {
        var compiler = new DslPipelineCompiler(new TestCapabilityRegistry());
        var document = new DslDocument(new PipelineRootNode(
        [
            new LoopNode(1, null!)
        ]));

        var result = compiler.Compile(document);

        Assert.True(result.IsFailure);
        Assert.Equal("DSL_COMPILE_ERROR", result.Error!.Code);
        Assert.Equal(FailureKind.FailClosed, result.Error.FailureKind);
    }

    [Fact]
    public void Compile_NullCapabilityArgsReturnsFailClosedResult()
    {
        var compiler = new DslPipelineCompiler(new TestCapabilityRegistry());
        var document = new DslDocument(new PipelineRootNode(
        [
            new CallCapabilityNode("Observe", null!)
        ]));

        var result = compiler.Compile(document);

        Assert.True(result.IsFailure);
        Assert.Equal("DSL_COMPILE_ERROR", result.Error!.Code);
        Assert.Equal(FailureKind.FailClosed, result.Error.FailureKind);
    }

    [Fact]
    public void Compile_RejectsEmptyCapabilityArgKey()
    {
        var compiler = new DslPipelineCompiler(new TestCapabilityRegistry());
        var document = new DslDocument(new PipelineRootNode(
        [
            new CallCapabilityNode(
                "Observe",
                new Dictionary<string, string>
                {
                    [""] = "bad"
                })
        ]));

        var result = compiler.Compile(document);

        Assert.True(result.IsFailure);
        Assert.Equal("DSL_COMPILE_ERROR", result.Error!.Code);
        Assert.Equal(FailureKind.Reject, result.Error.FailureKind);
    }

    [Fact]
    public void Execute_IsDeterministic_ForSameDslAndInput()
    {
        var pipeline = Compile("""
        {
          "type": "Pipeline",
          "steps": [
            { "type": "Step", "name": "start" },
            {
              "type": "Loop",
              "maxIterations": 2,
              "body": [
                { "type": "CallCapability", "name": "Observe" },
                { "type": "CallCapability", "name": "Decide" }
              ]
            }
          ]
        }
        """);

        var first = pipeline.Execute(DslPipelineExecutionContext.Create());
        var second = pipeline.Execute(DslPipelineExecutionContext.Create());

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.StepId, second.StepId);
        Assert.Equal(first.ReplayLogHash, second.ReplayLogHash);
        Assert.Equal(7, first.ReplayLog.Count);
        ReplayMetadataAssertions.AssertReplayLogHash(first.ReplayLogHash);
    }

    [Fact]
    public void Execute_StopsAtSuspendAndDoesNotRunLaterNodes()
    {
        var pipeline = Compile("""
        {
          "type": "Pipeline",
          "steps": [
            { "type": "CallCapability", "name": "Observe" },
            { "type": "Suspend", "reason": "user_approval" },
            { "type": "CallCapability", "name": "ExecutePlan" }
          ]
        }
        """);

        var result = pipeline.Execute(DslPipelineExecutionContext.Create());

        Assert.True(result.IsFailure);
        Assert.True(result.IsSuspended);
        Assert.Equal(PipelineStep.SuspendErrorCode, result.Error!.Code);
        Assert.Equal("suspend", result.SemanticDelta.Kind);
        Assert.Equal("user_approval", result.SemanticDelta.Metadata![PipelineStepMetadataKeys.SuspendReason]);
        Assert.Equal(2, result.ReplayLog.Count);
    }

    [Fact]
    public void Execute_RecordsCapabilityFailureAsFailedReplayLogEntry()
    {
        var pipeline = Compile(
            """
            {
              "type": "Pipeline",
              "steps": [
                { "type": "CallCapability", "name": "RejectPlan" }
              ]
            }
            """,
            new TestCapabilityRegistry("RejectPlan"));

        var result = pipeline.Execute(DslPipelineExecutionContext.Create());

        Assert.True(result.IsFailure);
        Assert.Equal("CAPABILITY_REJECTED", result.Error!.Code);
        Assert.Equal(OriginStep.Capability, result.Error.OriginStep);
        var entry = Assert.Single(result.ReplayLog);
        Assert.False(entry.IsSuccess);
        Assert.Equal("CAPABILITY_REJECTED", entry.ErrorCode);
        Assert.Equal("dsl.capability.call", entry.SemanticDelta.Label);
        Assert.Equal("RejectPlan", entry.SemanticDelta.Metadata!["dsl.node_name"]);
    }

    [Fact]
    public void Execute_CapabilityExceptionReturnsFailClosedReplayEntry()
    {
        var pipeline = Compile(
            """
            {
              "type": "Pipeline",
              "steps": [
                { "type": "CallCapability", "name": "Explode" }
              ]
            }
            """,
            new TestCapabilityRegistry("Explode"));

        var result = pipeline.Execute(DslPipelineExecutionContext.Create());

        Assert.True(result.IsFailure);
        Assert.Equal("UNHANDLED_EXCEPTION", result.Error!.Code);
        Assert.Equal(FailureKind.FailClosed, result.Error.FailureKind);
        Assert.Equal(OriginStep.Capability, result.Error.OriginStep);
        Assert.Equal(
            typeof(InvalidOperationException).FullName,
            result.Error.Metadata![ResultMetadataKeys.ExceptionType]);
        Assert.Equal("Explode", result.Error.Metadata["dsl.capability_name"]);
        var entry = Assert.Single(result.ReplayLog);
        Assert.False(entry.IsSuccess);
        Assert.Equal("UNHANDLED_EXCEPTION", entry.ErrorCode);
        Assert.Equal("dsl.capability.call", entry.SemanticDelta.Label);
    }

    [Fact]
    public void Execute_CapabilitySuccessNullReturnsFailClosedReplayEntry()
    {
        var pipeline = Compile(
            """
            {
              "type": "Pipeline",
              "steps": [
                { "type": "CallCapability", "name": "NullSuccess" }
              ]
            }
            """,
            new TestCapabilityRegistry("NullSuccess"));

        var result = pipeline.Execute(DslPipelineExecutionContext.Create());

        Assert.True(result.IsFailure);
        Assert.Equal("DSL_RUNTIME_ERROR", result.Error!.Code);
        Assert.Equal(FailureKind.FailClosed, result.Error.FailureKind);
        Assert.Equal(OriginStep.Capability, result.Error.OriginStep);
        Assert.Equal("NullSuccess", result.Error.Metadata!["dsl.capability_name"]);
        var entry = Assert.Single(result.ReplayLog);
        Assert.False(entry.IsSuccess);
        Assert.Equal("DSL_RUNTIME_ERROR", entry.ErrorCode);
        Assert.Equal("dsl.capability.call", entry.SemanticDelta.Label);
    }

    [Fact]
    public void Execute_CapabilityNullDataReturnsFailClosedReplayEntry()
    {
        var pipeline = Compile(
            """
            {
              "type": "Pipeline",
              "steps": [
                { "type": "CallCapability", "name": "NullData" }
              ]
            }
            """,
            new TestCapabilityRegistry("NullData"));

        var result = pipeline.Execute(DslPipelineExecutionContext.Create());

        Assert.True(result.IsFailure);
        Assert.Equal("DSL_RUNTIME_ERROR", result.Error!.Code);
        Assert.Equal(FailureKind.FailClosed, result.Error.FailureKind);
        Assert.Equal(OriginStep.Capability, result.Error.OriginStep);
        Assert.Equal("NullData", result.Error.Metadata!["dsl.capability_name"]);
        var entry = Assert.Single(result.ReplayLog);
        Assert.False(entry.IsSuccess);
        Assert.Equal("DSL_RUNTIME_ERROR", entry.ErrorCode);
    }

    [Fact]
    public void Execute_CapabilityNullDataValueReturnsFailClosedReplayEntry()
    {
        var pipeline = Compile(
            """
            {
              "type": "Pipeline",
              "steps": [
                { "type": "CallCapability", "name": "NullDataValue" }
              ]
            }
            """,
            new TestCapabilityRegistry("NullDataValue"));

        var result = pipeline.Execute(DslPipelineExecutionContext.Create());

        Assert.True(result.IsFailure);
        Assert.Equal("DSL_RUNTIME_ERROR", result.Error!.Code);
        Assert.Equal(FailureKind.FailClosed, result.Error.FailureKind);
        Assert.Equal(OriginStep.Capability, result.Error.OriginStep);
        Assert.Equal("NullDataValue", result.Error.Metadata!["dsl.capability_name"]);
        var entry = Assert.Single(result.ReplayLog);
        Assert.False(entry.IsSuccess);
        Assert.Equal("DSL_RUNTIME_ERROR", entry.ErrorCode);
    }

    [Fact]
    public void Execute_CapabilityEmptyDataKeyReturnsFailClosedReplayEntry()
    {
        var pipeline = Compile(
            """
            {
              "type": "Pipeline",
              "steps": [
                { "type": "CallCapability", "name": "EmptyDataKey" }
              ]
            }
            """,
            new TestCapabilityRegistry("EmptyDataKey"));

        var result = pipeline.Execute(DslPipelineExecutionContext.Create());

        Assert.True(result.IsFailure);
        Assert.Equal("DSL_RUNTIME_ERROR", result.Error!.Code);
        Assert.Equal(FailureKind.FailClosed, result.Error.FailureKind);
        Assert.Equal(OriginStep.Capability, result.Error.OriginStep);
        Assert.Equal("EmptyDataKey", result.Error.Metadata!["dsl.capability_name"]);
        var entry = Assert.Single(result.ReplayLog);
        Assert.False(entry.IsSuccess);
        Assert.Equal("DSL_RUNTIME_ERROR", entry.ErrorCode);
    }

    [Fact]
    public void Execute_LoopUntilStopsBeforeBodyWhenTimeoutReached()
    {
        var pipeline = Compile("""
        {
          "type": "Pipeline",
          "steps": [
            {
              "type": "LoopUntil",
              "timeout": "00:00:00",
              "maxIterations": 10,
              "body": [
                { "type": "CallCapability", "name": "Observe" }
              ]
            }
          ]
        }
        """);

        var result = pipeline.Execute(DslPipelineExecutionContext.Create());

        Assert.True(result.IsSuccess);
        var entry = Assert.Single(result.ReplayLog);
        Assert.Equal("loop", entry.SemanticDelta.Kind);
        Assert.Equal("timeout_reached", entry.SemanticDelta.Metadata![PipelineStepMetadataKeys.LoopDecision]);
        Assert.False(result.Value!.Data.ContainsKey("last_capability"));
    }

    [Fact]
    public void Execute_LoopUntilClockExceptionReturnsFailClosedReplayEntry()
    {
        var pipeline = Compile(
            """
            {
              "type": "Pipeline",
              "steps": [
                {
                  "type": "LoopUntil",
                  "timeout": "00:00:10",
                  "maxIterations": 1,
                  "body": [
                    { "type": "CallCapability", "name": "Observe" }
                  ]
                }
              ]
            }
            """,
            clock: new ThrowingClock());

        var result = pipeline.Execute(DslPipelineExecutionContext.Create());

        Assert.True(result.IsFailure);
        Assert.Equal("UNHANDLED_EXCEPTION", result.Error!.Code);
        Assert.Equal(FailureKind.FailClosed, result.Error.FailureKind);
        Assert.Equal("clock_failed", result.Error.Metadata![PipelineStepMetadataKeys.LoopDecision]);
        Assert.Equal("0", result.Error.Metadata[PipelineStepMetadataKeys.LoopIteration]);
        var entry = Assert.Single(result.ReplayLog);
        Assert.False(entry.IsSuccess);
        Assert.Equal("UNHANDLED_EXCEPTION", entry.ErrorCode);
        Assert.Equal("loop", entry.SemanticDelta.Kind);
        Assert.Equal("clock_failed", entry.SemanticDelta.Metadata![PipelineStepMetadataKeys.LoopDecision]);
    }

    [Fact]
    public void Execute_NullContextReturnsFailClosedReplayEntry()
    {
        var pipeline = Compile("""
        {
          "type": "Pipeline",
          "steps": [
            { "type": "Step", "name": "start" }
          ]
        }
        """);

        var result = pipeline.Execute(null!);

        Assert.True(result.IsFailure);
        Assert.Equal("DSL_RUNTIME_ERROR", result.Error!.Code);
        Assert.Equal(FailureKind.FailClosed, result.Error.FailureKind);
        var entry = Assert.Single(result.ReplayLog);
        Assert.False(entry.IsSuccess);
        Assert.Equal("dsl.context.invalid", entry.SemanticDelta.Label);
    }

    [Fact]
    public void Execute_NullInputReturnsFailClosedReplayEntry()
    {
        var pipeline = Compile("""
        {
          "type": "Pipeline",
          "steps": [
            { "type": "Step", "name": "start" }
          ]
        }
        """);
        var context = new DslPipelineExecutionContext(
            null!,
            DateTimeOffset.UnixEpoch);

        var result = pipeline.Execute(context);

        Assert.True(result.IsFailure);
        Assert.Equal("DSL_RUNTIME_ERROR", result.Error!.Code);
        Assert.Equal(FailureKind.FailClosed, result.Error.FailureKind);
        var entry = Assert.Single(result.ReplayLog);
        Assert.False(entry.IsSuccess);
        Assert.Equal("dsl.context.invalid", entry.SemanticDelta.Label);
    }

    [Fact]
    public void Execute_NullInputDataReturnsFailClosedReplayEntry()
    {
        var pipeline = Compile("""
        {
          "type": "Pipeline",
          "steps": [
            { "type": "Step", "name": "start" }
          ]
        }
        """);
        var context = new DslPipelineExecutionContext(
            new DslPipelineValue(null!),
            DateTimeOffset.UnixEpoch);

        var result = pipeline.Execute(context);

        Assert.True(result.IsFailure);
        Assert.Equal("DSL_RUNTIME_ERROR", result.Error!.Code);
        Assert.Equal(FailureKind.FailClosed, result.Error.FailureKind);
        var entry = Assert.Single(result.ReplayLog);
        Assert.False(entry.IsSuccess);
        Assert.Equal("dsl.context.invalid", entry.SemanticDelta.Label);
    }

    private static IKernelPipeline Compile(
        string json,
        IDslCapabilityRegistry? registry = null,
        IKernelClock? clock = null)
    {
        var document = DslDocument.FromJson(json);
        Assert.True(document.IsSuccess, document.Error?.Message);

        var compiler = new DslPipelineCompiler(
            registry ?? new TestCapabilityRegistry(),
            clock ?? KernelClock.Replay(DateTimeOffset.UnixEpoch));
        var pipeline = compiler.Compile(document.Value!);
        Assert.True(pipeline.IsSuccess, pipeline.Error?.Message);

        return pipeline.Value!;
    }

    private sealed class ThrowingClock : IKernelClock
    {
        public TimeProvider Physical => TimeProvider.System;

        public KernelTimeProvider Logical { get; } =
            new ReplayKernelTimeProvider(DateTimeOffset.UnixEpoch, TimeProvider.System);

        public DateTimeOffset Now =>
            throw new InvalidOperationException("Clock failed.");

        public bool IsReplaying => true;

        public double ReliabilityScore => 1.0;

        public KernelTimestamp GetLogicalTimestamp()
        {
            return KernelTimestamp.FromUtc(DateTimeOffset.UnixEpoch, "test");
        }
    }

    private sealed class TestCapabilityRegistry : IDslCapabilityRegistry
    {
        private readonly HashSet<string> _known = new(StringComparer.Ordinal)
        {
            "Observe",
            "Decide",
            "ExecutePlan"
        };

        public TestCapabilityRegistry(params string[] additionalKnown)
        {
            foreach (var name in additionalKnown)
            {
                _known.Add(name);
            }
        }

        public bool Contains(string name)
        {
            if (string.Equals(name, "ProbeFailure", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Capability probe failed.");
            }

            return _known.Contains(name);
        }

        public Result<DslPipelineValue> Invoke(
            string name,
            DslPipelineValue input,
            IReadOnlyDictionary<string, string> args)
        {
            if (string.Equals(name, "Explode", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Capability exploded.");
            }

            if (string.Equals(name, "RejectPlan", StringComparison.Ordinal))
            {
                return Result<DslPipelineValue>.Fail(new ErrorContext(
                    "Capability rejected the plan.",
                    "CAPABILITY_REJECTED",
                    false)
                {
                    FailureKind = FailureKind.Reject,
                    OriginStep = OriginStep.Capability,
                    SemanticSlot = SemanticSlot.T
                });
            }

            if (string.Equals(name, "NullSuccess", StringComparison.Ordinal))
            {
                return Result<DslPipelineValue>.Success(null!);
            }

            if (string.Equals(name, "NullData", StringComparison.Ordinal))
            {
                return Result<DslPipelineValue>.Success(
                    new DslPipelineValue(null!));
            }

            if (string.Equals(name, "NullDataValue", StringComparison.Ordinal))
            {
                return Result<DslPipelineValue>.Success(
                    new DslPipelineValue(new Dictionary<string, string>
                    {
                        ["bad"] = null!
                    }));
            }

            if (string.Equals(name, "EmptyDataKey", StringComparison.Ordinal))
            {
                return Result<DslPipelineValue>.Success(
                    new DslPipelineValue(new Dictionary<string, string>
                    {
                        [""] = "bad"
                    }));
            }

            return Contains(name)
                ? Result<DslPipelineValue>.Success(input
                    .With("last_capability", name)
                    .With($"capability.{name}", "called"))
                : Result<DslPipelineValue>.Fail(new ErrorContext(
                    $"Unknown capability: {name}.",
                    "UNKNOWN_CAPABILITY",
                    false)
                {
                    FailureKind = FailureKind.Reject,
                    OriginStep = OriginStep.Capability,
                    SemanticSlot = SemanticSlot.T
                });
        }
    }
}
