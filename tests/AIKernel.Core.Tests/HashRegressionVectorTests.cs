namespace AIKernel.Core.Tests;

using System.Collections.Immutable;
using AIKernel.Common.Results;
using AIKernel.Core.Dsl;
using AIKernel.Core.Governance.ChatChain;
using Xunit;

// V1 records the released algorithms. See docs/operations/HASH_REGRESSION_VECTORS-v0.1.3.md.
public sealed class HashRegressionVectorTests
{
    private const string ParentId = "step:sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string ChatParent = "sha256-chat-turn-v1:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string DslLf = "{\n  \"steps\": [\n    {\"capability\": \"echo\"}\n  ]\n}\n";
    private const string DslUnicode = "{\n  \"name\": \"計画😀\",\n  \"text\": \"café / cafe\u0301\"\n}\n";
    private const string ChatContent = "{\"actor\":\"user\",\"body\":\"こんにちは😀\\ncafé\",\"timestamp_utc\":\"2026-06-05T12:00:00.0000000Z\"}";

    [Theory]
    [InlineData("root-success",
        "step:sha256:f57f5a6ffaed0fabf9dc74772b3bfd09498bb08db295685c682ec11dedb003dc",
        "step:sha256:ed5136019d652482f683a63aa747b2573fb141d49f315e5ed734a65c39efcfb9")]
    [InlineData("root-failure",
        "step:sha256:8243a60d32612ed44705b1b0fefbb8227be398af5c7bb25f7d924c372e2bd6c3",
        "step:sha256:e06ed9249e26dfef9306221f3789acee869714a1aca9927085c72b9e529660be")]
    [InlineData("success",
        "step:sha256:9b88eddd8be7a9133b3ff8019f2cdee421d13013ce4083a8860bf2090a20365d",
        "step:sha256:551a82b80c061b4da472d19065a5caef6de87726d4dab02917d07264d8511b25")]
    [InlineData("failure",
        "step:sha256:49d588d3c09ccec2b1892ad8ff6b9ce1ec8b8e192128450e25bb74bf0cbc5f74",
        "step:sha256:a8b3e44e37f297872036fe55af485764196ffb2852e6b1530c2293f10216b89f")]
    public void ResultStepIdentity_Create_MatchesFixedVector(
        string vector, string linuxLf, string windowsCrLf)
    {
        // Exercise the internal ResultStepIdentity.Create through its public caller.
        var actual = CreateStep(vector).StepId;

        Assert.Equal(CurrentPlatformVector(linuxLf, windowsCrLf), actual);
    }

    [Theory]
    [InlineData("root-success", 0,
        "replay:sha256:d75a14ec383806e95b660071886b82e41e2563ba24710401850c9bdb7e347a51",
        "replay:sha256:449c3b88455e96b92b51899400e41d45a778c4c0f17f6d34247cab3e26108c91")]
    [InlineData("success", 1,
        "replay:sha256:8344054b1d057e838905611cb69b36c6973c7937fc79073cf0201a1da30cbb9c",
        "replay:sha256:89b6b59e6245a45626748918dfa26f45aaeab82a89b7f781b976f05b70f1c2a3")]
    [InlineData("failure", 1,
        "replay:sha256:3354ccd22cb190ce7975de03873df789bcf850cc85a917f36a2241807c70b5d6",
        "replay:sha256:8dffdedfab9d5c066a8604f7a96cf682edc0a63a7dd24d1416f6ac209b26b6c3")]
    [InlineData("chain", 2,
        "replay:sha256:3db37fd1e01bb73dd82f77820857e60844a19e0e6b51b25484fb175ed2fa1526",
        "replay:sha256:8e0f549339493812f4e151062da2fc8e1e33e170ed8781800476bdea814cfbac")]
    public void ResultStepIdentity_CreateReplayLogHash_MatchesFixedVector(
        string vector, int entryCount, string linuxLf, string windowsCrLf)
    {
        // The two-entry vector includes a success followed by a failure and its parent ID.
        var result = CreateStep(vector);

        Assert.Equal(entryCount, result.ReplayLog.Count);
        Assert.Equal(CurrentPlatformVector(linuxLf, windowsCrLf), result.ReplayLogHash);
    }

    [Theory]
    [InlineData("", "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855")]
    [InlineData(DslLf, "f3718dcd0a3c23e41976679aa601477b570d7c9e49ed85ab531d65f730275efb")]
    [InlineData(DslUnicode, "d2718c5251e3a88d587a5f0e262e1b5874226d0d7a41cbb794221ae2e5338eab")]
    [InlineData("{\r\n  \"steps\": []\r\n}\r\n", "f73b83a168e2f603cedfdb562abae7139a380838c81d3c7b7640c0c80e983c58")]
    public void DslRomHasher_ComputeHash_MatchesFixedVector(string text, string expected)
    {
        var actual = DslRomHasher.ComputeHash(text);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("", "", "sha256-chat-turn-v1:01ba4719c80b6fe911b091a7c05124b64eeece964e09c058ef8f9805daca546b")]
    [InlineData(ChatContent, "", "sha256-chat-turn-v1:6a2e92de7edfd032ae641209badc26451544e5f82979527d5a10e2f2b3c9c327")]
    [InlineData(ChatContent, ChatParent, "sha256-chat-turn-v1:2798a96444de876851d93fd15f63295dd31ae04f4aff2643e9ca8b6eecb9aae3")]
    [InlineData("first\nsecond=cafe\u0301\n", ChatParent, "sha256-chat-turn-v1:4273dc7f99115dcfc06537e7c079cacbc60e0b75cfe2c4acac1a0fe0188b10f5")]
    public void Sha256ChatTurnSemanticHasher_ComputeHash_MatchesFixedVector(
        string content, string previousHash, string expected)
    {
        var actual = new Sha256ChatTurnSemanticHasher().ComputeHash(content, previousHash);

        Assert.Equal(expected, actual);
    }

    private static string CurrentPlatformVector(string linuxLf, string windowsCrLf)
        // H1a will make Linux's fixed value mandatory on every OS. Until then, preserve
        // the released Windows behavior without changing the Linux baseline or skipping tests.
        => OperatingSystem.IsWindows() ? windowsCrLf : linuxLf;

    private static ResultStep<string, int> CreateStep(string vector)
    {
        var success = ResultStep<string, int>.Success("fixture", 7);
        var failure = ResultStep<string, int>.Fail(
            "fixture", new ErrorContext("denied", "CANON_DENIED", false));
        var delta = new SemanticDelta(
            "計画=確定\n次😀",
            OriginStep.Provider,
            SemanticSlot.T,
            ImmutableDictionary.CreateRange(StringComparer.Ordinal, new[]
            {
                KeyValuePair.Create("z", "last"),
                KeyValuePair.Create("a", "cafe\u0301\nvalue=1"),
                KeyValuePair.Create("A", "café"),
                KeyValuePair.Create("空", "")
            }),
            Kind: "review.vector");

        return vector switch
        {
            "root-success" => success,
            "root-failure" => failure,
            "success" => success.WithSemanticDelta(delta, ParentId),
            "failure" => failure.WithSemanticDelta(delta, ParentId),
            "chain" => success.WithSemanticDelta(delta, ParentId).Bind(
                _ => failure.WithSemanticDelta(new SemanticDelta("gate.reject", Kind: "gate"))),
            _ => throw new ArgumentOutOfRangeException(nameof(vector), vector, "Unknown fixed vector.")
        };
    }
}
