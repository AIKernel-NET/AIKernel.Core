namespace AIKernel.Kernel.Inference;

/// <summary>CPU weight paging through AIKernel virtual memory. Budgets are decimal bytes.</summary>
public record GgufMemoryOptions
{
    /// <summary>Process RSS ceiling checked at admission and page boundaries. Not an OS cgroup limit.</summary>
    public long MaximumResidentBytes{get;init;}=7_000_000_000;
    /// <summary>Bounded RAM cache for encoded weights; remaining pages use the original GGUF on disk.</summary>
    public long WeightCacheBytes{get;init;}=5_000_000_000;
    /// <summary>Requested buffer capacity to allocate before paging starts. Zero allocates on demand.
    /// The actual reservation is limited to whole pages that fit the model, cache, and process headroom.</summary>
    public long InitialWeightCacheBytes { get; init; }
    /// <summary>Headroom for tokenizer, shared KV, activations, runtime and transient buffers.</summary>
    public long RuntimeReserveBytes{get;init;}=1_000_000_000;
    /// <summary>Private directory for virtual-memory storage. Immutable weights need no duplicate swap copy.</summary>
    public required string SwapDirectory{get;init;}
}

/// <summary>Resident-memory limits for Gemma 4 CPU inference.</summary>
public sealed record Gemma4MemoryOptions : GgufMemoryOptions;
