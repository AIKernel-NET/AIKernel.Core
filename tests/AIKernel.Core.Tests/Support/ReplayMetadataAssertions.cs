namespace AIKernel.Core.Tests.Support;

using System.Text.RegularExpressions;
using System.Collections.Generic;

internal static partial class ReplayMetadataAssertions
{
    public static void AssertStepId(string value)
    {
        Assert.Matches(StepIdPattern(), value);
    }

    public static void AssertReplayLogHash(string value)
    {
        Assert.Matches(ReplayLogHashPattern(), value);
    }

    public static void AssertReplayMetadata(
        IReadOnlyDictionary<string, string> metadata,
        string semanticDelta,
        string replayLogCount)
    {
        AssertStepId(metadata["step_id"]);
        Assert.Equal(semanticDelta, metadata["semantic_delta"]);
        Assert.Equal(replayLogCount, metadata["replay_log_count"]);
        AssertReplayLogHash(metadata["replay_log_hash"]);
    }

    [GeneratedRegex("^step:sha256:[0-9a-f]{64}$")]
    private static partial Regex StepIdPattern();

    [GeneratedRegex("^replay:sha256:[0-9a-f]{64}$")]
    private static partial Regex ReplayLogHashPattern();
}
