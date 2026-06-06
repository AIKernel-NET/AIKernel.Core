namespace AIKernel.Core.Tests.Governance;

using AIKernel.Abstractions.Governance.ChatChain;
using AIKernel.Core.Governance.ChatChain;
using Xunit;

public sealed class ChatChainTests
{
    [Fact]
    public void Canonicalize_NormalizesBodyAndTimestamp()
    {
        var canonicalizer = new DefaultChatTurnCanonicalizer();

        var canonical = canonicalizer.Canonicalize(new ChatTurn(
            " user ",
            " hello\r\nworld ",
            new DateTime(2026, 6, 5, 12, 0, 0, DateTimeKind.Utc)));

        Assert.Equal(
            "{\"actor\":\"user\",\"body\":\"hello\\nworld\",\"timestamp_utc\":\"2026-06-05T12:00:00.0000000Z\"}",
            canonical);
    }

    [Fact]
    public async Task SignatureProvider_UsesAlgorithmTaggedDeterministicSignature()
    {
        var provider = new AlgorithmTaggedChatTurnSignatureProvider();

        var signature = await provider.SignAsync(
            "sha256-chat-turn-v1:abc",
            TestContext.Current.CancellationToken);

        Assert.StartsWith(
            AlgorithmTaggedChatTurnSignatureProvider.Algorithm + ":",
            signature,
            StringComparison.Ordinal);
        Assert.True(await provider.VerifyAsync(
            "sha256-chat-turn-v1:abc",
            signature,
            TestContext.Current.CancellationToken));
        Assert.False(await provider.VerifyAsync(
            "sha256-chat-turn-v1:other",
            signature,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task VerifyChain_Succeeds_ForDeterministicHashChain()
    {
        var canonicalizer = new DefaultChatTurnCanonicalizer();
        var hasher = new Sha256ChatTurnSemanticHasher();
        var signer = new AlgorithmTaggedChatTurnSignatureProvider();
        var verifier = new ChatTurnChainVerifier(canonicalizer, hasher, signer);

        var first = await CreateNodeAsync(
            canonicalizer,
            hasher,
            signer,
            new ChatTurn("user", "hello", new DateTime(2026, 6, 5, 1, 0, 0, DateTimeKind.Utc)),
            string.Empty);
        var second = await CreateNodeAsync(
            canonicalizer,
            hasher,
            signer,
            new ChatTurn("assistant", "world", new DateTime(2026, 6, 5, 1, 0, 1, DateTimeKind.Utc)),
            first.Hash);

        var result = verifier.VerifyChain([first, second]);

        Assert.True(result.IsSuccess);
        Assert.Equal(string.Empty, result.Error);
    }

    [Fact]
    public async Task VerifyNextTurn_FailsClosed_WhenPreviousHashDoesNotMatch()
    {
        var canonicalizer = new DefaultChatTurnCanonicalizer();
        var hasher = new Sha256ChatTurnSemanticHasher();
        var signer = new AlgorithmTaggedChatTurnSignatureProvider();
        var verifier = new ChatTurnChainVerifier(canonicalizer, hasher, signer);
        var node = await CreateNodeAsync(
            canonicalizer,
            hasher,
            signer,
            new ChatTurn("user", "hello", new DateTime(2026, 6, 5, 1, 0, 0, DateTimeKind.Utc)),
            "tail-a");

        var result = verifier.VerifyNextTurn(node, "tail-b");

        Assert.False(result.IsSuccess);
        Assert.Equal("HASH_CHAIN_PREVIOUS_HASH_MISMATCH", result.Error);
    }

    [Fact]
    public async Task VerifyNextTurn_FailsClosed_WhenSignatureIsNotAlgorithmTagged()
    {
        var canonicalizer = new DefaultChatTurnCanonicalizer();
        var hasher = new Sha256ChatTurnSemanticHasher();
        var signer = new AlgorithmTaggedChatTurnSignatureProvider();
        var verifier = new ChatTurnChainVerifier(canonicalizer, hasher, signer);
        var node = await CreateNodeAsync(
            canonicalizer,
            hasher,
            signer,
            new ChatTurn("user", "hello", new DateTime(2026, 6, 5, 1, 0, 0, DateTimeKind.Utc)),
            string.Empty);

        var result = verifier.VerifyNextTurn(
            node with { Signature = "not-tagged" },
            string.Empty);

        Assert.False(result.IsSuccess);
        Assert.Equal("HASH_CHAIN_SIGNATURE_MISSING_ALGORITHM_TAG", result.Error);
    }

    [Fact]
    public async Task VerifyNextTurn_FailsClosed_WhenSignatureValueDoesNotMatch()
    {
        var canonicalizer = new DefaultChatTurnCanonicalizer();
        var hasher = new Sha256ChatTurnSemanticHasher();
        var signer = new AlgorithmTaggedChatTurnSignatureProvider();
        var verifier = new ChatTurnChainVerifier(canonicalizer, hasher, signer);
        var node = await CreateNodeAsync(
            canonicalizer,
            hasher,
            signer,
            new ChatTurn("user", "hello", new DateTime(2026, 6, 5, 1, 0, 0, DateTimeKind.Utc)),
            string.Empty);

        var result = verifier.VerifyNextTurn(
            node with
            {
                Signature = AlgorithmTaggedChatTurnSignatureProvider.Algorithm + ":tampered"
            },
            string.Empty);

        Assert.False(result.IsSuccess);
        Assert.Equal("HASH_CHAIN_SIGNATURE_MISMATCH", result.Error);
    }

    private static async Task<HashChainNode> CreateNodeAsync(
        IChatTurnCanonicalizer canonicalizer,
        IChatTurnSemanticHasher hasher,
        IChatTurnSignatureProvider signer,
        IChatTurn turn,
        string previousHash)
    {
        var canonical = canonicalizer.Canonicalize(turn);
        var hash = hasher.ComputeHash(canonical, previousHash);
        var signature = await signer.SignAsync(hash, TestContext.Current.CancellationToken);

        return new HashChainNode(turn, previousHash, hash, signature);
    }
}
