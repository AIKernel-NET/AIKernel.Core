using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AIKernel.Abstractions.Hosting;
using AIKernel.Abstractions.Providers;
using AIKernel.Dtos.Providers;
using AIKernel.Kernel.Memory;
using AIKernel.Kernel.Providers;
using AIKernel.Providers.Distributed;
using AIKernel.Providers.ProviderPlacement;

var now = new DateTimeOffset(2026, 11, 24, 0, 0, 0, TimeSpan.Zero);
var scheduler = new ProviderPlacementScheduler(new FixedClock(now));
var provider = new FakeProvider().GetCapabilityDescriptor();
var request = new ProviderRequirements
{
    ProviderId = provider.ProviderId,
    ModelId = "example",
    RequiredModalities = [ProviderModality.Text],
    RequiredMode = ExecutionMode.Batch,
    RequiredFeatures = ["embedding.test"],
    RequiredIsaExtensions = ["avx2"],
    Precision = "fp32",
    WorkingSetBytes = 9
};
var coldGpu = Node("b", gpu: true, jobs: 0);
var hotCpu = Node("a", gpu: false, jobs: 5);
var coldGpuTie = Node("c", gpu: true, jobs: 0);
var nodes = new[] { coldGpuTie, hotCpu, coldGpu };
var registrations = new Dictionary<string, NodeProviderRegistrationSnapshot>
{
    ["c"] = Registration(provider),
    ["a"] = Registration(provider, resident: true),
    ["b"] = Registration(provider)
};
var ranked = await scheduler.ResolvePlacementAsync(request, nodes, registrations);
Check(ranked.Selected is { NodeId: "a", Target: ExecutionTarget.Cpu }, "Residency must precede CUDA and load.");
Check(ranked.Candidates.Select(c => (c.NodeId, c.Target)).SequenceEqual(new[]
{
    ("a", ExecutionTarget.Cpu), ("b", ExecutionTarget.Cuda), ("c", ExecutionTarget.Cuda),
    ("b", ExecutionTarget.Cpu), ("c", ExecutionTarget.Cpu)
}), "Deterministic CUDA and node-ID ordering.");
var reversed = await scheduler.ResolvePlacementAsync(request, nodes.Reverse().ToArray(), registrations);
Check(ranked.Candidates.SequenceEqual(reversed.Candidates), "Reordering inputs must not affect candidate order.");
var loaded = await scheduler.ResolvePlacementAsync(request,
    [coldGpu with { Load = new NodeLoadInfo { ActiveJobs = 0, QueuedJobs = 1 } }, coldGpuTie], registrations);
Check(loaded.Selected?.NodeId == "c", "Queue load must precede node ID.");
using (var memory = new VirtualMemorySpace(4, 4096, 2, Path.GetTempPath()))
{
    var source = new LocalNodeCapabilityDescriptorSource("local", memory,
        () => new NodeMemoryInfo { RamTotalBytes = 32768, RamAvailableBytes = 16384 },
        () => new NodeMemoryTierInfo { Tier = "Swap", AvailableBytes = 8192 },
        () => new NodeLoadInfo { ActiveJobs = 0, QueuedJobs = 0 }, timeProvider: new FixedClock(now));
    var local = await source.GetNodeCapabilityDescriptorAsync();
    Check(local.ObservedAtUtc == now && local.Memory.RamAvailableBytes == 8192 && local.Gpus.Count == 0,
        "Public 0.1.4 virtual memory enforces its RAM budget on the descriptor.");
}

