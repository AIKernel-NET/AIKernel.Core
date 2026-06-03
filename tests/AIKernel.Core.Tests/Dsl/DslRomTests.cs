namespace AIKernel.Core.Tests.Dsl;

using AIKernel.Common.Results;
using AIKernel.Core.Dsl;
using AIKernel.Core.Time;
using AIKernel.Core.Vfs.Memory;
using AIKernel.Core.Tests.Support;
using AIKernel.Vfs;
using Xunit;

public sealed class DslRomTests
{
    [Fact]
    public async Task SaveDslAsRomAsync_SavesDslToVfsAndRegistersCapability()
    {
        await using var session = await OpenSessionAsync();
        var registry = new DslRomRegistry();
        var compiler = CreateCompiler(new TestCapabilityRegistry());
        var store = new DslRomStore(new DslRomProvider(compiler), registry);

        var metadata = await store.SaveDslAsRomAsync(
            session,
            "agent",
            "plan1",
            PlanDsl,
            DateTimeOffset.UnixEpoch);

        Assert.True(metadata.IsSuccess, metadata.Error?.Message);
        Assert.Equal("rom/dsl/agent/plan1.json", metadata.Value!.Path);
        Assert.Equal("dsl://agent/plan1", metadata.Value.CapabilityName);
        Assert.Equal(DslRomHasher.ComputeHash(PlanDsl), metadata.Value.RomHash);
        Assert.True(registry.Contains("dsl://agent/plan1"));
        Assert.True(await session.ExistsAsync("rom/dsl/agent/plan1.json"));
    }

    [Fact]
    public async Task SaveDslAsRomAsync_RejectsChangedContentForExistingRomPath()
    {
        await using var session = await OpenSessionAsync();
        var registry = new DslRomRegistry();
        var compiler = CreateCompiler(new TestCapabilityRegistry());
        var store = new DslRomStore(new DslRomProvider(compiler), registry);

        var first = await store.SaveDslAsRomAsync(
            session,
            "agent",
            "plan1",
            PlanDsl,
            DateTimeOffset.UnixEpoch);
        var second = await store.SaveDslAsRomAsync(
            session,
            "agent",
            "plan1",
            MutatedPlanDsl,
            DateTimeOffset.UnixEpoch);

        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.True(second.IsFailure);
        Assert.Equal("DSL_ROM_ERROR", second.Error!.Code);
        Assert.Equal(FailureKind.FailClosed, second.Error.FailureKind);
    }

