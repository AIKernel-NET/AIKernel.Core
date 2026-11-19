using AIKernel.Kernel.Memory;

namespace AIKernel.Kernel.Inference;

/// <summary>Opt-in CPU paging. Decimal byte budgets; RSS checks are cooperative, not an OS hard limit.</summary>
public sealed record CosmosMemoryOptions
{
    /// <summary>Cooperative process working-set ceiling, in decimal bytes.</summary>
    public long MaximumResidentBytes { get; init; } = 7_000_000_000;
    /// <summary>Maximum cache for unchanged encoded weight pages; grows lazily.</summary>
    public long WeightCacheBytes { get; init; } = 5_000_000_000;
    /// <summary>Maximum resident request-private KV cache; remaining blocks use encrypted swap.</summary>
    public long KvCacheBytes { get; init; } = 128_000_000;
    /// <summary>Headroom for CLR, tokenizer, caller input and transient native buffers.</summary>
    public long RuntimeReserveBytes { get; init; } = 1_000_000_000;
    /// <summary>Local private SSD directory for encrypted request data.</summary>
    public required string SwapDirectory { get; init; }
    /// <summary>Capacity reserved for externally owned resident components such as SeedSLM; never reclaimed by this allocator.</summary>
    public long ProtectedResidentBytes { get; init; }
    /// <summary>Optional trusted manifest digest. Without it, shard hashes establish consistency, not publisher authenticity.</summary>
    public string? ExpectedImportManifestSha256 { get; init; }
    internal void Validate()
    {
        if(MaximumResidentBytes<256L<<20 || RuntimeReserveBytes<64L<<20 ||
            WeightCacheBytes<CosmosPagedWeights.PageBytes || KvCacheBytes<1L<<20 ||
            ProtectedResidentBytes<0 || ProtectedResidentBytes>MaximumResidentBytes ||
            RuntimeReserveBytes>MaximumResidentBytes-ProtectedResidentBytes-32L*1024*1024)
            throw new ArgumentOutOfRangeException(nameof(CosmosMemoryOptions));
        ArgumentException.ThrowIfNullOrWhiteSpace(SwapDirectory);
        Directory.CreateDirectory(SwapDirectory);
    }
}

/// <summary>Observed process RSS and bounded cache allocations. Peaks are sampled, not OS-enforced.</summary>
public sealed record CosmosMemoryStatistics(long MaximumResidentBytes,long ObservedResidentBytes,long PeakObservedResidentBytes,
    long WeightCacheCapacityBytes,long WeightResidentBytes,long KvCacheCapacityBytes,long KvResidentBytes,
    long LiveWorkspaceBytes,long PeakWorkspaceBytes,long BudgetAdjustments,long WeightBackingReads,long KvSwapReads);
