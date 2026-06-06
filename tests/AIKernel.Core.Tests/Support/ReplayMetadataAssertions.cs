namespace AIKernel.Core.Tests.Support;

using System.Text.RegularExpressions;
using System.Collections.Generic;
using AIKernel.Common.Results;

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
        AssertStepId(metadata[ReplayMetadataKeys.StepId]);
        Assert.Equal(semanticDelta, metadata[ReplayMetadataKeys.SemanticDelta]);
        Assert.Equal(replayLogCount, metadata[ReplayMetadataKeys.ReplayLogCount]);
        AssertReplayLogHash(metadata[ReplayMetadataKeys.ReplayLogHash]);
    }

    [GeneratedRegex("^step:sha256:[0-9a-f]{64}$")]
    private static partial Regex StepIdPattern();

    [GeneratedRegex("^replay:sha256:[0-9a-f]{64}$")]
    private static partial Regex ReplayLogHashPattern();
}
