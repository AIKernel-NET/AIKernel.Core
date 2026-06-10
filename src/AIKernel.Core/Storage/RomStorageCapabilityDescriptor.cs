namespace AIKernel.Core.Storage;

/// <summary>
/// [EN] Core-owned descriptor for the ROM storage capability boundary.
/// [JA] ROM storage capability 境界の Core 所有 descriptor です。
/// </summary>
/// <param name="CapabilityId">[EN] Stable ROM storage capability identifier. [JA] 安定した ROM storage capability identifier です。</param>
/// <param name="StorageScheme">[EN] Storage scheme exposed by the capability. [JA] capability が公開する storage scheme です。</param>
/// <param name="Metadata">[EN] Deterministic descriptor metadata. [JA] 決定論的な descriptor metadata です。</param>
public sealed record RomStorageCapabilityDescriptor(
    string CapabilityId,
    string StorageScheme,
    IReadOnlyDictionary<string, string> Metadata);
