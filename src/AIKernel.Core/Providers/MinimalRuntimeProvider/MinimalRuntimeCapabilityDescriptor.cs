namespace AIKernel.Core.Providers.MinimalRuntimeProvider;

/// <summary>
/// [EN] Descriptor for the built-in minimal runtime ping capability.
/// [JA] 組み込み minimal runtime ping capability の descriptor です。
/// </summary>
/// <param name="CapabilityId">[EN] Stable capability identifier. [JA] 安定した capability identifier です。</param>
/// <param name="Name">[EN] Human-readable capability name. [JA] 人が読める capability name です。</param>
/// <param name="Version">[EN] Capability contract version. [JA] capability contract version です。</param>
/// <param name="ProvidedOperations">[EN] Supported operation names. [JA] 対応する operation name です。</param>
/// <param name="Metadata">[EN] Deterministic descriptor metadata. [JA] 決定論的な descriptor metadata です。</param>
public sealed record MinimalRuntimeCapabilityDescriptor(
    string CapabilityId,
    string Name,
    string Version,
    IReadOnlyList<string> ProvidedOperations,
    IReadOnlyDictionary<string, string> Metadata)
{
    /// <summary>
    /// [EN] Creates the standard minimal runtime capability descriptor.
    /// [JA] 標準 minimal runtime capability descriptor を作成します。
    /// </summary>
    public static MinimalRuntimeCapabilityDescriptor Standard()
        => new(
            "aikernel.runtime.ping",
            "Minimal Runtime Ping",
            "1.0.0",
            ["runtime.ping"],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["kind"] = "Utility",
                ["invocationMode"] = "Inline",
                ["tags"] = "runtime,minimal,ping",
                ["provider"] = MinimalRuntimeProvider.ProviderIdValue
            });
}
