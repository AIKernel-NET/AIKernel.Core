namespace AIKernel.Kernel;

using System.Collections.Immutable;
using AIKernel.Dtos.Kernel;
using AIKernel.Dtos.Routing;

public static class KernelProviderRoutingDecisionExtensions
{
    public static ImmutableDictionary<string, string> ToMetadata(
        this KernelProviderRoutingDecision decision)
        => decision.ApplyTo(ImmutableDictionary<string, string>.Empty);

    public static KernelRequest ApplyToRequest(
        this KernelProviderRoutingDecision decision,
        KernelRequest request)
    {
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(request);

        return request with
        {
            RequestedModelId = decision.RequestedModelId,
            Metadata = decision.ApplyTo(request.Metadata)
        };
    }

    public static ImmutableDictionary<string, string> ApplyTo(
        this KernelProviderRoutingDecision decision,
        IReadOnlyDictionary<string, string>? metadata)
    {
        ArgumentNullException.ThrowIfNull(decision);

        var builder = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);

        foreach (var item in metadata ?? ImmutableDictionary<string, string>.Empty)
        {
            builder[item.Key] = item.Value;
        }

        foreach (var item in decision.Metadata ?? ImmutableDictionary<string, string>.Empty)
        {
            builder[item.Key] = item.Value;
        }

        builder[KernelFacadeMetadataKeys.ProviderId] = decision.ProviderId;
        builder[KernelFacadeMetadataKeys.RequestedModelId] = decision.RequestedModelId;
        AddOptional(builder, KernelFacadeMetadataKeys.ProviderTier, decision.ProviderTier);
        AddOptional(builder, KernelFacadeMetadataKeys.CapabilityModuleId, decision.CapabilityModuleId);
        AddOptional(builder, KernelFacadeMetadataKeys.RouteReason, decision.RouteReason);
        AddOptional(builder, "routing_score", decision.Score?.Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
        AddOptional(builder, "routing_score_profile_id", decision.Score?.ProfileId);

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
}
