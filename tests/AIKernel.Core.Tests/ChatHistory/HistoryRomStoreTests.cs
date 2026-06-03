namespace AIKernel.Core.Tests.ChatHistory;

using AIKernel.Core.ChatHistory;
using AIKernel.Core.Rom;
using AIKernel.Core.Tests.Support;
using AIKernel.Core.Time;
using AIKernel.Core.Vfs.Memory;
using AIKernel.Vfs;
using Xunit;

public sealed class HistoryRomStoreTests
{
    [Fact]
    public async Task SaveHistoryAsRomAsync_WritesSignedRomAndRegistersSnapshot()
    {
        var registry = new HistoryRomRegistry();
        var store = CreateStore(registry);
        await using var session = await OpenSessionAsync();

        var saved = await store.SaveHistoryAsRomAsync(
            session,
            "agent",
            "demo",
            Records,
            GeneratedAt,
            securityTags: ["chat", "history"]);

        Assert.True(saved.IsSuccess, saved.Error?.Message);
        Assert.Equal("agent", saved.Value!.Namespace);
        Assert.Equal("demo", saved.Value.Name);
        Assert.Equal("rom/history/agent/demo.md", saved.Value.Path);
        Assert.Equal("history://agent/demo", saved.Value.RomId);
        Assert.StartsWith("sha256:", saved.Value.RomHash, StringComparison.Ordinal);
        Assert.True(registry.Contains("history://agent/demo"));

        var resolved = registry.Resolve("history://agent/demo");

        Assert.True(resolved.IsSuccess, resolved.Error?.Message);
        Assert.Equal(saved.Value.RomHash, resolved.Value!.Metadata.RomHash);
        Assert.Equal("chat_history", resolved.Value.Rom.AdditionalMetadata["source_kind"]);
        Assert.Contains("@role: user", resolved.Value.Rom.Body, StringComparison.Ordinal);
        Assert.True(resolved.Value.Rom.Signature!.IsVerified);
    }

    [Fact]
    public async Task LoadHistoryRomAsync_RejectsTamperedContentWhenExpectedHashDiffers()
    {
        var seedRegistry = new HistoryRomRegistry();
        var seedStore = CreateStore(seedRegistry);
        await using var session = await OpenSessionAsync();

        var saved = await seedStore.SaveHistoryAsRomAsync(
            session,
            "agent",
            "demo",
            Records,
            GeneratedAt);

        Assert.True(saved.IsSuccess, saved.Error?.Message);

        var changedMarkdown = ChatHistoryRomExporter.ToRomMarkdown(
            [
                Records[0],
                Records[1] with
                {
                    Content = "Changed deterministic history."
                }
            ],
            new ChatHistoryRomOptions(
                "history://agent/changed",
                GeneratedAt));

        Assert.True(changedMarkdown.IsSuccess, changedMarkdown.Error?.Message);

        var changedSaved = await seedStore.SaveMarkdownAsRomAsync(
            session,
            "agent",
            "changed",
            changedMarkdown.Value!,
            GeneratedAt);

        Assert.True(changedSaved.IsSuccess, changedSaved.Error?.Message);

        var loadStore = CreateStore(new HistoryRomRegistry());
        var loaded = await loadStore.LoadHistoryRomAsync(
            session,
            "agent",
            "changed",
            GeneratedAt,
            expectedRomHash: saved.Value!.RomHash);

        Assert.True(loaded.IsFailure);
        Assert.Equal("HISTORY_ROM_ERROR", loaded.Error!.Code);
    }

    [Fact]
    public async Task SaveHistoryAsRomAsync_RejectsExistingPathWithDifferentContent()
    {
        var store = CreateStore(new HistoryRomRegistry());
        await using var session = await OpenSessionAsync();

        var saved = await store.SaveHistoryAsRomAsync(
            session,
            "agent",
            "demo",
            Records,
            GeneratedAt);

        Assert.True(saved.IsSuccess, saved.Error?.Message);

        var changed = await store.SaveHistoryAsRomAsync(
            session,
            "agent",
            "demo",
            [
                Records[0],
                Records[1] with
                {
                    Content = "Changed answer."
                }
            ],
            GeneratedAt);

        Assert.True(changed.IsFailure);
        Assert.Equal("HISTORY_ROM_ERROR", changed.Error!.Code);
    }

