namespace AIKernel.Kernel;

using System.Collections.Immutable;

public sealed record KernelProviderRoutingDecision
{
    public KernelProviderRoutingDecision(
        string providerId,
        string requestedModelId,
        string? providerTier = null,
        string? capabilityModuleId = null,
        string? routeReason = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        ProviderId = RequireNonEmpty(providerId, nameof(providerId));
        RequestedModelId = RequireNonEmpty(requestedModelId, nameof(requestedModelId));
        ProviderTier = NormalizeOptional(providerTier);
        CapabilityModuleId = NormalizeOptional(capabilityModuleId);
        RouteReason = NormalizeOptional(routeReason);
        Metadata = metadata ?? ImmutableDictionary<string, string>.Empty;
    }

    public string ProviderId { get; }

    public string RequestedModelId { get; }

    public string? ProviderTier { get; }

    public string? CapabilityModuleId { get; }

    public string? RouteReason { get; }

    public IReadOnlyDictionary<string, string> Metadata { get; }

    public static KernelProviderRoutingDecision ForProvider(
        string providerId,
        string requestedModelId,
        string? providerTier = null,
        string? routeReason = null,
        IReadOnlyDictionary<string, string>? metadata = null)
        => new(
            providerId,
            requestedModelId,
            providerTier,
            capabilityModuleId: null,
            routeReason,
            metadata);

    public static KernelProviderRoutingDecision ForCapabilityModule(
        string providerId,
        string requestedModelId,
        string capabilityModuleId,
        string? routeReason = null,
        IReadOnlyDictionary<string, string>? metadata = null)
        => new(
            providerId,
            requestedModelId,
            providerTier: "capability",
            capabilityModuleId,
            routeReason,
            metadata);

    public ImmutableDictionary<string, string> ToMetadata()
        => ApplyTo(ImmutableDictionary<string, string>.Empty);

    public ImmutableDictionary<string, string> ApplyTo(
        IReadOnlyDictionary<string, string>? metadata)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);

        foreach (var item in metadata ?? ImmutableDictionary<string, string>.Empty)
        {
            builder[item.Key] = item.Value;
        }

        foreach (var item in Metadata)
        {
            builder[item.Key] = item.Value;
        }

        builder[KernelFacadeMetadataKeys.ProviderId] = ProviderId;
        builder[KernelFacadeMetadataKeys.RequestedModelId] = RequestedModelId;
        AddOptional(builder, KernelFacadeMetadataKeys.ProviderTier, ProviderTier);
        AddOptional(builder, KernelFacadeMetadataKeys.CapabilityModuleId, CapabilityModuleId);
        AddOptional(builder, KernelFacadeMetadataKeys.RouteReason, RouteReason);

        return builder.ToImmutable();
    }

    private static void AddOptional(
        ImmutableDictionary<string, string>.Builder builder,
        string key,
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            builder[key] = value;
        }
    }

    private static string RequireNonEmpty(
        string value,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                $"{parameterName} is required.",
                parameterName);
        }

        return value;
    }

    private static string? NormalizeOptional(
        string? value)
        => string.IsNullOrWhiteSpace(value)
            ? null
            : value;
}
