namespace AIKernel.Core.Providers.SystemInfoProvider;

using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AIKernel.Abstractions.Capabilities;
using AIKernel.Abstractions.Providers;
using AIKernel.Common.Results;
using AIKernel.Dtos.Capabilities;
using AIKernel.Vfs;

/// <summary>
/// [EN] Read-only invoker for safe AIKernel system introspection.
/// [JA] 安全な AIKernel system introspection を行う read-only invoker です。
/// </summary>
public sealed class SystemInfoInvoker : ICapabilityModuleInvoker
{
    private readonly IEnumerable<IProvider> _providers;
    private readonly ICapabilityModuleRegistry _capabilityRegistry;
    private readonly IEnumerable<IVfsProvider> _vfsProviders;

    /// <summary>
    /// [EN] Creates a system information invoker from Core registries.
    /// [JA] Core registry から system information invoker を作成します。
    /// </summary>
    public SystemInfoInvoker(
        IEnumerable<IProvider> providers,
        ICapabilityModuleRegistry capabilityRegistry,
        IEnumerable<IVfsProvider> vfsProviders)
    {
        _providers = providers ?? throw new ArgumentNullException(nameof(providers));
        _capabilityRegistry = capabilityRegistry ?? throw new ArgumentNullException(nameof(capabilityRegistry));
        _vfsProviders = vfsProviders ?? throw new ArgumentNullException(nameof(vfsProviders));
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
            ["provider"] = SystemInfoProvider.ProviderIdValue,
            ["operation"] = request.Operation
        };

        if (!string.Equals(request.CapabilityId, "aikernel.system.info", StringComparison.Ordinal))
        {
            return Fail(request, metadata, "SYSTEM_INFO_UNSUPPORTED_CAPABILITY",
                "SystemInfoInvoker only supports aikernel.system.info.");
        }

        var snapshot = await CreateSnapshotAsync(cancellationToken)
            .ConfigureAwait(false);
        var json = request.Operation switch
        {
            "system.info" => JsonSerializer.Serialize(new
            {
                kernelVersion = snapshot.KernelVersion,
                providerCount = snapshot.Providers.Count,
                capabilityCount = snapshot.Capabilities.Count,
                vfsMounted = snapshot.VfsInfo.MountStatus == "mounted",
                runtimeVersion = snapshot.RuntimeInfo.PipelineEngineVersion
            }, SystemInfoJsonOptions.Options),
            "system.providers" => JsonSerializer.Serialize(
                new { providers = snapshot.Providers },
                SystemInfoJsonOptions.Options),
            "system.capabilities" => JsonSerializer.Serialize(
                new { capabilities = snapshot.Capabilities },
                SystemInfoJsonOptions.Options),
            "system.vfs" => JsonSerializer.Serialize(
                new { vfs = snapshot.VfsInfo },
                SystemInfoJsonOptions.Options),
            "system.runtime" => JsonSerializer.Serialize(
                new { runtime = snapshot.RuntimeInfo },
                SystemInfoJsonOptions.Options),
            _ => null
        };

        if (json is null)
        {
            return Fail(request, metadata, "SYSTEM_INFO_UNSUPPORTED_OPERATION",
                "Unsupported system information operation.");
        }

        metadata["snapshot.json"] = json;
        metadata["provider_count"] = snapshot.Providers.Count.ToString(
            System.Globalization.CultureInfo.InvariantCulture);
        metadata["capability_count"] = snapshot.Capabilities.Count.ToString(
            System.Globalization.CultureInfo.InvariantCulture);
        metadata["vfs.mounted"] = (snapshot.VfsInfo.MountStatus == "mounted")
            .ToString()
            .ToLowerInvariant();
        metadata["runtime.version"] = snapshot.RuntimeInfo.PipelineEngineVersion;

        return new CapabilityInvocationResult(
            request.InvocationId,
            request.CapabilityId,
            Succeeded: true,
            OutputHash: ComputeHash(json),
            ErrorCode: null,
            ErrorMessage: null,
            ReplayLogHash: request.ReplayLogHash,
            Metadata: metadata
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal));
    }

    private async ValueTask<SystemInfoSnapshot> CreateSnapshotAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var providers = _providers
            .Select(provider => new SystemProviderInfo(
                provider.ProviderId,
                provider.Name,
                provider.Version))
            .OrderBy(x => x.ProviderId, StringComparer.Ordinal)
            .ToArray();

        var capabilities = (await _capabilityRegistry.ListAsync(cancellationToken)
                .ConfigureAwait(false))
            .Select(capability => new SystemCapabilityInfo(
                capability.CapabilityId,
                capability.ProvidedOperations
                    .OrderBy(x => x, StringComparer.Ordinal)
                    .ToArray(),
                ReadProviderIdOrNull(capability.Metadata)))
            .OrderBy(x => x.CapabilityId, StringComparer.Ordinal)
            .ToArray();

        var vfsProviders = _vfsProviders
            .OrderBy(x => x.ProviderId, StringComparer.Ordinal)
            .ToArray();
        var vfsInfo = new SystemVfsInfo(
            VfsBackendType(vfsProviders),
            RootPath: null,
            VfsMountStatus(vfsProviders));

        var assemblyVersion = typeof(SystemInfoProvider).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?? typeof(SystemInfoProvider).Assembly.GetName().Version?.ToString()
            ?? "unknown";

        return new SystemInfoSnapshot(
            assemblyVersion,
            providers,
            capabilities,
            vfsInfo,
            new SystemRuntimeInfo(
                DslVersion: "1.0.0",
                PipelineEngineVersion: assemblyVersion));
    }

    private static string VfsBackendType(IReadOnlyCollection<IVfsProvider> vfsProviders)
        => VfsProviders(vfsProviders).Match(
            () => "none",
            providers => string.Join(",", providers.Select(x => x.GetType().Name)));

    private static string VfsMountStatus(IReadOnlyCollection<IVfsProvider> vfsProviders)
        => VfsProviders(vfsProviders).Match(() => "unmounted", _ => "mounted");

    private static Option<IReadOnlyCollection<IVfsProvider>> VfsProviders(
        IReadOnlyCollection<IVfsProvider> vfsProviders)
        => vfsProviders.Count == 0
            ? Option<IReadOnlyCollection<IVfsProvider>>.None()
            : Option<IReadOnlyCollection<IVfsProvider>>.Some(vfsProviders);

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

    private static string ComputeHash(
        string json)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)))
            .ToLowerInvariant();
    }

    private static Option<string> ReadProviderId(
        IReadOnlyDictionary<string, string> metadata)
    {
        if (metadata.TryGetValue("provider", out var providerId) &&
            !string.IsNullOrWhiteSpace(providerId))
        {
            return Option<string>.Some(providerId);
        }

        return Option<string>.None();
    }

    private static string? ReadProviderIdOrNull(
        IReadOnlyDictionary<string, string> metadata)
        => ReadProviderId(metadata)
            .Match<string?>(
                () => null,
                value => value);

    private static class SystemInfoJsonOptions
    {
        /// <summary>
        /// [EN] Deterministic JSON serializer options for system information snapshots.
        /// [JA] system information snapshot 用の決定論的 JSON serializer options です。
        /// </summary>
        public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = false
        };
    }
}
