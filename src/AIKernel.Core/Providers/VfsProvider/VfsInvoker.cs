namespace AIKernel.Core.Providers.VfsProvider;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AIKernel.Abstractions.Capabilities;
using AIKernel.Common.Results;
using AIKernel.Dtos.Capabilities;
using AIKernel.Dtos.Vfs;
using AIKernel.Enums;
using AIKernel.Vfs;

/// <summary>
/// [EN] Read-only VFS capability invoker that delegates to injected Core VFS providers.
/// [JA] DI された Core VFS provider へ委譲する read-only VFS capability invoker です。
/// </summary>
public sealed class VfsInvoker : ICapabilityModuleInvoker
{
    private readonly IReadOnlyList<IVfsProvider> _providers;

    /// <summary>
    /// [EN] Creates a VFS invoker from injected VFS providers.
    /// [JA] DI された VFS provider から VFS invoker を作成します。
    /// </summary>
    public VfsInvoker(
        IEnumerable<IVfsProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);
        _providers = providers
            .OrderBy(x => x.ProviderId, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// [EN] Executes the InvokeAsync operation as part of the AIKernel public reference surface.
    /// [JA] AIKernel の公開参照サーフェスとして InvokeAsync 操作を実行します。
    /// </summary>
    public async ValueTask<CapabilityInvocationResult> InvokeAsync(
        CapabilityInvocationRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);

        var metadata = new Dictionary<string, string>(
            request.Metadata,
            StringComparer.Ordinal)
        {
            ["provider"] = VfsProvider.ProviderIdValue,
            ["operation"] = request.Operation
        };

        return await UnsupportedCapability(request)
            .Match(
                () => ResolveProvider(request)
                    .Match(
                        () => Task.FromResult(Fail(
                            request,
                            metadata,
                            "VFS_PROVIDER_NOT_CONFIGURED",
                            "No injected IVfsProvider is available for VFS invocation.")),
                        provider => RequirePath(request.Arguments)
                            .Match(
                                error => Task.FromResult(Fail(
                                    request,
                                    metadata,
                                    "VFS_PATH_REQUIRED",
                                    error)),
                                path => InvokeResolvedAsync(request, metadata, provider, path))),
                error => Task.FromResult(Fail(request, metadata, error.Code, error.Message)))
            .ConfigureAwait(false);
    }

    private static async Task<CapabilityInvocationResult> InvokeResolvedAsync(
        CapabilityInvocationRequest request,
        Dictionary<string, string> metadata,
        IVfsProvider resolvedProvider,
        string resolvedPath)
    {
        metadata["vfs.provider_id"] = resolvedProvider.ProviderId;
        metadata["path"] = resolvedPath;

        return (await Try.RunAsync(async () =>
        {
            await using var session = await resolvedProvider.OpenSessionAsync(CreateCredentials(request))
                .ConfigureAwait(false);

            return request.Operation switch
            {
                "vfs.read_file" => await ReadFileAsync(request, metadata, session, resolvedPath)
                    .ConfigureAwait(false),
                "vfs.list" => await ListAsync(request, metadata, session, resolvedPath)
                    .ConfigureAwait(false),
                "vfs.exists" => await ExistsAsync(request, metadata, session, resolvedPath)
                    .ConfigureAwait(false),
                "vfs.metadata" => await MetadataAsync(request, metadata, session, resolvedPath)
                    .ConfigureAwait(false),
                _ => Fail(request, metadata, "VFS_UNSUPPORTED_OPERATION",
                    "Unsupported VFS read operation.")
            };
        }).ConfigureAwait(false)).Match(
            error => Fail(request, metadata, "VFS_READ_FAILED", error.Message),
            result => result);
    }

    private Option<IVfsProvider> ResolveProvider(
        CapabilityInvocationRequest request)
        => ReadValue(request.Arguments, "vfs.provider_id")
            .OrElseOption(ReadValue(request.Metadata, "vfs.provider_id"))
            .Match(
                FirstProvider,
                FindProvider);

    private Option<IVfsProvider> FindProvider(
        string providerId)
    {
        var provider = _providers.FirstOrDefault(
            item => string.Equals(item.ProviderId, providerId, StringComparison.Ordinal));
        return MonadicDecision.Optional(provider);
    }

    private Option<IVfsProvider> FirstProvider()
    {
        var provider = _providers.FirstOrDefault();
        return MonadicDecision.Optional(provider);
    }