await Excluded(coldGpu, null, PlacementReasonCodes.ProviderNotRegistered);
await Excluded(coldGpu, Registration(provider with { ModelIds = ["other"] }), PlacementReasonCodes.ProviderNotRegistered);
await Excluded(coldGpu, Registration(provider) with { ObservedAtUtc = now.AddSeconds(-31) }, PlacementReasonCodes.DescriptorStale);
await Excluded(coldGpu with { ObservedAtUtc = now.AddSeconds(-31) }, Registration(provider), PlacementReasonCodes.DescriptorStale);
await Excluded(coldGpu with { ObservedAtUtc = now.AddSeconds(1) }, Registration(provider), PlacementReasonCodes.DescriptorStale);
await Excluded(coldGpu with { SchemaVersion = 2 }, Registration(provider), PlacementReasonCodes.DescriptorSchemaUnknown);
await Excluded(coldGpu, Registration(provider with { SchemaVersion = 2 }), PlacementReasonCodes.DescriptorSchemaUnknown);
await Excluded(coldGpu, Registration(provider with { ExecutionTargets = [ExecutionTarget.Cuda] }), PlacementReasonCodes.DescriptorInvalid);
await Excluded(coldGpu, Registration(provider with { Extension = new FakeEmbeddingExtension(ProviderKind.Reranker) }), PlacementReasonCodes.DescriptorInvalid);
await Excluded(coldGpu, Registration(provider with { Metadata = new Dictionary<string, string> { ["access_token"] = "secret-value" } }),
    PlacementReasonCodes.DescriptorInvalid);
await Excluded(coldGpu, Registration(provider with { InputModalities = [ProviderModality.Audio] }), PlacementReasonCodes.ModalityUnsupported);
await Excluded(coldGpu, Registration(provider with { Modes = ExecutionMode.Single }), PlacementReasonCodes.ModeUnsupported);
await Excluded(coldGpu, Registration(provider with { Extension = null }), PlacementReasonCodes.FeatureUnsupported);
await Excluded(coldGpu, Registration(provider with { Precisions = [] }), PlacementReasonCodes.PrecisionUnsupported);
await Excluded(coldGpu with { Cpu = coldGpu.Cpu with { IsaExtensions = [] } }, Registration(provider), PlacementReasonCodes.IsaUnsupported);
await Excluded(coldGpu with { MemoryTiers = [] }, Registration(provider), PlacementReasonCodes.MemoryInsufficient);
var atExpiry = await scheduler.ResolvePlacementAsync(request, [coldGpu with { ObservedAtUtc = now.AddSeconds(-30) }],
    new Dictionary<string, NodeProviderRegistrationSnapshot> { ["b"] = Registration(provider) with { ObservedAtUtc = now.AddSeconds(-30) } });
Check(atExpiry.Selected is { Target: ExecutionTarget.Cuda }, "At 30 seconds, RAM plus swap fits despite small VRAM.");
var mixed = await scheduler.ResolvePlacementAsync(request, [hotCpu, coldGpu with { SchemaVersion = 2 }], registrations);
Check(mixed.Selected?.NodeId == "a" && mixed.Exclusions.Count == 1, "Unknown schema must not lose other observations.");
var empty = await scheduler.ResolvePlacementAsync(request, [], registrations);
Check(empty.Exclusions.Single().ReasonCode == PlacementReasonCodes.NoNodes, "An empty node list must have a reason.");
await Failure(() => scheduler.ResolvePlacementAsync(request with { ModelId = "" }, nodes, registrations).AsTask(),
    ProviderErrorCodes.InvalidRequest);
using var canceled = new CancellationTokenSource();
canceled.Cancel();
await Cancellation(() => scheduler.ResolvePlacementAsync(request, nodes, registrations, canceled.Token).AsTask());

var distance = new ScoreSemantics { Kind = ScoreKind.Distance, HigherIsBetter = false };
var scores = ProviderScoreOrdering.Create(distance, [Score(2, 5), Score(0, 5), Score(1, 2)]);
Check(scores.Items.Select(item => item.Index).SequenceEqual([1, 0, 2]) &&
      scores.Items[0].Score.Value == 2, "Preserve scores and sort ties by input index.");
await Failure(() => Task.FromResult(ProviderScoreOrdering.Create(distance, [Score(0, double.NaN)])), ProviderErrorCodes.InvalidOutput);
await Failure(() => Task.FromResult(ProviderScoreOrdering.Create(distance, [Score(0, 1), Score(0, 2)])), ProviderErrorCodes.InvalidOutput);
await Failure(() => Task.FromResult(ProviderScoreOrdering.Create(distance with { Calibrated = true }, [])),
    ProviderErrorCodes.InvalidOutput);

