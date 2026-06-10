namespace AIKernel.Core.Providers.MinimalRuntimeProvider;

using AIKernel.Dtos.Capabilities;
using AIKernel.Enums;

/// <summary>
/// [EN] Converts minimal runtime descriptors into capability module contracts.
/// [JA] minimal runtime descriptor を capability module contract に変換します。
/// </summary>
public static class MinimalRuntimeCapabilityContracts
{
    /// <summary>
    /// [EN] Converts the descriptor into the shared capability module descriptor.
    /// [JA] descriptor を共有 capability module descriptor に変換します。
    /// </summary>
    public static CapabilityModuleDescriptor ToContract(
        MinimalRuntimeCapabilityDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        var metadata = new Dictionary<string, string>(
            descriptor.Metadata,
            StringComparer.Ordinal)
        {
            ["kind"] = "Utility",
            ["invocationMode"] = "Inline",
            ["tags"] = "runtime,minimal,ping"
        };

        return new CapabilityModuleDescriptor(
            descriptor.CapabilityId,
            descriptor.Name,
            CapabilityModuleKind.Unknown,
            CapabilityInvocationMode.Direct,
            descriptor.Version,
            "AIKernel.Core.Providers.MinimalRuntimeProvider",
            null,
            null,
            descriptor.ProvidedOperations,
            ["runtime.ping"],
            metadata
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal));
    }
}