    private static async Task<CapabilityInvocationResult> ReadFileAsync(
        CapabilityInvocationRequest request,
        Dictionary<string, string> metadata,
        IVfsSession session,
        string path)
    {
        var exists = await session.ExistsAsync(path).ConfigureAwait(false);
        metadata["exists"] = exists.ToString().ToLowerInvariant();

        return await MissingPath(exists).Match(
            () => ReadExistingFileAsync(request, metadata, session, path),
            _ => Task.FromResult(Success(request, metadata, "vfs.read_file:missing")))
            .ConfigureAwait(false);
    }

    private static async Task<CapabilityInvocationResult> ListAsync(
        CapabilityInvocationRequest request,
        Dictionary<string, string> metadata,
        IVfsSession session,
        string path)
    {
        var directory = await session.GetDirectoryAsync(path).ConfigureAwait(false);
        var entries = await directory.GetEntriesAsync().ConfigureAwait(false);
        var output = entries
            .OrderBy(x => x.Path, StringComparer.Ordinal)
            .Select(x => new VfsEntryOutput(
                x.Name,
                x.Type.ToString().ToLowerInvariant(),
                x.Path))
            .ToArray();

        metadata["entries.json"] = JsonSerializer.Serialize(output);
        metadata["entry_count"] = output.Length.ToString(System.Globalization.CultureInfo.InvariantCulture);
        metadata["type"] = "directory";

        return Success(request, metadata, "vfs.list");
    }

    private static async Task<CapabilityInvocationResult> ExistsAsync(
        CapabilityInvocationRequest request,
        Dictionary<string, string> metadata,
        IVfsSession session,
        string path)
    {
        var exists = await session.ExistsAsync(path).ConfigureAwait(false);
        metadata["exists"] = exists.ToString().ToLowerInvariant();
        return Success(request, metadata, "vfs.exists");
    }

    private static async Task<CapabilityInvocationResult> ReadExistingFileAsync(
        CapabilityInvocationRequest request,
        Dictionary<string, string> metadata,
        IVfsSession session,
        string path)
    {
        var file = await session.ReadFileAsync(path).ConfigureAwait(false);
        metadata["content"] = await file.ReadAsTextAsync().ConfigureAwait(false);
        metadata["size"] = file.Size.ToString(System.Globalization.CultureInfo.InvariantCulture);
        metadata["type"] = "file";
        metadata["created"] = file.CreatedAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
        metadata["modified"] = file.ModifiedAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture);