var central = new TestCentralApi(request);
var worker = new HttpProviderWorkPullClient(central, central.Id);
var work = await worker.ClaimAsync("b") ?? throw new Exception("Missing work.");
Check(await worker.ClaimAsync("b") is null, "An empty pull returns null.");
using var staged = new MemoryStream();
await worker.CopyInputToAsync(work, staged);
Check(staged.ToArray().SequenceEqual(TestCentralApi.Payload), "Input must be verified before consumption.");
central.CorruptResponse = true;
await Failure(() => worker.CopyInputToAsync(work, Stream.Null).AsTask(), ProviderErrorCodes.InvalidOutput);
await Cancellation(() => worker.CopyInputToAsync(work, Stream.Null, canceled.Token).AsTask());
using var result = new MemoryStream(TestCentralApi.Payload);
await worker.CompleteAsync(work, result, work.Input);
Check(result.CanRead && central.CompletionVerified, "Result is staged and checked by the receiver; caller retains the stream.");
await worker.FailAsync(work, ProviderErrorCodes.UnsupportedFormat);
await Failure(() => worker.FailAsync(work, "private input").AsTask(), ProviderErrorCodes.InvalidRequest);
await Failure(() => ProviderPayloadVerifier.CopyAsync(new MemoryStream([1]), Stream.Null,
    work.Input with { SizeBytes = 0 }).AsTask(), ProviderErrorCodes.InvalidOutput);
Console.WriteLine("00 passed: capability double, deterministic placement, exclusions, schema, scores, central pull, hashes and cancellation.");
Check(typeof(IEmbeddingProvider).GetMethod("GetCapabilityDescriptor") is null &&
      typeof(IConnectionClient).IsAssignableFrom(typeof(ConnectionProfiles)),
    "Existing 0.1.4 provider and distributed connection contracts must load unchanged.");
Console.WriteLine("Compatibility: public 0.1.4 virtual memory, IEmbeddingProvider and distributed ConnectionProfiles loaded.");

async Task Excluded(NodeCapabilityDescriptor node, NodeProviderRegistrationSnapshot? snapshot, string expected)
{
    var view = new Dictionary<string, NodeProviderRegistrationSnapshot>();
    if (snapshot is not null) view.Add(node.NodeId, snapshot);
    var result = await scheduler.ResolvePlacementAsync(request, [node], view);
    Check(result.Selected is null && result.Exclusions.Single().ReasonCode == expected, expected);
    await Failure(() => Task.FromResult(ProviderPlacementScheduler.RequireCandidate(result)), ProviderErrorCodes.NoCandidateNode);
}

NodeProviderRegistrationSnapshot Registration(ProviderCapabilityDescriptor descriptor, bool resident = false) => new()
{
    ObservedAtUtc = now,
    Providers = [new RegisteredProvider { Descriptor = descriptor, ResidentModelIds = resident ? ["example"] : [] }]
};

NodeCapabilityDescriptor Node(string id, bool gpu, int jobs) => new()
{
    SchemaVersion = ProviderPlacementAbi.SchemaVersion,
    NodeId = id,
    Cpu = new NodeCpuInfo { Architecture = "x64", LogicalCores = 4, IsaExtensions = ["AVX2"] },
    Memory = new NodeMemoryInfo { RamTotalBytes = 7, RamAvailableBytes = 7 },
    Gpus = gpu ? [new NodeGpuInfo
    {
        DeviceId = "pci-0", Name = "test", VramTotalBytes = 1, VramAvailableBytes = 0, CudaComputeCapability = "8.9"
    }] : [],
    MemoryTiers = [new NodeMemoryTierInfo { Tier = "Swap", CapacityBytes = 2, AvailableBytes = 2 }],
    Load = new NodeLoadInfo { ActiveJobs = jobs, QueuedJobs = 0 },
    ObservedAtUtc = now
};

