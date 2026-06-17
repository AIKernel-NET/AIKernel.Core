namespace AIKernel.Kernel;

using System.Collections.Immutable;

/// <summary>[EN] Documents this public package API member. [JA] KernelProviderRoutingDecisionFactory を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Kernel.KernelProviderRoutingDecisionFactory']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Kernel.KernelProviderRoutingDecisionFactory']/summary" />
public static class KernelProviderRoutingDecisionFactory
{
    /// <summary>[EN] Documents this public package API member. [JA] ForProvider を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Kernel.KernelProviderRoutingDecisionFactory.ForProvider']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Kernel.KernelProviderRoutingDecisionFactory.ForProvider']/summary" />
    public static KernelProviderRoutingDecision ForProvider(
        string providerId,
        string requestedModelId,
        string? providerTier = null,
        string? routeReason = null,
        IReadOnlyDictionary<string, string>? metadata = null)
        => new(
            RequireNonEmpty(providerId, nameof(providerId)),
            RequireNonEmpty(requestedModelId, nameof(requestedModelId)),
            NormalizeOptional(providerTier),
            null,
            NormalizeOptional(routeReason),
            null,
            metadata ?? ImmutableDictionary<string, string>.Empty);

    /// <summary>[EN] Documents this public package API member. [JA] ForCapabilityModule を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Kernel.KernelProviderRoutingDecisionFactory.ForCapabilityModule']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Kernel.KernelProviderRoutingDecisionFactory.ForCapabilityModule']/summary" />
    public static KernelProviderRoutingDecision ForCapabilityModule(
        string providerId,
        string requestedModelId,
        string capabilityModuleId,
        string? routeReason = null,
        IReadOnlyDictionary<string, string>? metadata = null)
        => new(
            RequireNonEmpty(providerId, nameof(providerId)),
            RequireNonEmpty(requestedModelId, nameof(requestedModelId)),
            "capability",
            RequireNonEmpty(capabilityModuleId, nameof(capabilityModuleId)),
            NormalizeOptional(routeReason),
            null,
            metadata ?? ImmutableDictionary<string, string>.Empty);

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
