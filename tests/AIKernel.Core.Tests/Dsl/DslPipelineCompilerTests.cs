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

    private static IKernelPipeline Compile(string json)
    {
        var document = DslDocument.FromJson(json);
        Assert.True(document.IsSuccess, document.Error?.Message);

        var compiler = new DslPipelineCompiler(
            new TestCapabilityRegistry(),
            KernelClock.Replay(DateTimeOffset.UnixEpoch));
        var pipeline = compiler.Compile(document.Value!);
        Assert.True(pipeline.IsSuccess, pipeline.Error?.Message);

        return pipeline.Value!;
    }

    private sealed class TestCapabilityRegistry : IDslCapabilityRegistry
    {
        private static readonly HashSet<string> Known = new(StringComparer.Ordinal)
        {
            "Observe",
            "Decide",
            "ExecutePlan"
        };

        public bool Contains(string name)
        {
            return Known.Contains(name);
        }

        public Result<DslPipelineValue> Invoke(
            string name,
            DslPipelineValue input,
            IReadOnlyDictionary<string, string> args)
        {
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
