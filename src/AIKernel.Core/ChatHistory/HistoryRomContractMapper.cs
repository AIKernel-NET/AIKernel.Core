namespace AIKernel.Core.ChatHistory;

using ContractHistory = AIKernel.Dtos.History;

internal static class HistoryRomContractMapper
{
    /// <summary>
    /// EN: Gets ToContract.
    /// EN: Documentation for public API. JA: ToContract を取得します。
    /// </summary>
    public static ContractHistory.HistoryRomMetadata ToContract(
        HistoryRomMetadata metadata)
        => new(
            metadata.Namespace,
            metadata.Name,
            metadata.Path,
            metadata.RomId,
            metadata.RomHash,
            metadata.CreatedAtUtc);
    /// <summary>
    /// EN: Gets ToCore.
    /// EN: Documentation for public API. JA: ToCore を取得します。
    /// </summary>

    public static HistoryRomMetadata ToCore(
        ContractHistory.HistoryRomMetadata metadata)
        => new(
            metadata.Namespace,
            metadata.Name,
            metadata.Path,
            metadata.RomId,
            metadata.RomHash,
            metadata.CreatedAtUtc);
    /// <summary>
    /// EN: Gets ToContract.
    /// EN: Documentation for public API. JA: ToContract を取得します。
    /// </summary>

    public static ContractHistory.HistoryRomSnapshot ToContract(
        HistoryRomSnapshot snapshot)
        => new(
            ToContract(snapshot.Metadata),
            snapshot.Markdown,
            snapshot.Rom);
    /// <summary>
    /// EN: Gets ToCore.
    /// EN: Documentation for public API. JA: ToCore を取得します。
    /// </summary>

    public static IReadOnlyList<ChatHistoryRomRecord> ToCore(
        IReadOnlyList<ContractHistory.ChatHistoryRomRecord> records)
        => records
            .Select(record => new ChatHistoryRomRecord(
                record.Role,
                record.Content,
                record.Timestamp))
            .ToArray();
    /// <summary>
    /// EN: Gets ToCore.
    /// EN: Documentation for public API. JA: ToCore を取得します。
    /// </summary>

    public static ChatHistoryRomOptions ToCore(
        ContractHistory.ChatHistoryRomOptions options)
        => new(
            options.RomId,
            options.GeneratedAtUtc,
            options.EntityType,
            options.Version,
            options.SecurityTags);
}
