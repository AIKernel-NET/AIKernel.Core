namespace AIKernel.Core.Providers.VfsProvider;

using AIKernel.Abstractions.Capabilities;
using AIKernel.Abstractions.Providers;
using AIKernel.Core.Providers;
using AIKernel.Dtos.Core;

/// <summary>
/// [EN] Core standard provider that represents the abstract VFS boundary used by ROM, Skills, manifests, and replay.
/// [JA] ROM、Skill、manifest、replay が依存する抽象 VFS 境界を表す Core 標準 provider です。
/// </summary>
public sealed class VfsProvider : IProvider
{
    /// <summary>
    /// [EN] Stable provider identifier for the abstract VFS provider.
    /// [JA] 抽象 VFS provider 用の安定 provider identifier です。
    /// </summary>
    public const string ProviderIdValue = "aikernel.vfs";

    private static readonly VfsCapabilityDescriptor Descriptor =
        VfsCapabilityDescriptor.Standard();
    private static readonly IProviderCapabilities Capabilities =
        new StandardProviderCapabilities(
            Descriptor.ProvidedOperations,
            ["vfs", "rom", "skill", "provider-manifest", "replay"]);
    private readonly ICapabilityModuleRegistry? _capabilityModuleRegistry;

    /// <summary>
    /// [EN] Creates a VFS provider.
    /// [JA] VFS provider を作成します。
    /// </summary>
    public VfsProvider(
        ICapabilityModuleRegistry? capabilityModuleRegistry = null)
    {
        _capabilityModuleRegistry = capabilityModuleRegistry;
    }

    /// <summary>
    /// [EN] Executes the ProviderId operation as part of the AIKernel public reference surface.
    /// [JA] AIKernel の公開参照サーフェスとして ProviderId 操作を実行します。
    /// </summary>
    public string ProviderId => ProviderIdValue;

    /// <summary>
    /// [EN] Executes the Name operation as part of the AIKernel public reference surface.
    /// [JA] AIKernel の公開参照サーフェスとして Name 操作を実行します。
    /// </summary>
    public string Name => "Virtual File System Provider";

    /// <summary>
    /// [EN] Executes the Version operation as part of the AIKernel public reference surface.
    /// [JA] AIKernel の公開参照サーフェスとして Version 操作を実行します。
    /// </summary>
    public string Version => "1.0.0";

    /// <summary>
    /// [EN] Executes the GetCapabilities operation as part of the AIKernel public reference surface.
    /// [JA] AIKernel の公開参照サーフェスとして GetCapabilities 操作を実行します。
    /// </summary>
    public IProviderCapabilities GetCapabilities()
    {
        return Capabilities;
    }

    /// <summary>
    /// [EN] Executes the IsAvailableAsync operation as part of the AIKernel public reference surface.
    /// [JA] AIKernel の公開参照サーフェスとして IsAvailableAsync 操作を実行します。
    /// </summary>
    public Task<bool> IsAvailableAsync()
    {
        return Task.FromResult(true);
    }

    /// <summary>
    /// [EN] Executes the InitializeAsync operation as part of the AIKernel public reference surface.
    /// [JA] AIKernel の公開参照サーフェスとして InitializeAsync 操作を実行します。
    /// </summary>
    public Task InitializeAsync()
    {
        return _capabilityModuleRegistry is null
            ? Task.CompletedTask
            : _capabilityModuleRegistry
                .RegisterAsync(VfsCapabilityContracts.ToContract(Descriptor))
                .AsTask();
    }

    /// <summary>
    /// [EN] Executes the ShutdownAsync operation as part of the AIKernel public reference surface.
    /// [JA] AIKernel の公開参照サーフェスとして ShutdownAsync 操作を実行します。
    /// </summary>
    public Task ShutdownAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// [EN] Executes the GetHealthAsync operation as part of the AIKernel public reference surface.
    /// [JA] AIKernel の公開参照サーフェスとして GetHealthAsync 操作を実行します。
    /// </summary>
    public Task<ProviderHealthStatus> GetHealthAsync()
    {
        return Task.FromResult(new ProviderHealthStatus(
            IsHealthy: true,
            Message: "Healthy",
            CheckedAt: DateTime.UnixEpoch,
            ResponseTimeMs: 0));
    }
}
