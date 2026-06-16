namespace AIKernel.Core.ChatHistory;

internal sealed record HistoryRomMetadata(
    string Namespace,
    string Name,
    string Path,
    string RomId,
    string RomHash,
    DateTimeOffset CreatedAtUtc);

internal static class HistoryRomMetadataKeys
{
    /// <summary>
    /// EN: Gets the RomHash constant.
    /// EN: Documentation for public API. JA: RomHash 定数を取得します。
    /// </summary>
    public const string RomHash = "history_rom_hash";
    /// <summary>
    /// EN: Gets the RomId constant.
    /// EN: Documentation for public API. JA: RomId 定数を取得します。
    /// </summary>

    public const string RomId = "history_rom_id";
    /// <summary>
    /// EN: Gets the RomPath constant.
    /// EN: Documentation for public API. JA: RomPath 定数を取得します。
    /// </summary>

    public const string RomPath = "history_rom_path";
    /// <summary>
    /// EN: Gets the RomNamespace constant.
    /// EN: Documentation for public API. JA: RomNamespace 定数を取得します。
    /// </summary>

    public const string RomNamespace = "history_rom_namespace";
    /// <summary>
    /// EN: Gets the RomName constant.
    /// EN: Documentation for public API. JA: RomName 定数を取得します。
    /// </summary>

    public const string RomName = "history_rom_name";
}
