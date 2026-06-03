namespace AIKernel.Core.ChatHistory;

using AIKernel.Common.Results;

internal static class HistoryRomMetadataValidator
{
    public static ErrorContext? ValidateCanonicalIdentity(HistoryRomMetadata metadata)
    {
        var parsed = HistoryRomPath.ParseRomId(metadata.RomId);
        if (parsed.IsFailure)
        {
            return parsed.Error!;
        }

        var expectedRomId = HistoryRomPath.CreateRomId(
            metadata.Namespace,
            metadata.Name);
        if (expectedRomId.IsFailure)
        {
            return expectedRomId.Error!;
        }

        if (!string.Equals(
                expectedRomId.Value,
                metadata.RomId,
                StringComparison.Ordinal))
        {
            return HistoryRomErrors.Error("History ROM id must match history://{namespace}/{name}.");
        }

        var expectedPath = HistoryRomPath.Create(metadata.Namespace, metadata.Name);
        if (expectedPath.IsFailure)
        {
            return expectedPath.Error!;
        }

        if (!string.Equals(
                expectedPath.Value,
                metadata.Path,
                StringComparison.Ordinal))
        {
            return HistoryRomErrors.Error("History ROM path must match rom/history/{namespace}/{name}.md.");
        }

        return null;
    }
}
