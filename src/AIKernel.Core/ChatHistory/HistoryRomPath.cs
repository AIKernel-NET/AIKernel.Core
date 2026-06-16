namespace AIKernel.Core.ChatHistory;

using AIKernel.Common.Results;
using AIKernel.Core.Vfs.Abstractions;

internal static class HistoryRomPath
{
    private const string HistoryPrefix = "history://";
    /// <summary>
    /// EN: Executes Create.
    /// EN: Documentation for public API. JA: Create を実行します。
    /// </summary>

    public static Result<string> Create(string @namespace, string name)
    {
        var identity = ValidateIdentity(@namespace, name);
        return identity.Map(value => $"rom/history/{value.Namespace}/{value.Name}.md");
    }
    /// <summary>
    /// EN: Executes CreateRomId.
    /// EN: Documentation for public API. JA: CreateRomId を実行します。
    /// </summary>

    public static Result<string> CreateRomId(string @namespace, string name)
    {
        var identity = ValidateIdentity(@namespace, name);
        return identity.Map(value => $"{HistoryPrefix}{value.Namespace}/{value.Name}");
    }
    /// <summary>
    /// EN: Executes IsHistoryRomId.
    /// EN: Documentation for public API. JA: IsHistoryRomId を実行します。
    /// </summary>

    public static bool IsHistoryRomId(string romId)
        => romId is not null &&
           romId.StartsWith(HistoryPrefix, StringComparison.Ordinal);
    /// <summary>
    /// EN: Executes Result&lt;.
    /// EN: Documentation for public API. JA: Result&lt; を実行します。
    /// </summary>

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

        return
            from normalizedNamespace in NormalizeSegment(@namespace)
            from normalizedName in NormalizeSegment(name)
            from identity in ValidateSingleSegments(normalizedNamespace, normalizedName)
            select identity;
    }

    private static Result<string> NormalizeSegment(string value)
        => Try
            .Run(() => VfsPathRules.Normalize(value))
            .Match(error => Result<string>.Fail(HistoryRomErrors.Error(error.Message)), Result<string>.Success);

    private static Result<(string Namespace, string Name)> ValidateSingleSegments(
        string normalizedNamespace,
        string normalizedName)
        => normalizedNamespace.Contains('/', StringComparison.Ordinal) ||
           normalizedName.Contains('/', StringComparison.Ordinal)
            ? Invalid("History ROM namespace and name must be single path segments.")
            : Result<(string Namespace, string Name)>.Success(
                (normalizedNamespace, normalizedName));

    private static Result<(string Namespace, string Name)> Invalid(string message)
        => Result<(string Namespace, string Name)>.Fail(HistoryRomErrors.Error(message));
}
