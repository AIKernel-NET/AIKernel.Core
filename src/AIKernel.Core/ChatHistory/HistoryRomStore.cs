namespace AIKernel.Core.ChatHistory;

using System.Text;
using AIKernel.Abstractions.Rom;
using AIKernel.Common.Results;
using AIKernel.Vfs;

public sealed class HistoryRomStore
{
    private readonly HistoryRomProvider _provider;
    private readonly IHistoryRomRegistry _registry;
    private readonly IRomLoader _loader;

    public HistoryRomStore(
        HistoryRomProvider provider,
        IHistoryRomRegistry registry,
        IRomLoader loader)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _loader = loader ?? throw new ArgumentNullException(nameof(loader));
    }

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
        var romId = HistoryRomPath.CreateRomId(@namespace, name);
        if (romId.IsFailure)
        {
            return Result<HistoryRomMetadata>.Fail(romId.Error!);
        }

        var markdown = ChatHistoryRomExporter.ToRomMarkdown(
            records,
            new ChatHistoryRomOptions(
                romId.Value!,
                generatedAtUtc,
                entityType,
                version,
                securityTags));

        if (markdown.IsFailure)
        {
            return Result<HistoryRomMetadata>.Fail(markdown.Error!);
        }

        return await SaveMarkdownAsRomAsync(
                session,
                @namespace,
                name,
                markdown.Value!,
                generatedAtUtc)
            .ConfigureAwait(false);
    }

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

        var path = HistoryRomPath.Create(@namespace, name);
        if (path.IsFailure)
        {
            return Result<HistoryRomMetadata>.Fail(path.Error!);
        }

        try
        {
            if (await session.ExistsAsync(path.Value!).ConfigureAwait(false))
            {
                var existing = await ReadMarkdownAsync(session, path.Value!)
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
                        path.Value!,
                        Encoding.UTF8.GetBytes(markdown))
                    .ConfigureAwait(false);
            }

            return await LoadHistoryRomAsync(
                    session,
                    @namespace,
                    name,
                    createdAtUtc)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return Result<HistoryRomMetadata>.Fail(ErrorContext.FromException(ex) with
            {
                FailureKind = FailureKind.FailClosed,
                OriginStep = OriginStep.KernelFacade,
                SemanticSlot = SemanticSlot.C
            });
        }
    }

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

        var path = HistoryRomPath.Create(@namespace, name);
        if (path.IsFailure)
        {
            return Result<HistoryRomMetadata>.Fail(path.Error!);
        }

        try
        {
            var markdown = await ReadMarkdownAsync(session, path.Value!)
                .ConfigureAwait(false);

            var rom = await _loader.LoadAsync(session, path.Value!)
                .ConfigureAwait(false);

            var snapshot = _provider.CreateSnapshot(
                @namespace,
                name,
                markdown,
                createdAtUtc,
                rom,
                expectedRomHash);

            if (snapshot.IsFailure)
            {
                return Result<HistoryRomMetadata>.Fail(snapshot.Error!);
            }

            return _registry.Register(snapshot.Value!);
        }
        catch (Exception ex)
        {
            return Result<HistoryRomMetadata>.Fail(ErrorContext.FromException(ex) with
            {
                FailureKind = FailureKind.FailClosed,
                OriginStep = OriginStep.KernelFacade,
                SemanticSlot = SemanticSlot.C
            });
        }
    }

    private static async Task<string> ReadMarkdownAsync(
        IVfsSession session,
        string path)
    {
        var file = await session.ReadFileAsync(path).ConfigureAwait(false);
        var content = await file.ReadAsync().ConfigureAwait(false);
        return Encoding.UTF8.GetString(content);
    }
}
