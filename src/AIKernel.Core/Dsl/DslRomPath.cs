namespace AIKernel.Core.Dsl;

using AIKernel.Common.Results;
using AIKernel.Core.Vfs.Abstractions;

internal static class DslRomPath
{
    private const string CapabilityPrefix = "dsl://";

    public static Result<string> Create(string @namespace, string name)
    {
        var identity = ValidateIdentity(@namespace, name);
        return identity.Map(value => $"rom/dsl/{value.Namespace}/{value.Name}.json");
    }

    public static Result<string> CreateCapabilityName(string @namespace, string name)
    {
        var identity = ValidateIdentity(@namespace, name);
        return identity.Map(value => $"{CapabilityPrefix}{value.Namespace}/{value.Name}");
    }

    public static bool IsDslCapability(string capabilityName)
        => capabilityName is not null &&
           capabilityName.StartsWith(CapabilityPrefix, StringComparison.Ordinal);

    public static Result<(string Namespace, string Name)> ParseCapabilityName(
        string capabilityName)
    {
        var identity =
            from relative in
                from valid in RequireNonEmpty(
                capabilityName,
                "DSL ROM capability name is required.")
                from prefixed in RequirePrefix(
                    valid,
                    CapabilityPrefix,
                    "DSL ROM capability name must start with dsl://.")
                select prefixed[CapabilityPrefix.Length..]
            from separator in ReadSeparator(relative)
            select (
                Namespace: relative[..separator],
                Name: relative[(separator + 1)..]);

        return from value in identity.ToRomPathResult()
               from normalized in ValidateIdentity(value.Namespace, value.Name)
               select normalized;
    }

    private static Result<(string Namespace, string Name)> ValidateIdentity(
        string @namespace,
        string name)
    {
        var identity =
            from validNamespace in RequireNonEmpty(
                @namespace,
                "DSL ROM namespace is required.")
            from validName in RequireNonEmpty(
                name,
                "DSL ROM name is required.")
            select (Namespace: validNamespace, Name: validName);

        var normalized =
            from value in identity.ToRomPathResult()
            from normalizedNamespace in NormalizeSegment(value.Namespace)
            from normalizedName in NormalizeSegment(value.Name)
            select (Namespace: normalizedNamespace, Name: normalizedName);

        var singleSegments =
            from value in normalized
            from segments in
                (from validNamespace in RequireSinglePathSegment(
                        value.Namespace)
                 from validName in RequireSinglePathSegment(
                        value.Name)
                 select (Namespace: validNamespace, Name: validName))
                    .ToRomPathResult()
            select segments;

        return singleSegments;
    }

    private static Result<string> NormalizeSegment(
        string value)
        => Try.Run(() => VfsPathRules.Normalize(value))
            .MapRomPathError();

    private static Either<string, int> ReadSeparator(
        string relative)
    {
        var separator = relative.IndexOf('/', StringComparison.Ordinal);
        return separator <= 0 || separator == relative.Length - 1
            ? Either<string, int>.FromLeft(
                "DSL ROM capability name must be dsl://{namespace}/{name}.")
            : Either<string, int>.FromRight(separator);
    }

    private static Either<string, string> RequireNonEmpty(
        string value,
        string message)
        => string.IsNullOrWhiteSpace(value)
            ? Either<string, string>.FromLeft(message)
            : Either<string, string>.FromRight(value);

    private static Either<string, string> RequirePrefix(
        string value,
        string prefix,
        string message)
        => value.StartsWith(prefix, StringComparison.Ordinal)
            ? Either<string, string>.FromRight(value)
            : Either<string, string>.FromLeft(message);

    private static Either<string, string> RequireSinglePathSegment(
        string value)
        => value.Contains('/', StringComparison.Ordinal)
            ? Either<string, string>.FromLeft(
                "DSL ROM namespace and name must be single path segments.")
            : Either<string, string>.FromRight(value);

    private static Result<(string Namespace, string Name)> Invalid(string message)
        => DslRomPathResultExtensions.Invalid<(string Namespace, string Name)>(
            message);
}

internal static class DslRomPathResultExtensions
{
    public static Result<T> Invalid<T>(string message)
        => Result<T>.Fail(new ErrorContext(
            message,
            "DSL_ROM_ERROR",
            false)
        {
            FailureKind = FailureKind.FailClosed,
            OriginStep = OriginStep.KernelFacade,
            SemanticSlot = SemanticSlot.G
        });

    public static Result<T> MapRomPathError<T>(
        this Result<T> value)
        => value.Match(
            error => Result<T>.Fail(error with
            {
                Code = "DSL_ROM_ERROR",
                FailureKind = FailureKind.FailClosed,
                OriginStep = OriginStep.KernelFacade,
                SemanticSlot = SemanticSlot.G
            }),
            Result<T>.Success);

    public static Result<T> ToRomPathResult<T>(
        this Either<string, T> value)
        => value.Match(
            Invalid<T>,
            Result<T>.Success);
}
