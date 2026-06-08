namespace AIKernel.Kernel;

using System.Collections.Immutable;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Kernel.KernelProviderRoutingDecisionFactory']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Kernel.KernelProviderRoutingDecisionFactory']" />
public static class KernelProviderRoutingDecisionFactory
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Kernel.KernelProviderRoutingDecisionFactory.ForProvider']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Kernel.KernelProviderRoutingDecisionFactory.ForProvider']" />
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

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Kernel.KernelProviderRoutingDecisionFactory.ForCapabilityModule']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Kernel.KernelProviderRoutingDecisionFactory.ForCapabilityModule']" />
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
