namespace AIKernel.Core.ChatHistory;

using AIKernel.Common.Results;
using AIKernel.Core.Vfs.Abstractions;

internal static class HistoryRomPath
{
    private const string HistoryPrefix = "history://";

    public static Result<string> Create(string @namespace, string name)
    {
        var identity = ValidateIdentity(@namespace, name);
        return identity.Map(value => $"rom/history/{value.Namespace}/{value.Name}.md");
    }

    public static Result<string> CreateRomId(string @namespace, string name)
    {
        var identity = ValidateIdentity(@namespace, name);
        return identity.Map(value => $"{HistoryPrefix}{value.Namespace}/{value.Name}");
    }

    public static bool IsHistoryRomId(string romId)
        => romId is not null &&
           romId.StartsWith(HistoryPrefix, StringComparison.Ordinal);

    public static Result<(string Namespace, string Name)> ParseRomId(string romId)
    {
        if (string.IsNullOrWhiteSpace(romId))
        {
            return Invalid("History ROM id is required.");
        }

        if (!romId.StartsWith(HistoryPrefix, StringComparison.Ordinal))
        {
            return Invalid("History ROM id must start with history://.");
        }

        var relative = romId[HistoryPrefix.Length..];
        var separator = relative.IndexOf('/', StringComparison.Ordinal);
        if (separator <= 0 || separator == relative.Length - 1)
        {
            return Invalid("History ROM id must be history://{namespace}/{name}.");
        }

        return ValidateIdentity(
            relative[..separator],
            relative[(separator + 1)..]);
    }

    private static Result<(string Namespace, string Name)> ValidateIdentity(
        string @namespace,
        string name)
    {
        if (string.IsNullOrWhiteSpace(@namespace))
        {
            return Invalid("History ROM namespace is required.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return Invalid("History ROM name is required.");
        }

        try
        {
            var normalizedNamespace = VfsPathRules.Normalize(@namespace);
            var normalizedName = VfsPathRules.Normalize(name);

            if (normalizedNamespace.Contains('/', StringComparison.Ordinal) ||
                normalizedName.Contains('/', StringComparison.Ordinal))
            {
                return Invalid("History ROM namespace and name must be single path segments.");
            }

            return Result<(string Namespace, string Name)>.Success(
                (normalizedNamespace, normalizedName));
        }
        catch (ArgumentException ex)
        {
            return Invalid(ex.Message);
        }
    }

    private static Result<(string Namespace, string Name)> Invalid(string message)
        => Result<(string Namespace, string Name)>.Fail(HistoryRomErrors.Error(message));
}
