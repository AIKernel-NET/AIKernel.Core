using AIKernel.Kernel.Inference;
using System.Text.Json.Nodes;

namespace AIKernel.Core.Tests.Inference;

public sealed class ModelRegistrationTests
{
    private const string Profile = """
        {"version":"Q1_0-1","readerId":"gguf","tokenizerVersion":"qwen2-bpe-1",
         "modelId":"example","shape":{"width":64,"hidden":128,"heads":2,"kvHeads":1,"headSize":32,"layers":4,"epsilon":0.000001}}
        """;

    private static JsonObject Catalog()
    {
        var root = JsonNode.Parse(Profile)!.AsObject();
        root["weightsSha256"] = new string('a', 64);
        root["tokenizerSha256"] = new string('b', 64);
        return new JsonObject
        {
            ["profile"] = "leaf",
            ["profiles"] = new JsonObject
            {
                ["leaf"] = JsonNode.Parse("""{"extends":"middle","shape":{"layers":5}}"""),
                ["middle"] = JsonNode.Parse("""{"extends":"base","shape":{"heads":4}}"""),
                ["base"] = root
            }
        };
    }

    [Fact]
    public void ResolvedDimensionsAreIndependentOfDeclarationOrder()
    {
        var catalog = Catalog();
        var profile = DenseModelRegistration.Parse(catalog.ToJsonString(), "example");
        var table = catalog["profiles"]!.AsObject();
        var reordered = new JsonObject(table.Reverse().Select(p => KeyValuePair.Create(p.Key, p.Value?.DeepClone())));
        catalog["profiles"] = reordered;
        var second = DenseModelRegistration.Parse(catalog.ToJsonString(), "example");
        var expected = new Qwen3ModelDefinition(64, 128, 4, 1, 32, 5, .000001f);
        Assert.Equal(expected, profile.BindShape(expected));
        Assert.Equal(expected, second.BindShape(expected));
        Assert.Throws<InvalidDataException>(() => profile.BindShape(expected with { LayerCount = 4 }));
    }

    [Theory]
    [InlineData("middle", "extends", "leaf")]
    [InlineData("leaf", "extends", "missing")]
    [InlineData("base", "unrecognized", "hidden")]
    public void InvalidAncestorsFailClosed(string profile, string key, string value)
    {
        var catalog = Catalog();
        catalog["profiles"]![profile]![key] = value;
        Assert.Throws<InvalidDataException>(() => DenseModelRegistration.Parse(catalog.ToJsonString(), "example"));
    }

    [Fact]
    public void InvalidOverriddenShapeStillFails()
    {
        var catalog = Catalog();
        catalog["profiles"]!["base"]!["shape"]!["layers"] = -1;
        Assert.Throws<InvalidDataException>(() => DenseModelRegistration.Parse(catalog.ToJsonString(), "example"));
    }

    [Fact]
    public void EqualScoresSelectLowerResourceIdsAndRecordActualDispatches()
    {
        var plan = new RoutingPlan(5, 3);
        var order = new int[5];
        for (int i = 0; i < 2; i++)
        {
            Assert.Equal(3f, plan.Select([0, 2, 2, 2, 2], order));
            Assert.Equal(new[] { 1, 2, 3 }, order[..3]);
        }
        Assert.Equal(new long[] { 0, 2, 2, 2, 0 }, plan.Snapshot());
    }

    [Fact]
    public void AliasedWeightsShareOneReservationAndOneBackingRead()
    {
        var dir = Directory.CreateTempSubdirectory();
        string path = Path.Combine(dir.FullName, "weights");
        try
        {
            File.WriteAllBytes(path, new byte[4096]);
            var view = new ModelTensor(0, 32, 32, 128, "F32", 1, 4);
            using (var pages = new TensorPageStore(path, [view, view],
                new(7_000_000_000, 8192, 8192, 64L << 20, dir.FullName), 4096))
            {
                Assert.Equal(4096L, pages.RamUsage.ReservedBytes);
                Assert.Equal(new ResourceResidency(1, 0), pages.Observe(view.Backing));
                pages.VisitRow(view, 0, _ => { });
                pages.VisitRow(view.Row(7), 0, _ => { });
                Assert.Equal(1L, pages.DistinctPagesRead);
                Assert.Equal(4096L, pages.BackingBytesRead);
                Assert.Equal(new ResourceResidency(1, 1), pages.Observe(view.Backing));
            }
            using var exclusive = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            Assert.Single(dir.EnumerateFiles());
        }
        finally { dir.Delete(true); }
    }

    public static bool HasBonsai => File.Exists(Environment.GetEnvironmentVariable("AIKERNEL_TEST_BONSAI_GGUF"));

    [Fact(SkipUnless = nameof(HasBonsai), Skip = "Requires the real Bonsai GGUF.")]
    public async Task ConcurrentRequestsReturnThePagedWorkspaceWithClearedKeysAndValues()
    {
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            using (var model = new DenseModelSession(Environment.GetEnvironmentVariable("AIKERNEL_TEST_BONSAI_GGUF")!,
                2, 128, "auto", new() { SwapDirectory = directory.FullName, WeightCacheBytes = 64L << 20 }))
            {
                var stop = TestContext.Current.CancellationToken;
                var first = model.GenerateBenchmarkAsync("Reply briefly.", "Hello", 1, stop);
                var second = model.GenerateBenchmarkAsync("Reply briefly.", "Hello", 1, stop);
                var replies = await Task.WhenAll(first, second);
                Assert.Equal(replies[0].GeneratedTokenSha256, replies[1].GeneratedTokenSha256);
                var field = typeof(DenseModelSession).GetField("workspace",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var state = Assert.IsType<Qwen3Workspace>(field!.GetValue(model));
                Assert.False(state.InUse);
                Assert.Equal(0, state.UsedTokens);
                foreach (var buffer in state.Keys.Concat(state.Values))
                    Assert.False(buffer.AsSpan().ContainsAnyExcept(0f));
            }
            Assert.Empty(directory.EnumerateFileSystemInfos());
        }
        finally { directory.Delete(true); }
    }
}
