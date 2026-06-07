namespace AIKernel.Core.Dsl;

using System.Text;
using AIKernel.Common.Results;
using AIKernel.Vfs;

internal sealed class DslRomStore : AIKernel.Abstractions.Dsl.IDslRomStore
{
    private readonly DslRomProvider _provider;
    private readonly IDslRomRegistry _registry;

    public DslRomStore(
        DslRomProvider provider,
        IDslRomRegistry registry)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public async Task<Result<DslRomMetadata>> SaveDslAsRomAsync(
        IVfsSession session,
        string @namespace,
        string name,
        string jsonDsl,
        DateTimeOffset createdAtUtc,
        string? expectedRomHash = null)
    {
        if (session is null)
        {
            return Result<DslRomMetadata>.Fail(Error("VFS session is required."));
        }

        var snapshot = _provider.CreateSnapshot(
            @namespace,
            name,
            jsonDsl,
            createdAtUtc,
            expectedRomHash);

        if (snapshot.IsFailure)
        {
            return Result<DslRomMetadata>.Fail(snapshot.Error!);
        }

        try
        {
            if (await session.ExistsAsync(snapshot.Value!.Metadata.Path).ConfigureAwait(false))
            {
                var existing = await session.ReadFileAsync(snapshot.Value.Metadata.Path)
                    .ConfigureAwait(false);
                var existingContent = await existing.ReadAsync().ConfigureAwait(false);
                var existingJson = Encoding.UTF8.GetString(existingContent);
                var existingHash = DslRomHasher.ComputeHash(existingJson);

                if (!string.Equals(
                        existingHash,
                        snapshot.Value.Metadata.RomHash,
                        StringComparison.Ordinal))
                {
                    return Result<DslRomMetadata>.Fail(Error(
                        "DSL ROM path already exists with different content."));
                }
            }
            else
            {
                await session.WriteFileAsync(
                        snapshot.Value.Metadata.Path,
                        Encoding.UTF8.GetBytes(jsonDsl))
                    .ConfigureAwait(false);
            }

            return _registry.Register(snapshot.Value);
        }
        catch (Exception ex)
        {
            return Result<DslRomMetadata>.Fail(ErrorContext.FromException(ex) with
            {
                FailureKind = FailureKind.FailClosed,
                OriginStep = OriginStep.KernelFacade,
                SemanticSlot = SemanticSlot.G
            });
        }
    }

    async Task<AIKernel.Dtos.Dsl.DslRomMetadata>
        AIKernel.Abstractions.Dsl.IDslRomStore.SaveDslAsRomAsync(
            IVfsSession session,
            string @namespace,
            string name,
            string jsonDsl,
            DateTimeOffset createdAtUtc,
            string? expectedRomHash,
            CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var result = await SaveDslAsRomAsync(
                session,
                @namespace,
                name,
                jsonDsl,
                createdAtUtc,
                expectedRomHash)
            .ConfigureAwait(false);

        if (result.IsFailure)
        {
            throw new InvalidOperationException(result.Error!.Message);
        }

        return DslContractMapper.ToContract(result.Value!);
    }

    public async Task<Result<DslRomMetadata>> LoadDslRomAsync(
        IVfsSession session,
        string @namespace,
        string name,
        DateTimeOffset createdAtUtc,
        string expectedRomHash)
    {
        if (session is null)
        {
            return Result<DslRomMetadata>.Fail(Error("VFS session is required."));
        }

        if (string.IsNullOrWhiteSpace(expectedRomHash))
        {
            return Result<DslRomMetadata>.Fail(Error("Expected DSL ROM hash is required."));
        }

        var path = DslRomPath.Create(@namespace, name);
        if (path.IsFailure)
        {
            return Result<DslRomMetadata>.Fail(path.Error!);
        }

        try
        {
            var file = await session.ReadFileAsync(path.Value!).ConfigureAwait(false);
            var content = await file.ReadAsync().ConfigureAwait(false);
            var jsonDsl = Encoding.UTF8.GetString(content);

            var snapshot = _provider.CreateSnapshot(
                @namespace,
                name,
                jsonDsl,
                createdAtUtc,
                expectedRomHash);

            if (snapshot.IsFailure)
            {
                return Result<DslRomMetadata>.Fail(snapshot.Error!);
            }

            return _registry.Register(snapshot.Value!);
        }
        catch (Exception ex)
        {
            return Result<DslRomMetadata>.Fail(ErrorContext.FromException(ex) with
            {
                FailureKind = FailureKind.FailClosed,
                OriginStep = OriginStep.KernelFacade,
                SemanticSlot = SemanticSlot.G
            });
        }
    }

    async Task<AIKernel.Dtos.Dsl.DslRomMetadata>
        AIKernel.Abstractions.Dsl.IDslRomStore.LoadDslRomAsync(
            IVfsSession session,
            string @namespace,
            string name,
            DateTimeOffset createdAtUtc,
            string expectedRomHash,
            CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var result = await LoadDslRomAsync(
                session,
                @namespace,
                name,
                createdAtUtc,
                expectedRomHash)
            .ConfigureAwait(false);

        if (result.IsFailure)
        {
            throw new InvalidOperationException(result.Error!.Message);
        }

        return DslContractMapper.ToContract(result.Value!);
    }

    private static ErrorContext Error(string message)
        => new(message, "DSL_ROM_ERROR", false)
        {
            FailureKind = FailureKind.FailClosed,
            OriginStep = OriginStep.KernelFacade,
            SemanticSlot = SemanticSlot.G
        };
}
