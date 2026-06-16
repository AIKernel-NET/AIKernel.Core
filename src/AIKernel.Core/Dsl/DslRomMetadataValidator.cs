namespace AIKernel.Core.Dsl;

using AIKernel.Common.Results;

internal static class DslRomMetadataValidator
{
    /// <summary>
    /// EN: Executes ValidateCanonicalIdentity.
    /// EN: Documentation for public API. JA: ValidateCanonicalIdentity を実行します。
    /// </summary>
    public static ErrorContext? ValidateCanonicalIdentity(DslRomMetadata metadata)
        => ValidateCanonicalIdentityResult(metadata)
            .Match<ErrorContext?>(
                error => error,
                _ => null);

    private static Result<bool> ValidateCanonicalIdentityResult(
        DslRomMetadata metadata)
        => from _ in DslRomPath.ParseCapabilityName(metadata.CapabilityName)
           from __ in ValidateCapabilityName(metadata)
           from ___ in ValidatePath(metadata)
           select true;

    private static Result<bool> ValidateCapabilityName(
        DslRomMetadata metadata)
        => from expected in DslRomPath.CreateCapabilityName(
                metadata.Namespace,
                metadata.Name)
           from _ in RequireEqual(
                expected,
                metadata.CapabilityName,
                "DSL ROM capability name must match dsl://{namespace}/{name}.")
           select true;

    private static Result<bool> ValidatePath(
        DslRomMetadata metadata)
        => from expected in DslRomPath.Create(metadata.Namespace, metadata.Name)
           from _ in RequireEqual(
                expected,
                metadata.Path,
                "DSL ROM path must match rom/dsl/{namespace}/{name}.json.")
           select true;

    private static Result<bool> RequireEqual(
        string expected,
        string actual,
        string message)
        => RequireEqualEither(expected, actual, message)
            .Match(
                left => Result<bool>.Fail(Error(left)),
                _ => Result<bool>.Success(true));

    private static Either<string, bool> RequireEqualEither(
        string expected,
        string actual,
        string message)
        => string.Equals(expected, actual, StringComparison.Ordinal)
            ? Either<string, bool>.FromRight(true)
            : Either<string, bool>.FromLeft(message);

    private static ErrorContext Error(string message)
        => new(message, "DSL_ROM_ERROR", false)
        {
            FailureKind = FailureKind.FailClosed,
            OriginStep = OriginStep.KernelFacade,
            SemanticSlot = SemanticSlot.G
        };
}
