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
        return await (
            from validSession in RequireSession(session).AsTask()
            from snapshot in _provider.CreateSnapshot(
                @namespace,
                name,
                jsonDsl,
                createdAtUtc,
                expectedRomHash)
            from _ in EnsureSnapshotStoredAsync(validSession, snapshot, jsonDsl)
            from metadata in RegisterSnapshot(snapshot)
            select metadata)
            .ConfigureAwait(false);
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

        return result.Match(
            error => throw new InvalidOperationException(error.Message),
            DslContractMapper.ToContract);
    }

    public async Task<Result<DslRomMetadata>> LoadDslRomAsync(
        IVfsSession session,
        string @namespace,
        string name,
        DateTimeOffset createdAtUtc,
        string expectedRomHash)
    {
        return await (
            from validSession in RequireSession(session).AsTask()
            from validHash in RequireExpectedHash(expectedRomHash).AsTask()
            from path in DslRomPath.Create(@namespace, name).AsTask()
            from jsonDsl in ReadJsonAsync(validSession, path)
            from snapshot in _provider.CreateSnapshot(
                @namespace,
                name,
                jsonDsl,
                createdAtUtc,
                validHash)
            from metadata in RegisterSnapshot(snapshot)
            select metadata)
            .ConfigureAwait(false);
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

        return result.Match(
            error => throw new InvalidOperationException(error.Message),
            DslContractMapper.ToContract);
    }

    private static ErrorContext Error(string message)
        => new(message, "DSL_ROM_ERROR", false)
        {
            FailureKind = FailureKind.FailClosed,
            OriginStep = OriginStep.KernelFacade,
            SemanticSlot = SemanticSlot.G
        };

    private static Result<IVfsSession> RequireSession(
        IVfsSession? session)
        => session is null
            ? Result<IVfsSession>.Fail(Error("VFS session is required."))
            : Result<IVfsSession>.Success(session);

    private static Result<string> RequireExpectedHash(
        string expectedRomHash)
        => RequireNonEmpty(expectedRomHash, "Expected DSL ROM hash is required.")
            .ToStoreResult();

    private static async Task<Result<bool>> EnsureSnapshotStoredAsync(
        IVfsSession session,
        DslRomSnapshot snapshot,
        string jsonDsl)
    {
        var exists = await Try.RunAsync(
                () => session.ExistsAsync(snapshot.Metadata.Path))
            .ConfigureAwait(false);
        return await exists.Match(
            error => Task.FromResult(Result<bool>.Fail(StoreException(error))),
            async value =>
            {
                if (value)
                {
                    return await ValidateExistingSnapshotAsync(session, snapshot)
                        .ConfigureAwait(false);
                }

                return (await Try.RunAsync(async () =>
            {
                await session.WriteFileAsync(
                        snapshot.Metadata.Path,
                        Encoding.UTF8.GetBytes(jsonDsl))
                    .ConfigureAwait(false);

                return true;
            }).ConfigureAwait(false))
                    .MapStoreError();
            }).ConfigureAwait(false);
    }

    private Result<DslRomMetadata> RegisterSnapshot(
        DslRomSnapshot snapshot)
    {
        return Try.Run(() => _registry.Register(snapshot))
            .Match(
                error => Result<DslRomMetadata>.Fail(StoreException(error)),
                value => value);
    }

    private static async Task<Result<bool>> ValidateExistingSnapshotAsync(
        IVfsSession session,
        DslRomSnapshot snapshot)
    {
        var existingJson = await ReadJsonUncheckedAsync(session, snapshot.Metadata.Path)
            .ConfigureAwait(false);
        var existingHash = DslRomHasher.ComputeHash(existingJson);

        return string.Equals(
            existingHash,
            snapshot.Metadata.RomHash,
            StringComparison.Ordinal)
            ? Result<bool>.Success(true)
            : Result<bool>.Fail(Error(
                "DSL ROM path already exists with different content."));
    }

    private static async Task<Result<string>> ReadJsonAsync(
        IVfsSession session,
        string path)
        => (await Try.RunAsync(() => ReadJsonUncheckedAsync(session, path))
                .ConfigureAwait(false))
            .MapStoreError();

    private static Either<string, string> RequireNonEmpty(
        string value,
        string message)
        => string.IsNullOrWhiteSpace(value)
            ? Either<string, string>.FromLeft(message)
            : Either<string, string>.FromRight(value);

    private static async Task<string> ReadJsonUncheckedAsync(
        IVfsSession session,
        string path)
    {
        var file = await session.ReadFileAsync(path).ConfigureAwait(false);
        var content = await file.ReadAsync().ConfigureAwait(false);
        return Encoding.UTF8.GetString(content);
    }

    private static ErrorContext StoreException(
        Exception exception)
        => ErrorContext.FromException(exception) with
        {
            FailureKind = FailureKind.FailClosed,
            OriginStep = OriginStep.KernelFacade,
            SemanticSlot = SemanticSlot.G
        };

    private static ErrorContext StoreException(
        ErrorContext error)
        => error with
        {
            FailureKind = FailureKind.FailClosed,
            OriginStep = OriginStep.KernelFacade,
            SemanticSlot = SemanticSlot.G
        };
}

internal static class DslRomStoreResultExtensions
{
    public static Result<T> ToStoreResult<T>(
        this Either<string, T> value)
        => value.Match(
            left => Result<T>.Fail(new ErrorContext(left, "DSL_ROM_ERROR", false)
            {
                FailureKind = FailureKind.FailClosed,
                OriginStep = OriginStep.KernelFacade,
                SemanticSlot = SemanticSlot.G
            }),
            Result<T>.Success);

    public static Result<T> MapStoreError<T>(
        this Result<T> value)
        => value.Match(
            error => Result<T>.Fail(error with
            {
                FailureKind = FailureKind.FailClosed,
                OriginStep = OriginStep.KernelFacade,
                SemanticSlot = SemanticSlot.G
            }),
            Result<T>.Success);
}
