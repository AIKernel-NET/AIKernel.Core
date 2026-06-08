namespace AIKernel.Kernel;

/// <summary>
/// Represents a Core-side provider routing decision for the Kernel facade.
/// </summary>
public sealed record KernelProviderRoutingDecision(
    string ProviderId,
    string RequestedModelId,
    string? ProviderTier,
    string? CapabilityModuleId,
    string? RouteReason,
    KernelProviderRoutingScore? Score,
    IReadOnlyDictionary<string, string>? Metadata);

/// <summary>
/// Carries optional routing score metadata for a provider decision.
/// </summary>
public sealed record KernelProviderRoutingScore(
    double Value,
    string? ProfileId = null);
