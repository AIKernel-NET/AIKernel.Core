namespace AIKernel.Kernel;

/// <summary>
/// [EN] Represents a Core-side provider routing decision for the Kernel facade.
/// [JA] AIKernel の公開参照サーフェスにおける KernelProviderRoutingDecision を説明します。
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
/// [EN] Carries optional routing score metadata for a provider decision.
/// [JA] AIKernel の公開参照サーフェスにおける KernelProviderRoutingScore を説明します。
/// </summary>
public sealed record KernelProviderRoutingScore(
    double Value,
    string? ProfileId = null);
