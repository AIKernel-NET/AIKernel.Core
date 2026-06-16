namespace AIKernel.Core.Dsl;

internal sealed record DslRomMetadata(
    string Namespace,
    string Name,
    string Path,
    string CapabilityName,
    string RomHash,
    DateTimeOffset CreatedAtUtc);

internal static class DslRomMetadataKeys
{
    /// <summary>
    /// EN: Gets the RomHash constant.
    /// EN: Documentation for public API. JA: RomHash 定数を取得します。
    /// </summary>
    public const string RomHash = "dsl_rom_hash";
    /// <summary>
    /// EN: Gets the RomCall constant.
    /// EN: Documentation for public API. JA: RomCall 定数を取得します。
    /// </summary>

    public const string RomCall = "dsl_rom_call";
    /// <summary>
    /// EN: Gets the RomPath constant.
    /// EN: Documentation for public API. JA: RomPath 定数を取得します。
    /// </summary>

    public const string RomPath = "dsl_rom_path";
    /// <summary>
    /// EN: Gets the RomNamespace constant.
    /// EN: Documentation for public API. JA: RomNamespace 定数を取得します。
    /// </summary>

    public const string RomNamespace = "dsl_rom_namespace";
    /// <summary>
    /// EN: Gets the RomName constant.
    /// EN: Documentation for public API. JA: RomName 定数を取得します。
    /// </summary>

    public const string RomName = "dsl_rom_name";
    /// <summary>
    /// EN: Gets the RomReplayLogCount constant.
    /// EN: Documentation for public API. JA: RomReplayLogCount 定数を取得します。
    /// </summary>

    public const string RomReplayLogCount = "dsl_rom_replay_log_count";
    /// <summary>
    /// EN: Gets the RomReplayLogHash constant.
    /// EN: Documentation for public API. JA: RomReplayLogHash 定数を取得します。
    /// </summary>

    public const string RomReplayLogHash = "dsl_rom_replay_log_hash";
}
