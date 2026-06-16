namespace AIKernel.Core.ChatHistory;

using System.Text;
using AIKernel.Abstractions.Rom;
using AIKernel.Common.Results;
using AIKernel.Vfs;

internal sealed class HistoryRomStore : AIKernel.Abstractions.History.IHistoryRomStore
{
    private readonly HistoryRomProvider _provider;
    private readonly IHistoryRomRegistry _registry;
    private readonly IRomLoader _loader;
    /// <summary>
    /// EN: Gets HistoryRomStore.
    /// EN: Documentation for public API. JA: HistoryRomStore を取得します。
    /// </summary>

    public HistoryRomStore(
        HistoryRomProvider provider,
        IHistoryRomRegistry registry,
        IRomLoader loader)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _loader = loader ?? throw new ArgumentNullException(nameof(loader));
    }
    /// <summary>
    /// EN: Gets SaveHistoryAsRomAsync.
    /// EN: Documentation for public API. JA: SaveHistoryAsRomAsync を取得します。
    /// </summary>

    public async Task<Result<HistoryRomMetadata>> SaveHistoryAsRomAsync(
        IVfsSession session,
        string @namespace,
        string name,
        IReadOnlyList<ChatHistoryRomRecord> records,
        DateTimeOffset generatedAtUtc,
        string entityType = "conversation",
        string version = "1",
        IReadOnlyList<string>? securityTags = null)
    {
        var markdown =
            from romId in HistoryRomPath.CreateRomId(@namespace, name)
            from text in ChatHistoryRomExporter.ToRomMarkdown(
                records,
                new ChatHistoryRomOptions(
                    romId,
                    generatedAtUtc,
                    entityType,
                    version,
                    securityTags))
            select text;

        return await markdown.Match(
            error => Task.FromResult(Result<HistoryRomMetadata>.Fail(error)),
            text => SaveMarkdownAsRomAsync(
                session,
                @namespace,
                name,
                text,
                generatedAtUtc))
            .ConfigureAwait(false);
    }

    async Task<AIKernel.Dtos.History.HistoryRomMetadata>
        AIKernel.Abstractions.History.IHistoryRomStore.SaveHistoryAsRomAsync(
            IVfsSession session,
            string @namespace,
            string name,
            IReadOnlyList<AIKernel.Dtos.History.ChatHistoryRomRecord> records,
            DateTimeOffset generatedAtUtc,
            string entityType,
            string version,
            IReadOnlyList<string>? securityTags,
            CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var result = await SaveHistoryAsRomAsync(
                session,
                @namespace,
                name,
                HistoryRomContractMapper.ToCore(records),
                generatedAtUtc,
                entityType,
                version,
                securityTags)
            .ConfigureAwait(false);

        return result.Match(
            error => throw new InvalidOperationException(error.Message),
            HistoryRomContractMapper.ToContract);
    }
    /// <summary>
    /// EN: Gets SaveMarkdownAsRomAsync.
    /// EN: Documentation for public API. JA: SaveMarkdownAsRomAsync を取得します。
    /// </summary>

    public async Task<Result<HistoryRomMetadata>> SaveMarkdownAsRomAsync(
        IVfsSession session,
        string @namespace,
        string name,
        string markdown,
        DateTimeOffset createdAtUtc)
    {
        if (session is null)
        {
            return Result<HistoryRomMetadata>.Fail(
                HistoryRomErrors.Error("VFS session is required."));
        }

        return await HistoryRomPath.Create(@namespace, name)
            .Match(
                error => Task.FromResult(Result<HistoryRomMetadata>.Fail(error)),
                path => SaveMarkdownAtPathAsync(
                    session,
                    @namespace,
                    name,
                    markdown,
                    createdAtUtc,
                    path))
            .ConfigureAwait(false);
    }

    private async Task<Result<HistoryRomMetadata>> SaveMarkdownAtPathAsync(
        IVfsSession session,
        string @namespace,
        string name,
        string markdown,
        DateTimeOffset createdAtUtc,
        string path)
    {
        var wroteNewFile = false;

        var result = await Try.RunAsync(async () =>
        {
            if (await session.ExistsAsync(path).ConfigureAwait(false))
            {
                var existing = await ReadMarkdownAsync(session, path)
                    .ConfigureAwait(false);
                if (!string.Equals(existing, markdown, StringComparison.Ordinal))
                {
                    return Result<HistoryRomMetadata>.Fail(HistoryRomErrors.Error(
                        "History ROM path already exists with different content."));
                }
            }
            else
            {
                await session.WriteFileAsync(
                        path,
                        Encoding.UTF8.GetBytes(markdown))
                    .ConfigureAwait(false);
                wroteNewFile = true;
            }

            var loaded = await LoadHistoryRomAsync(
                    session,
                    @namespace,
                    name,
                    createdAtUtc)
                .ConfigureAwait(false);

            await DeleteWhenFailedAsync(loaded, wroteNewFile, session, path)
                .ConfigureAwait(false);

            return loaded;
        }).ConfigureAwait(false);

        await DeleteWhenFailedAsync(result, wroteNewFile, session, path)
            .ConfigureAwait(false);

        return result.Match(FailClosedHistoryRom<HistoryRomMetadata>, value => value);
    }

    private static Task DeleteWhenFailedAsync<T>(
        Result<T> result,
        bool wroteNewFile,
        IVfsSession session,
        string path)
        => result.Match(
            _ => wroteNewFile
                ? TryDeleteAsync(session, path)
                : Task.CompletedTask,
            _ => Task.CompletedTask);

    async Task<AIKernel.Dtos.History.HistoryRomMetadata>
        AIKernel.Abstractions.History.IHistoryRomStore.SaveMarkdownAsRomAsync(
            IVfsSession session,
            string @namespace,
            string name,
            string markdown,
            DateTimeOffset createdAtUtc,
            CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var result = await SaveMarkdownAsRomAsync(
                session,
                @namespace,
                name,
                markdown,
                createdAtUtc)
            .ConfigureAwait(false);

        return result.Match(
            error => throw new InvalidOperationException(error.Message),
            HistoryRomContractMapper.ToContract);
    }
    /// <summary>
    /// EN: Gets LoadHistoryRomAsync.
    /// EN: Documentation for public API. JA: LoadHistoryRomAsync を取得します。
    /// </summary>

    public async Task<Result<HistoryRomMetadata>> LoadHistoryRomAsync(
        IVfsSession session,
        string @namespace,
        string name,
        DateTimeOffset createdAtUtc,
        string? expectedRomHash = null)
    {
        if (session is null)
        {
            return Result<HistoryRomMetadata>.Fail(
                HistoryRomErrors.Error("VFS session is required."));
        }

        return await HistoryRomPath.Create(@namespace, name)
            .Match(
                error => Task.FromResult(Result<HistoryRomMetadata>.Fail(error)),
                path => LoadHistoryRomAtPathAsync(
                    session,
                    @namespace,
                    name,
                    createdAtUtc,
                    expectedRomHash,
                    path))
            .ConfigureAwait(false);
    }

    private async Task<Result<HistoryRomMetadata>> LoadHistoryRomAtPathAsync(
        IVfsSession session,
        string @namespace,
        string name,
        DateTimeOffset createdAtUtc,
        string? expectedRomHash,
        string path)
        => (await Try.RunAsync(async () =>
        {
            var markdown = await ReadMarkdownAsync(session, path)
                .ConfigureAwait(false);

            var rom = await _loader.LoadAsync(session, path)
                .ConfigureAwait(false);

            var snapshot = _provider.CreateSnapshot(
                @namespace,
                name,
                markdown,
                createdAtUtc,
                rom,
                expectedRomHash);

            return snapshot.Bind(_registry.Register);
        }).ConfigureAwait(false)).Match(FailClosedHistoryRom<HistoryRomMetadata>, value => value);

    async Task<AIKernel.Dtos.History.HistoryRomMetadata>
        AIKernel.Abstractions.History.IHistoryRomStore.LoadHistoryRomAsync(
            IVfsSession session,
            string @namespace,
            string name,
            DateTimeOffset createdAtUtc,
            string? expectedRomHash,
            CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var result = await LoadHistoryRomAsync(
                session,
                @namespace,
                name,
                createdAtUtc,
                expectedRomHash)
            .ConfigureAwait(false);

        return result.Match(
            error => throw new InvalidOperationException(error.Message),
            HistoryRomContractMapper.ToContract);
    }

    private static async Task<string> ReadMarkdownAsync(
        IVfsSession session,
        string path)
    {
        var file = await session.ReadFileAsync(path).ConfigureAwait(false);
        var content = await file.ReadAsync().ConfigureAwait(false);
        return Encoding.UTF8.GetString(content);
    }

    private static async Task TryDeleteAsync(
        IVfsSession session,
        string path)
    {
        await Try.RunAsync(async () =>
        {
            if (await session.ExistsAsync(path).ConfigureAwait(false))
            {
                await session.DeleteAsync(path).ConfigureAwait(false);
            }

            return true;
        }).ConfigureAwait(false);
    }

    private static Result<T> FailClosedHistoryRom<T>(ErrorContext error)
        => Result<T>.Fail(error with
        {
            FailureKind = FailureKind.FailClosed,
            OriginStep = OriginStep.KernelFacade,
            SemanticSlot = SemanticSlot.C
        });
}
