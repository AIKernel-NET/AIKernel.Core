namespace AIKernel.Core.Tests.ChatHistory;

using AIKernel.Common.Results;
using AIKernel.Core.ChatHistory;
using AIKernel.Core.Rom;
using AIKernel.Core.Tests.Support;
using AIKernel.Core.Time;
using AIKernel.Core.Vfs.Memory;
using AIKernel.Vfs;
using Xunit;

public sealed class ChatHistoryRomExporterTests
{
    [Fact]
    public async Task ToRomMarkdown_ProducesSignedRomLoadableByRomLoader()
    {
        var generated = ChatHistoryRomExporter.ToRomMarkdown(
            Records,
            Options);

        Assert.True(generated.IsSuccess, generated.Error?.Message);
        Assert.Contains("rom_id: 'chat://share/demo'", generated.Value, StringComparison.Ordinal);
        Assert.Contains("source_kind: 'chat_history'", generated.Value, StringComparison.Ordinal);
        Assert.Contains("signature:", generated.Value, StringComparison.Ordinal);
        Assert.Contains("sha256:", generated.Value, StringComparison.Ordinal);

        await using var session = await OpenSessionAsync();
        await session.WriteFileAsync(
            "rom/chat-history/demo.md",
            System.Text.Encoding.UTF8.GetBytes(generated.Value!));

        var loader = new RomLoader(
            new MarkdownFrontMatterParser(),
            new RomSignatureVerifier(
                new DefaultRomCanonicalizer(),
                new Sha256SemanticHasher()),
            KernelClock.Replay(DateTimeOffset.UnixEpoch));

        var snapshot = await loader.LoadAsync(
            session,
            "rom/chat-history/demo.md",
            TestContext.Current.CancellationToken);

        Assert.Equal("chat://share/demo", snapshot.RomId.Value);
        Assert.Contains("@role: user", snapshot.Body, StringComparison.Ordinal);
        Assert.Contains("@role: assistant", snapshot.Body, StringComparison.Ordinal);
        Assert.Equal("chat_history", snapshot.AdditionalMetadata["source_kind"]);
        Assert.Equal("conversation", snapshot.AdditionalMetadata["entity_type"]);
        Assert.Contains("chat", snapshot.SecurityTags);
        Assert.True(snapshot.Signature!.IsVerified);
    }

    [Fact]
    public void ToRomMarkdown_IsDeterministic_ForSameRecordsAndOptions()
    {
        var first = ChatHistoryRomExporter.ToRomMarkdown(Records, Options);
        var second = ChatHistoryRomExporter.ToRomMarkdown(Records, Options);

        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.True(second.IsSuccess, second.Error?.Message);
        Assert.Equal(first.Value, second.Value);
    }

    [Fact]
    public void ToRomMarkdown_ReturnsFailClosed_WhenRecordsAreMissing()
    {
        var result = ChatHistoryRomExporter.ToRomMarkdown(
            [],
            Options);

        Assert.True(result.IsFailure);
        Assert.Equal("CHAT_HISTORY_ROM_ERROR", result.Error!.Code);
        Assert.Equal(FailureKind.FailClosed, result.Error.FailureKind);
        Assert.Equal(OriginStep.KernelFacade, result.Error.OriginStep);
        Assert.Equal(SemanticSlot.C, result.Error.SemanticSlot);
    }

    [Fact]
    public void ToRomMarkdown_ChangesSignature_WhenContentChanges()
    {
        var first = ChatHistoryRomExporter.ToRomMarkdown(Records, Options);
        var changed = ChatHistoryRomExporter.ToRomMarkdown(
        [
            Records[0],
            Records[1] with
            {
                Content = "Changed answer."
            }
        ],
            Options);

        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.True(changed.IsSuccess, changed.Error?.Message);
        Assert.NotEqual(first.Value, changed.Value);
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

    private static readonly ChatHistoryRomOptions Options = new(
        "chat://share/demo",
        DateTimeOffset.Parse("2026-06-04T00:00:00Z"),
        SecurityTags: ["chat", "history"]);

    private static readonly ChatHistoryRomRecord[] Records =
    [
        new(
            "user",
            "Can we turn this chat into ROM?",
            DateTimeOffset.Parse("2026-06-04T00:00:01Z")),
        new(
            "assistant",
            "Yes. The export must be deterministic.",
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
