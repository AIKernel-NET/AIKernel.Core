namespace AIKernel.Core.ChatHistory;

using AIKernel.Abstractions.Rom;
using AIKernel.Common.Results;
using AIKernel.Dtos.Rom;

public sealed record HistoryRomSnapshot(
    HistoryRomMetadata Metadata,
    string Markdown,
    RomSnapshot Rom);

public sealed class HistoryRomProvider
{
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

        var path = HistoryRomPath.Create(@namespace, name);
        if (path.IsFailure)
        {
            return Result<HistoryRomSnapshot>.Fail(path.Error!);
        }

        var romId = HistoryRomPath.CreateRomId(@namespace, name);
        if (romId.IsFailure)
        {
            return Result<HistoryRomSnapshot>.Fail(romId.Error!);
        }

        if (!string.Equals(rom.RomId.Value, romId.Value, StringComparison.Ordinal))
        {
            return Result<HistoryRomSnapshot>.Fail(
                HistoryRomErrors.Error("Loaded History ROM id does not match the requested identity."));
        }

        if (!string.Equals(rom.SourcePath, path.Value, StringComparison.Ordinal))
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

        var identity = HistoryRomPath.ParseRomId(romId.Value!);
        if (identity.IsFailure)
        {
            return Result<HistoryRomSnapshot>.Fail(identity.Error!);
        }

        return Result<HistoryRomSnapshot>.Success(new HistoryRomSnapshot(
            new HistoryRomMetadata(
                identity.Value.Namespace,
                identity.Value.Name,
                path.Value!,
                romId.Value!,
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