        return Success(request, metadata, "vfs.read_file");
    }

    private static async Task<CapabilityInvocationResult> MetadataAsync(
        CapabilityInvocationRequest request,
        Dictionary<string, string> metadata,
        IVfsSession session,
        string path)
    {
        var exists = await session.ExistsAsync(path).ConfigureAwait(false);
        if (MissingPath(exists).Match(() => false, _ => true))
        {
            metadata["exists"] = "false";
            return Success(request, metadata, "vfs.metadata:missing");
        }

        var fileResult = await Try
            .RunAsync(async () => await session.ReadFileAsync(path).ConfigureAwait(false))
            .ConfigureAwait(false);

        return await fileResult.Match(
            error => IsFileNotFound(error)
                ? ReadDirectoryMetadataAsync(request, metadata, session, path)
                : Task.FromResult(Fail(
                    request,
                    metadata,
                    "VFS_METADATA_FAILED",
                    error.Message)),
            file =>
            {
                metadata["exists"] = "true";
                metadata["type"] = "file";
                metadata["size"] = file.Size.ToString(System.Globalization.CultureInfo.InvariantCulture);
                metadata["created"] = file.CreatedAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
                metadata["modified"] = file.ModifiedAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
                return Task.FromResult(Success(request, metadata, "vfs.metadata:file"));
            }).ConfigureAwait(false);
    }

    private static bool IsFileNotFound(ErrorContext error)
        => error.Metadata is not null &&
           error.Metadata.TryGetValue(ResultMetadataKeys.ExceptionType, out var exceptionType) &&
           string.Equals(exceptionType, typeof(FileNotFoundException).FullName, StringComparison.Ordinal);

    private static Option<MonadicError> UnsupportedCapability(
        CapabilityInvocationRequest request)
        => MonadicDecision.ErrorUnless(
            string.Equals(request.CapabilityId, "aikernel.vfs", StringComparison.Ordinal),
            "VFS_UNSUPPORTED_CAPABILITY",
            "VfsInvoker only supports aikernel.vfs.");

    private static Option<string> MissingPath(bool exists)
        => exists
            ? Option<string>.None()
            : Option<string>.Some("missing");

    private static async Task<CapabilityInvocationResult> ReadDirectoryMetadataAsync(
        CapabilityInvocationRequest request,
        Dictionary<string, string> metadata,
        IVfsSession session,
        string path)
    {
        var directory = await session.GetDirectoryAsync(path).ConfigureAwait(false);
        metadata["exists"] = "true";
        metadata["type"] = "directory";
        metadata["size"] = "0";
        foreach (var item in directory.GetMetadata() ?? new Dictionary<string, string>(StringComparer.Ordinal))
        {
            metadata[$"vfs.metadata.{item.Key}"] = item.Value;
        }

        return Success(request, metadata, "vfs.metadata:directory");
    }

    private static IVfsCredentials CreateCredentials(
        CapabilityInvocationRequest request)
    {
        var parameters = request.Arguments
            .Where(x => x.Key.StartsWith("credential.", StringComparison.Ordinal))
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .ToDictionary(
                x => x.Key["credential.".Length..],
                x => x.Value,
                StringComparer.Ordinal);

        var objectParameters = parameters
            .ToDictionary(
                x => x.Key,
                x => (object)x.Value,
                StringComparer.Ordinal);

        return new InvocationVfsCredentials(
            ReadValue(parameters, "username").ToNullable(),
            ReadValue(parameters, "api_key").ToNullable(),
            ReadValue(parameters, "token").ToNullable(),
            ReadParameters(objectParameters).ToNullable());
    }

    private static Either<string, string> RequirePath(
        IReadOnlyDictionary<string, string> arguments)
        => ReadValue(arguments, "path")
            .Match(
                () => Either<string, string>.FromLeft("path argument is required."),
                Either<string, string>.FromRight);

    private static Option<string> ReadValue(
        IReadOnlyDictionary<string, string> source,
        string key)
    {
        if (source.TryGetValue(key, out var value) &&
            !string.IsNullOrWhiteSpace(value))
        {
            return Option<string>.Some(value);
        }

        return Option<string>.None();
    }

    private static Option<IReadOnlyDictionary<string, object>> ReadParameters(
        IReadOnlyDictionary<string, object> parameters)
    {
        if (parameters.Count > 0)
        {
            return Option<IReadOnlyDictionary<string, object>>.Some(parameters);
        }

        return Option<IReadOnlyDictionary<string, object>>.None();
    }

    private static CapabilityInvocationResult Success(
        CapabilityInvocationRequest request,
        IReadOnlyDictionary<string, string> metadata,
        string hashSeed)
    {
        var payload = string.Join(
            "\n",
            hashSeed,
            string.Join("\n", metadata
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .Select(x => $"{x.Key}={x.Value}")));

        return new CapabilityInvocationResult(
            request.InvocationId,
            request.CapabilityId,
            Succeeded: true,
            OutputHash: Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)))
                .ToLowerInvariant(),
            ErrorCode: null,
            ErrorMessage: null,
            ReplayLogHash: request.ReplayLogHash,
            Metadata: metadata
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal));
    }

    private static CapabilityInvocationResult Fail(
        CapabilityInvocationRequest request,
        IReadOnlyDictionary<string, string> metadata,
        string code,
        string message)
    {
        return new CapabilityInvocationResult(
            request.InvocationId,
            request.CapabilityId,
            Succeeded: false,
            OutputHash: null,
            ErrorCode: code,
            ErrorMessage: message,
            ReplayLogHash: request.ReplayLogHash,
            Metadata: metadata);
    }

    private sealed record VfsEntryOutput(
        string Name,
        string Type,
        string Path);

    private sealed record InvocationVfsCredentials(
        string? Username,
        string? ApiKey,
        string? Token,
        IReadOnlyDictionary<string, object>? Parameters) : IVfsCredentials;
}

internal static class VfsInvokerOptionExtensions
{
    public static Option<T> OrElseOption<T>(
        this Option<T> option,
        Option<T> fallback)
        => option.Match(
            () => fallback,
            Option<T>.Some);

    public static T? ToNullable<T>(
        this Option<T> option)
        where T : class
        => option.Match<T?>(
            () => null,
            value => value);
}
