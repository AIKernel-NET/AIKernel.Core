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
    public const string RomHash = "dsl_rom_hash";

    public const string RomCall = "dsl_rom_call";

    public const string RomPath = "dsl_rom_path";

    public const string RomNamespace = "dsl_rom_namespace";

    public const string RomName = "dsl_rom_name";

    public const string RomReplayLogCount = "dsl_rom_replay_log_count";

    public const string RomReplayLogHash = "dsl_rom_replay_log_hash";
}
