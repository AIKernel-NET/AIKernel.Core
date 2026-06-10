namespace AIKernel.Core.Providers.SkillProvider;

using AIKernel.Common.Results;
using AIKernel.Dtos.Capabilities;
using AIKernel.Enums;

/// <summary>
/// [EN] Converts Skill.MD descriptors into AIKernel capability module contracts.
/// [JA] Skill.MD descriptor を AIKernel capability module contract に変換します。
/// </summary>
public static class SkillCapabilityContracts
{
    /// <summary>
    /// [EN] Converts a Skill capability descriptor into the shared capability module descriptor.
    /// [JA] Skill capability descriptor を共有 capability module descriptor に変換します。
    /// </summary>
    public static CapabilityModuleDescriptor ToContract(
        SkillCapabilityDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        var metadata = new Dictionary<string, string>(
            descriptor.Metadata,
            StringComparer.Ordinal)
        {
            ["provider"] = SkillProvider.ProviderIdValue,
            ["skill.source_path"] = descriptor.SourcePath
        };

        return new CapabilityModuleDescriptor(
            descriptor.CapabilityId,
            descriptor.Name,
            CapabilityModuleKind.ManagedAssembly,
            CapabilityInvocationMode.AssemblyReference,
            descriptor.Version,
            "AIKernel.Core.Providers.SkillProvider",
            descriptor.SourcePath,
            GetMetadataValue(metadata, "skill.content_hash"),
            descriptor.ProvidedOperations,
            descriptor.RequiredPermissions,
            metadata
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal));
    }

    private static string? GetMetadataValue(
        IReadOnlyDictionary<string, string> metadata,
        string key)
        => ReadMetadata(metadata, key)
            .Match<string?>(
                () => null,
                value => value);

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