static ScoredItem Score(int index, double score) => new() { Index = index, Score = new ProviderScore(score) };
static void Check(bool success, string message)
{
    if (!success) throw new InvalidOperationException(message);
}
static async Task Failure(Func<Task> run, string code)
{
    try { await run(); }
    catch (ProviderOperationException error) when (error.Error.Code == code)
    {
        Check(!error.ToString().Contains("secret-value") && !error.ToString().Contains("private input"), "Diagnostics leaked data.");
        return;
    }
    throw new InvalidOperationException("Expected " + code);
}
static async Task Cancellation(Func<Task> run)
{
    try { await run(); }
    catch (OperationCanceledException) { return; }
    throw new InvalidOperationException("Expected cancellation.");
}

sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
sealed record FakeEmbeddingExtension(ProviderKind DeclaredKind = ProviderKind.Embedding) : ProviderCapabilityExtension
{
    public override ProviderKind Kind => DeclaredKind;
    public override IReadOnlyList<string> Features => ["embedding.test"];
}
sealed class FakeProvider : IProviderCapabilityDescriptorSource
{
    public ProviderCapabilityDescriptor GetCapabilityDescriptor() => new()
    {
        SchemaVersion = ProviderPlacementAbi.SchemaVersion,
        ProviderId = "embedding.test",
        Kind = ProviderKind.Embedding,
        ModelIds = ["example"],
        InputModalities = [ProviderModality.Text],
        ExecutionTargets = [ExecutionTarget.Cpu, ExecutionTarget.Cuda],
        Modes = ExecutionMode.Single | ExecutionMode.Batch,
        Precisions = ["FP32"],
        Memory = new ProviderMemoryRequirement { Basis = "estimated", WorkingSetBytes = 9 },
        Extension = new FakeEmbeddingExtension()
    };
}
sealed class TestCentralApi(ProviderRequirements requirements) : IConnectionClient
{
    private readonly JsonSerializerOptions json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
    private bool claimed;
    public static byte[] Payload { get; } = Encoding.UTF8.GetBytes("test only");
    public Guid Id { get; } = Guid.NewGuid();
    public bool CorruptResponse { get; set; }
    public bool CompletionVerified { get; private set; }

    public async Task<HttpResponseMessage> SendAsync(Guid serverId, HttpMethod method, string relativePath,
        HttpContent? body = null, string? range = null, CancellationToken cancellationToken = default)
    {
        if (serverId != Id || method != HttpMethod.Post || !relativePath.StartsWith("/api/v1/provider-work/", StringComparison.Ordinal))
            throw new InvalidOperationException("Work went outside the central API.");
        var reference = new ProviderPayloadReference(Convert.ToHexStringLower(SHA256.HashData(Payload)), Payload.Length);
        switch (relativePath.Split('/')[^1])
        {
            case "claim":
                if (claimed) return new(HttpStatusCode.NoContent);
                claimed = true;
                return new(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(new ProviderWorkItem("job", "claim", requirements, reference), json))
                };
            case "input":
                var bytes = (byte[])Payload.Clone();
                if (CorruptResponse) bytes[0] ^= 1;
                return new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
            case "complete":
                var multipart = (MultipartFormDataContent)body!;
                var parts = multipart.ToArray();
                using (var output = new MemoryStream())
                {
                    await ProviderPayloadVerifier.CopyAsync(await parts[1].ReadAsStreamAsync(cancellationToken), output, reference, cancellationToken);
                    CompletionVerified = output.ToArray().SequenceEqual(Payload);
                }
                return new(HttpStatusCode.NoContent);
            case "fail":
                return new(HttpStatusCode.NoContent);
            default:
                throw new InvalidOperationException("Unexpected work route.");
        }
    }

    public Task<HttpResponseMessage> SendRequestAsync(Guid serverId, HttpRequestMessage request,
        CancellationToken cancellationToken = default) => throw new InvalidOperationException("Expected a central API request.");
}
