namespace AIKernel.Kernel.Inference;

/// <summary>Resident-memory policy for immutable encoded weights, in bytes.</summary>
internal sealed record TensorPageBudget(long MaximumResidentBytes, long WeightCacheBytes,
    long InitialWeightCacheBytes, long RuntimeReserveBytes, string SwapDirectory);
