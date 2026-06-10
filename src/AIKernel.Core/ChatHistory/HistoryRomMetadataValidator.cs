namespace AIKernel.Core.ChatHistory;

using AIKernel.Common.Results;

internal static class HistoryRomMetadataValidator
{
    public static ErrorContext? ValidateCanonicalIdentity(HistoryRomMetadata metadata)
        => ValidateCanonicalIdentityResult(metadata)
            .Match<ErrorContext?>(
                error => error,
                _ => null);

    private static Result<bool> ValidateCanonicalIdentityResult(
        HistoryRomMetadata metadata)
        => from _ in HistoryRomPath.ParseRomId(metadata.RomId)
           from __ in ValidateRomId(metadata)
           from ___ in ValidatePath(metadata)
           select true;

    private static Result<bool> ValidateRomId(
        HistoryRomMetadata metadata)
        => from expected in HistoryRomPath.CreateRomId(
                metadata.Namespace,
                metadata.Name)
           from _ in RequireEqual(
                expected,
                metadata.RomId,
                "History ROM id must match history://{namespace}/{name}.")
           select true;

    private static Result<bool> ValidatePath(
        HistoryRomMetadata metadata)
        => from expected in HistoryRomPath.Create(
                metadata.Namespace,
                metadata.Name)
           from _ in RequireEqual(
                expected,
                metadata.Path,
                "History ROM path must match rom/history/{namespace}/{name}.md.")
           select true;

    private static Result<bool> RequireEqual(
        string expected,
        string actual,
        string message)
        => string.Equals(expected, actual, StringComparison.Ordinal)
            ? Result<bool>.Success(true)
            : Result<bool>.Fail(HistoryRomErrors.Error(message));
}
