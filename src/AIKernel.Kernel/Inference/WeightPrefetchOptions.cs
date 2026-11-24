namespace AIKernel.Kernel.Inference;

/// <summary>Host settings for speculative reads of immutable CPU weight pages.</summary>
public sealed record WeightPrefetchOptions
{
    /// <summary>Opt in separately for each registered model. The default uses demand paging.</summary>
    public bool Enabled { get; init; }
    /// <summary>Dense look-ahead layers, or predicted experts per MoE layer. Range 1–4.</summary>
    public int Depth { get; init { ArgumentOutOfRangeException.ThrowIfLessThan(value, 1); ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 4); field = value; } } = 1;
    /// <summary>Additional staging allowance in bytes, within the process ceiling. Range 1–64 MiB.</summary>
    public long MaximumBytes { get; init { ArgumentOutOfRangeException.ThrowIfLessThan(value, 1L << 20); ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 64L << 20); field = value; } } = 8L << 20;
}

/// <summary>Cumulative page counts. DemandReads counts reads from immutable backing issued by the demand path.</summary>
public readonly record struct WeightPrefetchStatistics(long Issued, long Hits, long Misses, long Cancelled,
    long Failures, long WaitedForInFlight, long DemandReads);
