namespace AIKernel.Core.Time;

/// <summary>
/// AIKernel が扱う論理時刻を表す DTO です。
///
/// v0.1.0 では UTC 時刻のラップに留めます。
/// v0.2.0 以降で HLC、論理カウンタ、署名付き時刻、Replay sequence などを追加するため、
/// DateTimeOffset を直接ばらまかず、Kernel 専用の時刻表現として分離しています。
/// </summary>
public sealed record KernelTimestamp
{
    public required DateTimeOffset UtcDateTime { get; init; }

    public long? LogicalCounter { get; init; }

    public string? SourceId { get; init; }

    public string? Signature { get; init; }

    public static KernelTimestamp FromUtc(
        DateTimeOffset utcDateTime,
        string? sourceId = null)
    {
        return new KernelTimestamp
        {
            UtcDateTime = utcDateTime.ToUniversalTime(),
            SourceId = sourceId
        };
    }
}
