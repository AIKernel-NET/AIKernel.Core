namespace AIKernel.Core.ChatHistory;

public sealed record HistoryRomMetadata(
    string Namespace,
    string Name,
    string Path,
    string RomId,
    string RomHash,
    DateTimeOffset CreatedAtUtc);

public static class HistoryRomMetadataKeys
{
    public const string RomHash = "history_rom_hash";

    public const string RomId = "history_rom_id";

    public const string RomPath = "history_rom_path";

    public const string RomNamespace = "history_rom_namespace";

    public const string RomName = "history_rom_name";
}
