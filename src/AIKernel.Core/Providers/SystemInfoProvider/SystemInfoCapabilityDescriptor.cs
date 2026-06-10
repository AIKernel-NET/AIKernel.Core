namespace AIKernel.Core.Providers.SystemInfoProvider;

/// <summary>
/// [EN] Descriptor for the built-in system information capability.
/// [JA] 組み込み system information capability の descriptor です。
/// </summary>
/// <param name="CapabilityId">[EN] Stable capability identifier. [JA] 安定した capability identifier です。</param>
/// <param name="Name">[EN] Human-readable capability name. [JA] 人が読める capability name です。</param>
/// <param name="Version">[EN] Capability contract version. [JA] capability contract version です。</param>
/// <param name="ProvidedOperations">[EN] Supported operation names. [JA] 対応する operation name です。</param>
/// <param name="Metadata">[EN] Deterministic descriptor metadata. [JA] 決定論的な descriptor metadata です。</param>
public sealed record SystemInfoCapabilityDescriptor(
    string CapabilityId,
    string Name,
    string Version,
    IReadOnlyList<string> ProvidedOperations,
    IReadOnlyDictionary<string, string> Metadata)
{
    /// <summary>
    /// [EN] Creates the standard system information capability descriptor.
    /// [JA] 標準 system information capability descriptor を作成します。
    /// </summary>
    public static SystemInfoCapabilityDescriptor Standard()
        => new(
            "aikernel.system.info",
            "System Information",
            "1.0.0",
            ["system.capabilities", "system.info", "system.providers", "system.runtime", "system.vfs"],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["kind"] = "Utility",
                ["invocationMode"] = "Inline",
                ["tags"] = "system,info,introspection,core",
                ["provider"] = SystemInfoProvider.ProviderIdValue
            });
}
