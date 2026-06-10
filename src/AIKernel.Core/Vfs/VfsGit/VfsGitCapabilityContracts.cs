namespace AIKernel.Core.Vfs.VfsGit;

using AIKernel.Common.Results;
using AIKernel.Dtos.Capabilities;
using AIKernel.Enums;

/// <summary>
/// [EN] Core-owned mapper for VFS Git capability contracts.
/// [JA] VFS Git capability contract の Core 所有 mapper です。
/// </summary>
public static class VfsGitCapabilityContracts
{
    /// <summary>
    /// [EN] Converts a VFS Git descriptor into the shared capability module contract.
    /// [JA] VFS Git descriptor を共有 capability module contract へ変換します。
    /// </summary>
    public static CapabilityModuleDescriptor ToContract(
        VfsGitCapabilityDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        return new CapabilityModuleDescriptor(
            descriptor.CapabilityId,
            "VFS Git",
            CapabilityModuleKind.ManagedAssembly,
            CapabilityInvocationMode.AssemblyReference,
            GetMetadataValue(descriptor.Metadata, "version", "0.1.0"),
            "AIKernel.Core.Vfs.VfsGit",
            null,
            null,
            [
                "vfs.git.read",
                "vfs.git.list",
                "vfs.git.checkout"
            ],
            ["vfs.read", "git.read"],
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
