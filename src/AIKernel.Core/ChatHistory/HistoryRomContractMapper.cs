namespace AIKernel.Core.ChatHistory;

using ContractHistory = AIKernel.Dtos.History;

internal static class HistoryRomContractMapper
{
    public static ContractHistory.HistoryRomMetadata ToContract(
        HistoryRomMetadata metadata)
        => new(
            metadata.Namespace,
            metadata.Name,
            metadata.Path,
            metadata.RomId,
            metadata.RomHash,
            metadata.CreatedAtUtc);

    public static HistoryRomMetadata ToCore(
        ContractHistory.HistoryRomMetadata metadata)
        => new(
            metadata.Namespace,
            metadata.Name,
            metadata.Path,
            metadata.RomId,
            metadata.RomHash,
            metadata.CreatedAtUtc);

    public static ContractHistory.HistoryRomSnapshot ToContract(
        HistoryRomSnapshot snapshot)
        => new(
            ToContract(snapshot.Metadata),
            snapshot.Markdown,
            snapshot.Rom);

    public static IReadOnlyList<ChatHistoryRomRecord> ToCore(
        IReadOnlyList<ContractHistory.ChatHistoryRomRecord> records)
        => records
            .Select(record => new ChatHistoryRomRecord(
                record.Role,
                record.Content,
                record.Timestamp))
            .ToArray();

    public static ChatHistoryRomOptions ToCore(
        ContractHistory.ChatHistoryRomOptions options)
        => new(
            options.RomId,
            options.GeneratedAtUtc,
            options.EntityType,
            options.Version,
            options.SecurityTags);
}
