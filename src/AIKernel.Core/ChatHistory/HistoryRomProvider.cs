namespace AIKernel.Core.ChatHistory;

using AIKernel.Abstractions.Rom;
using AIKernel.Common.Results;
using AIKernel.Dtos.Rom;

internal sealed record HistoryRomSnapshot(
    HistoryRomMetadata Metadata,
    string Markdown,
    RomSnapshot Rom);

internal sealed class HistoryRomProvider
{
    /// <summary>
    /// EN: Gets CreateSnapshot.
    /// [EN] Documents this public package API member. [JA] CreateSnapshot を取得します。
    /// </summary>
    public Result<HistoryRomSnapshot> CreateSnapshot(
        string @namespace,
        string name,
        string markdown,
        DateTimeOffset createdAtUtc,
        RomSnapshot rom,
        string? expectedRomHash = null)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return Result<HistoryRomSnapshot>.Fail(
                HistoryRomErrors.Error("History ROM markdown is required."));
        }

        if (rom is null)
        {
            return Result<HistoryRomSnapshot>.Fail(
                HistoryRomErrors.Error("History ROM snapshot is required."));
        }

        var identity =
            from path in HistoryRomPath.Create(@namespace, name)
            from romId in HistoryRomPath.CreateRomId(@namespace, name)
            from parsed in HistoryRomPath.ParseRomId(romId)
            select (Path: path, RomId: romId, Parsed: parsed);

        return identity.Match(
            Result<HistoryRomSnapshot>.Fail,
            resolved => CreateSnapshotFromIdentity(
                resolved,
                markdown,
                createdAtUtc,
                rom,
                expectedRomHash));
    }

    private static Result<HistoryRomSnapshot> CreateSnapshotFromIdentity(
        (string Path, string RomId, (string Namespace, string Name) Parsed) resolved,
        string markdown,
        DateTimeOffset createdAtUtc,
        RomSnapshot rom,
        string? expectedRomHash)
    {
        if (!string.Equals(rom.RomId.Value, resolved.RomId, StringComparison.Ordinal))
        {
            return Result<HistoryRomSnapshot>.Fail(
                HistoryRomErrors.Error("Loaded History ROM id does not match the requested identity."));
        }

        if (!string.Equals(rom.SourcePath, resolved.Path, StringComparison.Ordinal))
        {
            return Result<HistoryRomSnapshot>.Fail(
                HistoryRomErrors.Error("Loaded History ROM path does not match the canonical path."));
        }

        if (rom.Signature is null || !rom.Signature.IsVerified)
        {
            return Result<HistoryRomSnapshot>.Fail(
                HistoryRomErrors.Error("History ROM signature is not verified."));
        }

        if (!string.Equals(
                "chat_history",
                GetMetadata(rom, "source_kind"),
                StringComparison.Ordinal))
        {
            return Result<HistoryRomSnapshot>.Fail(
                HistoryRomErrors.Error("History ROM source_kind must be chat_history."));
        }

        var hash = rom.Signature.ActualHash;
        if (!string.IsNullOrWhiteSpace(expectedRomHash) &&
            !string.Equals(hash, expectedRomHash, StringComparison.Ordinal))
        {
            return Result<HistoryRomSnapshot>.Fail(
                HistoryRomErrors.Error("History ROM hash mismatch."));
        }

        return Result<HistoryRomSnapshot>.Success(new HistoryRomSnapshot(
            new HistoryRomMetadata(
                resolved.Parsed.Namespace,
                resolved.Parsed.Name,
                resolved.Path,
                resolved.RomId,
                hash,
                createdAtUtc),
            markdown,
            rom));
    }

    private static string? GetMetadata(RomSnapshot rom, string key)
        => rom.AdditionalMetadata.TryGetValue(key, out var value)
            ? value
            : null;
}
