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
        if (string.IsNullOrWhiteSpace(jsonDsl))
        {
            return Result<DslRomSnapshot>.Fail(Error("DSL ROM JSON is required."));
        }

        var path = DslRomPath.Create(@namespace, name);
        if (path.IsFailure)
        {
            return Result<DslRomSnapshot>.Fail(path.Error!);
        }

        var capabilityName = DslRomPath.CreateCapabilityName(@namespace, name);
        if (capabilityName.IsFailure)
        {
            return Result<DslRomSnapshot>.Fail(capabilityName.Error!);
        }

        var hash = DslRomHasher.ComputeHash(jsonDsl);
        if (!string.IsNullOrWhiteSpace(expectedRomHash) &&
            !string.Equals(hash, expectedRomHash, StringComparison.OrdinalIgnoreCase))
        {
            return Result<DslRomSnapshot>.Fail(Error(
                "DSL ROM hash mismatch."));
        }

        var document = DslDocument.FromJson(jsonDsl);
        if (document.IsFailure)
        {
            return Result<DslRomSnapshot>.Fail(document.Error!);
        }

        Result<IKernelPipeline> pipeline;
        try
        {
            pipeline = _compiler.Compile(document.Value!);
        }
        catch (Exception ex)
        {
            return Result<DslRomSnapshot>.Fail(ErrorContext.FromException(ex) with
            {
                FailureKind = FailureKind.FailClosed,
                OriginStep = OriginStep.KernelFacade,
                SemanticSlot = SemanticSlot.G
            });
        }

        if (pipeline.IsFailure)
        {
            return Result<DslRomSnapshot>.Fail(pipeline.Error!);
        }

        if (pipeline.Value is null)
        {
            return Result<DslRomSnapshot>.Fail(Error("DSL ROM compiler returned a successful null pipeline."));
        }

        var identity = DslRomPath.ParseCapabilityName(capabilityName.Value!);
        if (identity.IsFailure)
        {
            return Result<DslRomSnapshot>.Fail(identity.Error!);
        }

        return Result<DslRomSnapshot>.Success(new DslRomSnapshot(
            new DslRomMetadata(
                identity.Value.Namespace,
                identity.Value.Name,
                path.Value!,
                capabilityName.Value!,
                hash,
                createdAtUtc),
            jsonDsl,
            pipeline.Value!));
    }

    private static ErrorContext Error(string message)
        => new(message, "DSL_ROM_ERROR", false)
        {
            FailureKind = FailureKind.FailClosed,
            OriginStep = OriginStep.KernelFacade,
            SemanticSlot = SemanticSlot.G
        };
}
