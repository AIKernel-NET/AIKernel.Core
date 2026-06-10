namespace AIKernel.Core.Providers.SystemInfoProvider;

using AIKernel.Dtos.Capabilities;
using AIKernel.Enums;

/// <summary>
/// [EN] Converts system information descriptors into capability module contracts.
/// [JA] system information descriptor を capability module contract に変換します。
/// </summary>
public static class SystemInfoCapabilityContracts
{
    /// <summary>
    /// [EN] Converts the descriptor into the shared capability module descriptor.
    /// [JA] descriptor を共有 capability module descriptor に変換します。
    /// </summary>
    public static CapabilityModuleDescriptor ToContract(
        SystemInfoCapabilityDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        var metadata = new Dictionary<string, string>(
            descriptor.Metadata,
            StringComparer.Ordinal)
        {
            ["kind"] = "Utility",
            ["invocationMode"] = "Inline",
            ["tags"] = "system,info,introspection,core"
        };

        return new CapabilityModuleDescriptor(
            descriptor.CapabilityId,
            descriptor.Name,
            CapabilityModuleKind.Unknown,
            CapabilityInvocationMode.Direct,
            descriptor.Version,
            "AIKernel.Core.Providers.SystemInfoProvider",
            null,
            null,
            descriptor.ProvidedOperations,
            ["system.read"],
            metadata
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal));
    }
}
