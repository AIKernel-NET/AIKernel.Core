namespace AIKernel.Core.Dsl;

using AIKernel.Common.Results;

internal sealed record DslRomSnapshot(
    DslRomMetadata Metadata,
    string JsonDsl,
    IKernelPipeline Pipeline);

internal sealed class DslRomProvider
{
    private readonly IDslPipelineCompiler _compiler;

    public DslRomProvider(IDslPipelineCompiler compiler)
    {
        _compiler = compiler ?? throw new ArgumentNullException(nameof(compiler));
    }

    public Result<DslRomSnapshot> CreateSnapshot(
        string @namespace,
        string name,
        string jsonDsl,
        DateTimeOffset createdAtUtc,
        string? expectedRomHash = null)
    {
        return
            from validJsonDsl in ValidateJsonDsl(jsonDsl)
            from path in DslRomPath.Create(@namespace, name)
            from capabilityName in DslRomPath.CreateCapabilityName(@namespace, name)
            let hash = DslRomHasher.ComputeHash(validJsonDsl)
            from _ in ValidateExpectedHash(hash, expectedRomHash)
            from document in DslDocument.FromJson(validJsonDsl)
            from pipeline in Compile(document)
            from identity in DslRomPath.ParseCapabilityName(capabilityName)
            select new DslRomSnapshot(
                new DslRomMetadata(
                    identity.Namespace,
                    identity.Name,
                    path,
                    capabilityName,
                    hash,
                    createdAtUtc),
                validJsonDsl,
                pipeline);
    }

    private static Result<string> ValidateJsonDsl(
        string jsonDsl)
        => RequireNonEmpty(jsonDsl, "DSL ROM JSON is required.")
            .ToRomProviderResult();

    private static Result<bool> ValidateExpectedHash(
        string actualHash,
        string? expectedRomHash)
        => ReadExpectedHash(expectedRomHash)
            .Map(expected => RequireMatchingHash(actualHash, expected))
            .OrElse(Either<string, bool>.FromRight(true))
            .ToRomProviderResult();

    private Result<IKernelPipeline> Compile(
        DslDocument document)
        => Try.Run(() => _compiler.Compile(document))
            .Match(
                error => Result<IKernelPipeline>.Fail(
                    RomProviderException(error)),
                compiled => compiled.Bind(RequirePipeline));

    private static Result<IKernelPipeline> RequirePipeline(
        IKernelPipeline? pipeline)
        => OptionalPipeline(pipeline)
            .Map(Result<IKernelPipeline>.Success)
            .OrElse(Result<IKernelPipeline>.Fail(Error(
                "DSL ROM compiler returned a successful null pipeline.")));

    private static Either<string, string> RequireNonEmpty(
        string value,
        string message)
        => string.IsNullOrWhiteSpace(value)
            ? Either<string, string>.FromLeft(message)
            : Either<string, string>.FromRight(value);

    private static Option<string> ReadExpectedHash(
        string? expectedRomHash)
        => string.IsNullOrWhiteSpace(expectedRomHash)
            ? Option<string>.None()
            : Option<string>.Some(expectedRomHash);

    private static Either<string, bool> RequireMatchingHash(
        string actualHash,
        string expectedHash)
        => string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase)
            ? Either<string, bool>.FromRight(true)
            : Either<string, bool>.FromLeft("DSL ROM hash mismatch.");

    private static Option<IKernelPipeline> OptionalPipeline(
        IKernelPipeline? pipeline)
        => pipeline is null
            ? Option<IKernelPipeline>.None()
            : Option<IKernelPipeline>.Some(pipeline);

    private static ErrorContext RomProviderException(
        ErrorContext error)
        => error with
        {
            FailureKind = FailureKind.FailClosed,
            OriginStep = OriginStep.KernelFacade,
            SemanticSlot = SemanticSlot.G
        };

    private static ErrorContext Error(string message)
        => new(message, "DSL_ROM_ERROR", false)
        {
            FailureKind = FailureKind.FailClosed,
            OriginStep = OriginStep.KernelFacade,
            SemanticSlot = SemanticSlot.G
        };
}

internal static class DslRomProviderEitherExtensions
{
    public static Result<T> ToRomProviderResult<T>(
        this Either<string, T> value)
        => value.Match(
            left => Result<T>.Fail(new ErrorContext(left, "DSL_ROM_ERROR", false)
            {
                FailureKind = FailureKind.FailClosed,
                OriginStep = OriginStep.KernelFacade,
                SemanticSlot = SemanticSlot.G
            }),
            Result<T>.Success);
}
