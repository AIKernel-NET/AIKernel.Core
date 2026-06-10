namespace AIKernel.Core.Storage;

using AIKernel.Common.Results;
using AIKernel.Dtos.Capabilities;
using AIKernel.Enums;

/// <summary>
/// [EN] Core-owned mapper for ROM storage capability contracts.
/// [JA] ROM storage capability contract の Core 所有 mapper です。
/// </summary>
public static class RomStorageCapabilityContracts
{
    /// <summary>
    /// [EN] Converts a ROM storage descriptor into the shared capability module contract.
    /// [JA] ROM storage descriptor を共有 capability module contract へ変換します。
    /// </summary>
    public static CapabilityModuleDescriptor ToContract(
        RomStorageCapabilityDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        return new CapabilityModuleDescriptor(
            descriptor.CapabilityId,
            "ROM Storage",
            CapabilityModuleKind.ManagedAssembly,
            CapabilityInvocationMode.AssemblyReference,
            GetMetadataValue(descriptor.Metadata, "version", "0.1.0"),
            "AIKernel.Core.Storage",
            null,
            null,
            [
                "rom.save",
                "rom.load",
                "rom.list"
            ],
            ["rom.read", "rom.write"],
            descriptor.Metadata);
    }

    private static string GetMetadataValue(
        IReadOnlyDictionary<string, string> metadata,
        string key,
        string fallback)
        => ReadMetadata(metadata, key).OrElse(fallback);

    private static Option<string> ReadMetadata(
        IReadOnlyDictionary<string, string> metadata,
        string key)
    {
        if (metadata.TryGetValue(key, out var value) &&
            !string.IsNullOrWhiteSpace(value))
        {
            return Option<string>.Some(value);
        }

        return Option<string>.None();
    }
}
