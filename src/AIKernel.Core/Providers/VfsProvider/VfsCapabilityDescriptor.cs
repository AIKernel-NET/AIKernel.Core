namespace AIKernel.Core.Providers.VfsProvider;

/// <summary>
/// [EN] Descriptor for the built-in read-only VFS capability.
/// [JA] 組み込み read-only VFS capability の descriptor です。
/// </summary>
/// <param name="CapabilityId">[EN] Stable capability identifier. [JA] 安定した capability identifier です。</param>
/// <param name="Name">[EN] Human-readable capability name. [JA] 人が読める capability name です。</param>
/// <param name="Version">[EN] Capability contract version. [JA] capability contract version です。</param>
/// <param name="ProvidedOperations">[EN] Supported operation names. [JA] 対応する operation name です。</param>
/// <param name="Metadata">[EN] Deterministic descriptor metadata. [JA] 決定論的な descriptor metadata です。</param>
public sealed record VfsCapabilityDescriptor(
    string CapabilityId,
    string Name,
    string Version,
    IReadOnlyList<string> ProvidedOperations,
    IReadOnlyDictionary<string, string> Metadata)
{
    /// <summary>
    /// [EN] Creates the standard VFS read capability descriptor.
    /// [JA] 標準 VFS read capability descriptor を作成します。
    /// </summary>
    public static VfsCapabilityDescriptor Standard()
        => new(
            "aikernel.vfs",
            "Virtual File System Read",
            "1.0.0",
            ["vfs.exists", "vfs.list", "vfs.metadata", "vfs.read_file"],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["kind"] = "Storage",
                ["invocationMode"] = "Inline",
                ["tags"] = "vfs,filesystem,storage,core",
                ["provider"] = VfsProvider.ProviderIdValue
            });
}