    [Fact]
    public async Task SaveMarkdownAsRomAsync_RemovesNewFileWhenSignatureVerificationFails()
    {
        var store = CreateStore(new HistoryRomRegistry());
        await using var session = await OpenSessionAsync();

        var saved = await store.SaveMarkdownAsRomAsync(
            session,
            "agent",
            "invalid",
            "# not signed history",
            GeneratedAt);

        Assert.True(saved.IsFailure);
        Assert.False(await session.ExistsAsync("rom/history/agent/invalid.md"));
    }

    [Fact]
    public void HistoryRomPath_RejectsNestedRomId()
    {
        var parsed = HistoryRomPath.ParseRomId("history://agent/nested/demo");

        Assert.True(parsed.IsFailure);
        Assert.Equal("HISTORY_ROM_ERROR", parsed.Error!.Code);
    }

    [Fact]
    public void HistoryRomRegistry_ReturnsRejectWhenRomIsMissing()
    {
        var registry = new HistoryRomRegistry();

        var resolved = registry.Resolve("history://agent/missing");

        Assert.True(resolved.IsFailure);
        Assert.Equal("HISTORY_ROM_NOT_FOUND", resolved.Error!.Code);
    }

    [Fact]
    public void HistoryRomMetadataKeys_ExposeStableContractNames()
    {
        Assert.Equal("history_rom_hash", HistoryRomMetadataKeys.RomHash);
        Assert.Equal("history_rom_id", HistoryRomMetadataKeys.RomId);
        Assert.Equal("history_rom_path", HistoryRomMetadataKeys.RomPath);
        Assert.Equal("history_rom_namespace", HistoryRomMetadataKeys.RomNamespace);
        Assert.Equal("history_rom_name", HistoryRomMetadataKeys.RomName);
    }

    [Fact]
    public void HistoryRomRegistry_ContainsReturnsFalseForMalformedRomId()
    {
        var registry = new HistoryRomRegistry();

        Assert.False(registry.Contains("history://agent/nested/demo"));
    }

    private static HistoryRomStore CreateStore(IHistoryRomRegistry registry)
        => new(
            new HistoryRomProvider(),
            registry,
            new RomLoader(
                new MarkdownFrontMatterParser(),
                new RomSignatureVerifier(
                    new DefaultRomCanonicalizer(),
                    new Sha256SemanticHasher()),
                KernelClock.Replay(DateTimeOffset.UnixEpoch)));

    private static async Task<IVfsSession> OpenSessionAsync()
    {
        var provider = new MemoryFileProvider(
            new MemoryFileProviderOptions
            {
                Clock = KernelClock.Replay(DateTimeOffset.UnixEpoch)
            });

        return await provider.OpenSessionAsync(new TestVfsCredentials());
    }

    private static readonly DateTimeOffset GeneratedAt =
        DateTimeOffset.Parse("2026-06-04T00:00:00Z");

    private static readonly ChatHistoryRomRecord[] Records =
    [
        new(
            "user",
            "Can this chat be registered as HistoryROM?",
            DateTimeOffset.Parse("2026-06-04T00:00:01Z")),
        new(
            "assistant",
            "Yes. Store it as a signed immutable ROM.",
            DateTimeOffset.Parse("2026-06-04T00:00:02Z"))
    ];

    private sealed class TestVfsCredentials : IVfsCredentials
    {
        public string? Username => "test";
        public string? ApiKey => null;
        public string? Token => "test-token";
        public IReadOnlyDictionary<string, object>? Parameters => null;
    }
}
