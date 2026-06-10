namespace AIKernel.Core.Providers.LocalExecutionProvider;

/// <summary>
/// [EN] Descriptor for the built-in local DSL pipeline execution capability.
/// [JA] 組み込み local DSL pipeline execution capability の descriptor です。
/// </summary>
/// <param name="CapabilityId">[EN] Stable capability identifier. [JA] 安定した capability identifier です。</param>
/// <param name="Name">[EN] Human-readable capability name. [JA] 人が読める capability name です。</param>
/// <param name="Version">[EN] Capability contract version. [JA] capability contract version です。</param>
/// <param name="ProvidedOperations">[EN] Supported operation names. [JA] 対応する operation name です。</param>
/// <param name="Metadata">[EN] Deterministic descriptor metadata. [JA] 決定論的な descriptor metadata です。</param>
public sealed record LocalExecutionCapabilityDescriptor(
    string CapabilityId,
    string Name,
    string Version,
    IReadOnlyList<string> ProvidedOperations,
    IReadOnlyDictionary<string, string> Metadata)
{
    /// <summary>
    /// [EN] Creates the standard local execution capability descriptor.
    /// [JA] 標準 local execution capability descriptor を作成します。
    /// </summary>
    public static LocalExecutionCapabilityDescriptor Standard()
        => new(
            "aikernel.local.execute",
            "Local Execution",
            "1.0.0",
            ["pipeline.execute"],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["kind"] = "Execution",
                ["invocationMode"] = "Inline",
                ["tags"] = "local,execution,dsl",
                ["provider"] = LocalExecutionProvider.ProviderIdValue
            });
}
