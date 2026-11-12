namespace AIKernel.Common.Results;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;

internal static class ResultStepIdentity
{
    /// <summary>
    /// EN: Gets Create.
    /// [EN] Documents this public package API member. [JA] Create を取得します。
    /// </summary>
    public static string Create(
        string? parentStepId,
        SemanticDelta delta,
        bool isSuccess,
        string? errorCode)
    {
        var payload = BuildCanonicalPayload(
            parentStepId,
            delta,
            isSuccess,
            errorCode);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));

        return "step:sha256:" + Convert.ToHexStringLower(bytes);
    }
    /// <summary>
    /// EN: Gets CreateReplayLogHash.
    /// [EN] Documents this public package API member. [JA] CreateReplayLogHash を取得します。
    /// </summary>

    public static string CreateReplayLogHash(
        IReadOnlyList<ResultStepReplayLogEntry> replayLog)
    {
        var payload = BuildReplayLogCanonicalPayload(replayLog);
        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var encoder = Encoding.UTF8.GetEncoder();
        Span<byte> buffer = stackalloc byte[4096];
        foreach (var chunk in payload.GetChunks())
        {
            var remaining = chunk.Span;
            while (!remaining.IsEmpty)
            {
                encoder.Convert(remaining, buffer, flush: false, out var charsUsed, out var bytesUsed, out _);
                hasher.AppendData(buffer[..bytesUsed]);
                remaining = remaining[charsUsed..];
            }
        }
        // Keep encoder state across StringBuilder chunks, including split surrogate pairs.
        encoder.Convert(ReadOnlySpan<char>.Empty, buffer, flush: true, out _, out var finalBytes, out _);
        hasher.AppendData(buffer[..finalBytes]);
        var bytes = hasher.GetHashAndReset();

        return "replay:sha256:" + Convert.ToHexStringLower(bytes);
    }

    private static string BuildCanonicalPayload(
        string? parentStepId,
        SemanticDelta delta,
        bool isSuccess,
        string? errorCode)
    {
        var builder = new StringBuilder();
        builder.AppendLine("aikernel.result-step.v1");
        builder.AppendLine(parentStepId ?? string.Empty);
        builder.AppendLine(isSuccess ? "success" : "failure");
        builder.AppendLine(errorCode ?? string.Empty);
        builder.AppendLine(delta.Label);
        builder.AppendLine(delta.Kind ?? string.Empty);
        builder.AppendLine(delta.OriginStep?.ToString() ?? string.Empty);
        builder.AppendLine(delta.SemanticSlot?.ToString() ?? string.Empty);

        if (delta.Metadata is not null)
        {
            foreach (var item in delta.Metadata.OrderBy(
                item => item.Key,
                StringComparer.Ordinal))
            {
                builder.Append(item.Key);
                builder.Append('=');
                builder.AppendLine(item.Value);
            }
        }

        builder.AppendLine(
            delta.Metadata?.Count.ToString(CultureInfo.InvariantCulture) ?? "0");

        return builder.ToString();
    }

    private static StringBuilder BuildReplayLogCanonicalPayload(
        IReadOnlyList<ResultStepReplayLogEntry> replayLog)
    {
        var builder = new StringBuilder();
        builder.AppendLine("aikernel.result-step.replay-log.v1");
        builder.AppendLine(replayLog.Count.ToString(CultureInfo.InvariantCulture));

        foreach (var entry in replayLog)
        {
            builder.AppendLine(entry.StepId);
            builder.AppendLine(entry.ParentStepId ?? string.Empty);
            builder.AppendLine(entry.IsSuccess ? "success" : "failure");
            builder.AppendLine(entry.ErrorCode ?? string.Empty);
            builder.AppendLine(entry.SemanticDelta.Label);
            builder.AppendLine(entry.SemanticDelta.Kind ?? string.Empty);
            builder.AppendLine(entry.SemanticDelta.OriginStep?.ToString() ?? string.Empty);
            builder.AppendLine(entry.SemanticDelta.SemanticSlot?.ToString() ?? string.Empty);

            if (entry.SemanticDelta.Metadata is not null)
            {
                foreach (var item in entry.SemanticDelta.Metadata.OrderBy(
                    item => item.Key,
                    StringComparer.Ordinal))
                {
                    builder.Append(item.Key);
                    builder.Append('=');
                    builder.AppendLine(item.Value);
                }
            }

            builder.AppendLine(
                entry.SemanticDelta.Metadata?.Count.ToString(CultureInfo.InvariantCulture) ?? "0");
        }

        return builder;
    }
}
