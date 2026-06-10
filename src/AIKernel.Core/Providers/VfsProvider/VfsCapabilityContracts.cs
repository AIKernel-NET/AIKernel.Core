namespace AIKernel.Core.Providers.VfsProvider;

using AIKernel.Dtos.Capabilities;
using AIKernel.Enums;

/// <summary>
/// [EN] Converts VFS descriptors into capability module contracts.
/// [JA] VFS descriptor を capability module contract に変換します。
/// </summary>
public static class VfsCapabilityContracts
{
    /// <summary>
    /// [EN] Converts the descriptor into the shared capability module descriptor.
    /// [JA] descriptor を共有 capability module descriptor に変換します。
    /// </summary>
    public static CapabilityModuleDescriptor ToContract(
        VfsCapabilityDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        var metadata = new Dictionary<string, string>(
            descriptor.Metadata,
            StringComparer.Ordinal)
        {
            ["kind"] = "Storage",
            ["invocationMode"] = "Inline",
            ["tags"] = "vfs,filesystem,storage,core"
        };

        return new CapabilityModuleDescriptor(
            descriptor.CapabilityId,
            descriptor.Name,
            CapabilityModuleKind.Unknown,
            CapabilityInvocationMode.Direct,
            descriptor.Version,
            "AIKernel.Core.Providers.VfsProvider",
            null,
            null,
            descriptor.ProvidedOperations,
            ["vfs.read"],
            metadata
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal));
    }
}
