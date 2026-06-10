namespace AIKernel.Core.Providers.LocalExecutionProvider;

using AIKernel.Dtos.Capabilities;
using AIKernel.Enums;

/// <summary>
/// [EN] Converts local execution descriptors into capability module contracts.
/// [JA] local execution descriptor を capability module contract に変換します。
/// </summary>
public static class LocalExecutionCapabilityContracts
{
    /// <summary>
    /// [EN] Converts the descriptor into the shared capability module descriptor.
    /// [JA] descriptor を共有 capability module descriptor に変換します。
    /// </summary>
    public static CapabilityModuleDescriptor ToContract(
        LocalExecutionCapabilityDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        var metadata = new Dictionary<string, string>(
            descriptor.Metadata,
            StringComparer.Ordinal)
        {
            ["kind"] = "Execution",
            ["invocationMode"] = "Inline",
            ["tags"] = "local,execution,dsl"
        };

        return new CapabilityModuleDescriptor(
            descriptor.CapabilityId,
            descriptor.Name,
            CapabilityModuleKind.DslRom,
            CapabilityInvocationMode.DslPipeline,
            descriptor.Version,
            "AIKernel.Core.Providers.LocalExecutionProvider",
            null,
            null,
            descriptor.ProvidedOperations,
            ["dsl.execute", "capability.invoke"],
            metadata
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal));
    }
}