    [Fact]
    public async Task LoadDslRomAsync_RejectsTamperedContentWhenHashChanges()
    {
        await using var session = await OpenSessionAsync();
        var compiler = CreateCompiler(new TestCapabilityRegistry());
        var store = new DslRomStore(
            new DslRomProvider(compiler),
            new DslRomRegistry());
        var saved = await store.SaveDslAsRomAsync(
            session,
            "agent",
            "plan1",
            PlanDsl,
            DateTimeOffset.UnixEpoch);
        Assert.True(saved.IsSuccess, saved.Error?.Message);

        await session.WriteFileAsync(
            saved.Value!.Path,
            System.Text.Encoding.UTF8.GetBytes(MutatedPlanDsl));

        var loaded = await store.LoadDslRomAsync(
            session,
            "agent",
            "plan1",
            DateTimeOffset.UnixEpoch,
            saved.Value.RomHash);

        Assert.True(loaded.IsFailure);
        Assert.Equal("DSL_ROM_ERROR", loaded.Error!.Code);
        Assert.Equal(FailureKind.FailClosed, loaded.Error.FailureKind);
        Assert.Contains("hash", loaded.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DslRomRegistry_RejectsSnapshotWithNullPipeline()
    {
        var registry = new DslRomRegistry();
        var snapshot = new DslRomSnapshot(
            CreateMetadata(PlanDsl),
            PlanDsl,
            Pipeline: null!);

        var result = registry.Register(snapshot);

        Assert.True(result.IsFailure);
        Assert.Equal("DSL_ROM_ERROR", result.Error!.Code);
        Assert.Equal(FailureKind.FailClosed, result.Error.FailureKind);
        Assert.Contains("pipeline", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DslRomRegistry_RejectsSnapshotWhenHashDoesNotMatchJson()
    {
        var registry = new DslRomRegistry();
        var pipeline = Compile(PlanDsl, new TestCapabilityRegistry());
        var metadata = CreateMetadata(PlanDsl) with
        {
            RomHash = DslRomHasher.ComputeHash(MutatedPlanDsl)
        };
        var snapshot = new DslRomSnapshot(metadata, PlanDsl, pipeline);

        var result = registry.Register(snapshot);

        Assert.True(result.IsFailure);
        Assert.Equal("DSL_ROM_ERROR", result.Error!.Code);
        Assert.Equal(FailureKind.FailClosed, result.Error.FailureKind);
        Assert.Contains("hash", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DslRomRegistry_RejectsSnapshotWithoutDslCapabilityScheme()
    {
        var registry = new DslRomRegistry();
        var pipeline = Compile(PlanDsl, new TestCapabilityRegistry());
        var snapshot = new DslRomSnapshot(
            CreateMetadata(PlanDsl) with
            {
                CapabilityName = "agent/plan1"
            },
            PlanDsl,
            pipeline);

        var result = registry.Register(snapshot);

        Assert.True(result.IsFailure);
        Assert.Equal("DSL_ROM_ERROR", result.Error!.Code);
        Assert.Equal(FailureKind.FailClosed, result.Error.FailureKind);
        Assert.Contains("dsl://", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DslRomCapabilityRegistry_ExecutesRegisteredRomDeterministically()
    {
        await using var session = await OpenSessionAsync();
        var leafRegistry = new TestCapabilityRegistry();
        var romRegistry = new DslRomRegistry();
        var compiler = CreateCompiler(new DslRomCapabilityRegistry(leafRegistry, romRegistry));
        var store = new DslRomStore(new DslRomProvider(compiler), romRegistry);
        var saved = await store.SaveDslAsRomAsync(
            session,
            "agent",
            "plan1",
            PlanDsl,
            DateTimeOffset.UnixEpoch);
        Assert.True(saved.IsSuccess, saved.Error?.Message);

        var rootPipeline = Compile(
            """
            {
              "type": "Pipeline",
              "steps": [
                { "type": "CallCapability", "name": "dsl://agent/plan1" }
              ]
            }
            """,
            new DslRomCapabilityRegistry(leafRegistry, romRegistry));

        var first = rootPipeline.Execute(DslPipelineExecutionContext.Create());
        var second = rootPipeline.Execute(DslPipelineExecutionContext.Create());

        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.True(second.IsSuccess, second.Error?.Message);
        Assert.Equal(first.StepId, second.StepId);
        Assert.Equal(first.ReplayLogHash, second.ReplayLogHash);
        Assert.Equal(saved.Value!.RomHash, first.Value!.Data[DslRomMetadataKeys.RomHash]);
        Assert.Equal("dsl://agent/plan1", first.Value.Data[DslRomMetadataKeys.RomCall]);
        Assert.Equal("agent", first.Value.Data[DslRomMetadataKeys.RomNamespace]);
        Assert.Equal("plan1", first.Value.Data[DslRomMetadataKeys.RomName]);
        Assert.True(first.Value.Data.ContainsKey(DslRomMetadataKeys.RomReplayLogCount));
        Assert.True(first.Value.Data.ContainsKey(DslRomMetadataKeys.RomReplayLogHash));

        var entry = Assert.Single(first.ReplayLog);
        Assert.Equal("dsl.capability.call", entry.SemanticDelta.Label);
        Assert.Equal(saved.Value.RomHash, entry.SemanticDelta.Metadata![DslRomMetadataKeys.RomHash]);
        Assert.Equal("dsl://agent/plan1", entry.SemanticDelta.Metadata[DslRomMetadataKeys.RomCall]);
        Assert.Equal("agent", entry.SemanticDelta.Metadata[DslRomMetadataKeys.RomNamespace]);
        Assert.Equal("plan1", entry.SemanticDelta.Metadata[DslRomMetadataKeys.RomName]);
        Assert.Equal(
            first.Value.Data[DslRomMetadataKeys.RomReplayLogCount],
            entry.SemanticDelta.Metadata[DslRomMetadataKeys.RomReplayLogCount]);
        Assert.Equal(
            first.Value.Data[DslRomMetadataKeys.RomReplayLogHash],
            entry.SemanticDelta.Metadata[DslRomMetadataKeys.RomReplayLogHash]);
        ReplayMetadataAssertions.AssertReplayLogHash(first.ReplayLogHash);
    }

    [Fact]
    public async Task DslRomCapabilityRegistry_PropagatesRomHashOnSuspendFailure()
    {
        await using var session = await OpenSessionAsync();
        var leafRegistry = new TestCapabilityRegistry();
        var romRegistry = new DslRomRegistry();
        var compiler = CreateCompiler(new DslRomCapabilityRegistry(leafRegistry, romRegistry));
        var store = new DslRomStore(new DslRomProvider(compiler), romRegistry);
        var saved = await store.SaveDslAsRomAsync(
            session,
            "agent",
            "approval",
            SuspendDsl,
            DateTimeOffset.UnixEpoch);
        Assert.True(saved.IsSuccess, saved.Error?.Message);

        var rootPipeline = Compile(
            """
            {
              "type": "Pipeline",
              "steps": [
                { "type": "CallCapability", "name": "dsl://agent/approval" }
              ]
            }
            """,
            new DslRomCapabilityRegistry(leafRegistry, romRegistry));

        var result = rootPipeline.Execute(DslPipelineExecutionContext.Create());

        Assert.True(result.IsFailure);
        Assert.True(result.IsSuspended);
        Assert.Equal(saved.Value!.RomHash, result.Error!.Metadata![DslRomMetadataKeys.RomHash]);
        Assert.Equal("1", result.Error.Metadata[DslRomMetadataKeys.RomReplayLogCount]);
        Assert.True(result.Error.Metadata.ContainsKey(DslRomMetadataKeys.RomReplayLogHash));
        var entry = Assert.Single(result.ReplayLog);
        Assert.Equal(saved.Value.RomHash, entry.SemanticDelta.Metadata![DslRomMetadataKeys.RomHash]);
        Assert.Equal("1", entry.SemanticDelta.Metadata[DslRomMetadataKeys.RomReplayLogCount]);
        Assert.Equal(
            result.Error.Metadata[DslRomMetadataKeys.RomReplayLogHash],
            entry.SemanticDelta.Metadata[DslRomMetadataKeys.RomReplayLogHash]);
    }

    [Fact]
    public void DslRomPath_RejectsTraversalIdentity()
    {
        var path = DslRomPath.Create("../escape", "plan1");

        Assert.True(path.IsFailure);
        Assert.Equal("DSL_ROM_ERROR", path.Error!.Code);
        Assert.Equal(FailureKind.FailClosed, path.Error.FailureKind);
    }

    [Fact]
    public void DslRomCapabilityRegistry_ReturnsFailure_WhenRomPipelineThrows()
    {
        var romRegistry = new DslRomRegistry();
        var metadata = CreateMetadata(PlanDsl);
        var registered = romRegistry.Register(new DslRomSnapshot(
            metadata,
            PlanDsl,
            new ThrowingPipeline()));
        Assert.True(registered.IsSuccess, registered.Error?.Message);
        var registry = new DslRomCapabilityRegistry(
            new TestCapabilityRegistry(),
            romRegistry);

        var result = registry.Invoke(
            metadata.CapabilityName,
            DslPipelineValue.Empty,
            new Dictionary<string, string>());

        Assert.True(result.IsFailure);
        Assert.Equal("UNHANDLED_EXCEPTION", result.Error!.Code);
        Assert.Equal(FailureKind.FailClosed, result.Error.FailureKind);
        Assert.Equal(metadata.RomHash, result.Error.Metadata![DslRomMetadataKeys.RomHash]);
        Assert.Equal(metadata.CapabilityName, result.Error.Metadata[DslRomMetadataKeys.RomCall]);
    }

    [Fact]
    public void DslRomCapabilityRegistry_ReturnsFailure_WhenRomPipelineReturnsNullSuccess()
    {
        var romRegistry = new DslRomRegistry();
        var metadata = CreateMetadata(PlanDsl);
        var registered = romRegistry.Register(new DslRomSnapshot(
            metadata,
            PlanDsl,
            new NullSuccessPipeline()));
        Assert.True(registered.IsSuccess, registered.Error?.Message);
        var registry = new DslRomCapabilityRegistry(
            new TestCapabilityRegistry(),
            romRegistry);

        var result = registry.Invoke(
            metadata.CapabilityName,
            DslPipelineValue.Empty,
            new Dictionary<string, string>());

        Assert.True(result.IsFailure);
        Assert.Equal("DSL_ROM_ERROR", result.Error!.Code);
        Assert.Equal(FailureKind.FailClosed, result.Error.FailureKind);
        Assert.Equal(metadata.RomHash, result.Error.Metadata![DslRomMetadataKeys.RomHash]);
        Assert.Contains("null", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DslRomCapabilityRegistry_ReturnsFailure_WhenRomResolveThrows()
    {
        var registry = new DslRomCapabilityRegistry(
            new TestCapabilityRegistry(),
            new ThrowingRomRegistry());

        var result = registry.Invoke(
            "dsl://agent/plan1",
            DslPipelineValue.Empty,
            new Dictionary<string, string>());

        Assert.True(result.IsFailure);
        Assert.Equal("UNHANDLED_EXCEPTION", result.Error!.Code);
        Assert.Equal(FailureKind.FailClosed, result.Error.FailureKind);
        Assert.Equal("dsl://agent/plan1", result.Error.Metadata!["dsl.capability_name"]);
    }

    [Fact]
    public void DslRomCapabilityRegistry_ReturnsFailure_WhenInnerInvokeThrows()
    {
        var registry = new DslRomCapabilityRegistry(
            new ThrowingCapabilityRegistry(),
            new DslRomRegistry());

        var result = registry.Invoke(
            "Observe",
            DslPipelineValue.Empty,
            new Dictionary<string, string>());

        Assert.True(result.IsFailure);
        Assert.Equal("UNHANDLED_EXCEPTION", result.Error!.Code);
        Assert.Equal(FailureKind.FailClosed, result.Error.FailureKind);
        Assert.Equal("Observe", result.Error.Metadata!["dsl.capability_name"]);
    }

    [Fact]
    public void DslRomCapabilityRegistry_ReturnsFailure_WhenResolvedSnapshotIsInvalid()
    {
        var metadata = CreateMetadata(PlanDsl) with
        {
            RomHash = ""
        };
        var registry = new DslRomCapabilityRegistry(
            new TestCapabilityRegistry(),
            new FixedRomRegistry(new DslRomSnapshot(
                metadata,
                PlanDsl,
                Compile(PlanDsl, new TestCapabilityRegistry()))));

        var result = registry.Invoke(
            metadata.CapabilityName,
            DslPipelineValue.Empty,
            new Dictionary<string, string>());

        Assert.True(result.IsFailure);
        Assert.Equal("DSL_ROM_ERROR", result.Error!.Code);
        Assert.Equal(FailureKind.FailClosed, result.Error.FailureKind);
        Assert.Contains("hash", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<IVfsSession> OpenSessionAsync()
    {
        var provider = new MemoryFileProvider(
            new MemoryFileProviderOptions
            {
                Clock = KernelClock.Replay(DateTimeOffset.UnixEpoch)
            });

        return await provider.OpenSessionAsync(new TestVfsCredentials());
    }

    private static IDslPipelineCompiler CreateCompiler(
        IDslCapabilityRegistry registry)
        => new DslPipelineCompiler(
            registry,
            KernelClock.Replay(DateTimeOffset.UnixEpoch));

    private static IKernelPipeline Compile(
        string json,
        IDslCapabilityRegistry registry)
    {
        var document = DslDocument.FromJson(json);
        Assert.True(document.IsSuccess, document.Error?.Message);

        var compiled = CreateCompiler(registry).Compile(document.Value!);
        Assert.True(compiled.IsSuccess, compiled.Error?.Message);
        return compiled.Value!;
    }

    private static DslRomMetadata CreateMetadata(string json)
        => new(
            "agent",
            "plan1",
            "rom/dsl/agent/plan1.json",
            "dsl://agent/plan1",
            DslRomHasher.ComputeHash(json),
            DateTimeOffset.UnixEpoch);

    private const string PlanDsl = """
        {
          "type": "Pipeline",
          "steps": [
            { "type": "CallCapability", "name": "Observe" },
            {
              "type": "Loop",
              "maxIterations": 2,
              "body": [
                { "type": "CallCapability", "name": "Decide" }
              ]
            }
          ]
        }
        """;

    private const string MutatedPlanDsl = """
        {
          "type": "Pipeline",
          "steps": [
            { "type": "CallCapability", "name": "Observe" },
            { "type": "CallCapability", "name": "ExecutePlan" }
          ]
        }
        """;

    private const string SuspendDsl = """
        {
          "type": "Pipeline",
          "steps": [
            { "type": "Suspend", "reason": "user_approval" }
          ]
        }
        """;

    private sealed class TestCapabilityRegistry : IDslCapabilityRegistry
    {
        private static readonly HashSet<string> Known = new(StringComparer.Ordinal)
        {
            "Observe",
            "Decide",
            "ExecutePlan"
        };

        public bool Contains(string name) => Known.Contains(name);

        public Result<DslPipelineValue> Invoke(
            string name,
            DslPipelineValue input,
            IReadOnlyDictionary<string, string> args)
        {
            return Contains(name)
                ? Result<DslPipelineValue>.Success(input.With($"capability.{name}", "called"))
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

    private sealed class ThrowingPipeline : IKernelPipeline
    {
        public ResultStep<DslPipelineState, DslPipelineValue> Execute(
            DslPipelineExecutionContext context)
        {
            throw new InvalidOperationException("ROM pipeline exploded.");
        }
    }

    private sealed class NullSuccessPipeline : IKernelPipeline
    {
        public ResultStep<DslPipelineState, DslPipelineValue> Execute(
            DslPipelineExecutionContext context)
            => ResultStep<DslPipelineState, DslPipelineValue>
                .Success(
                    DslPipelineState.Initial("dsl.pipeline"),
                    null!);
    }

    private sealed class ThrowingCapabilityRegistry : IDslCapabilityRegistry
    {
        public bool Contains(string name) => true;

        public Result<DslPipelineValue> Invoke(
            string name,
            DslPipelineValue input,
            IReadOnlyDictionary<string, string> args)
        {
            throw new InvalidOperationException("Inner capability exploded.");
        }
    }

    private sealed class ThrowingRomRegistry : IDslRomRegistry
    {
        public Result<DslRomMetadata> Register(DslRomSnapshot snapshot)
            => throw new InvalidOperationException("ROM register exploded.");

        public bool Contains(string capabilityName) => true;

        public Result<DslRomSnapshot> Resolve(string capabilityName)
            => throw new InvalidOperationException("ROM resolve exploded.");
    }

    private sealed class FixedRomRegistry(DslRomSnapshot snapshot) : IDslRomRegistry
    {
        private readonly DslRomSnapshot _snapshot = snapshot;

        public Result<DslRomMetadata> Register(DslRomSnapshot snapshot)
            => Result<DslRomMetadata>.Success(snapshot.Metadata);

        public bool Contains(string capabilityName) => true;

        public Result<DslRomSnapshot> Resolve(string capabilityName)
            => Result<DslRomSnapshot>.Success(_snapshot);
    }

    private sealed class TestVfsCredentials : IVfsCredentials
    {
        public string? Username => "test";
        public string? ApiKey => null;
        public string? Token => "test-token";
        public IReadOnlyDictionary<string, object>? Parameters => null;
    }
}
