namespace AIKernel.Core.Providers.MinimalRuntimeProvider;

using AIKernel.Abstractions.Capabilities;
using AIKernel.Abstractions.Providers;
using AIKernel.Core.Providers;
using AIKernel.Dtos.Core;

/// <summary>
/// [EN] Core standard provider for the minimal AIKernel capability execution environment.
/// [JA] AIKernel の最小 capability 実行環境を担う Core 標準 provider です。
/// </summary>
public sealed class MinimalRuntimeProvider : IProvider
{
    /// <summary>
    /// [EN] Stable provider identifier for the minimal runtime boot layer.
    /// [JA] minimal runtime boot layer 用の安定 provider identifier です。
    /// </summary>
    public const string ProviderIdValue = "aikernel.runtime.minimal";

    private static readonly MinimalRuntimeCapabilityDescriptor Descriptor =
        MinimalRuntimeCapabilityDescriptor.Standard();
    private static readonly IProviderCapabilities Capabilities =
        new StandardProviderCapabilities(
            Descriptor.ProvidedOperations,
            ["runtime", "capability"]);
    private readonly ICapabilityModuleRegistry? _capabilityModuleRegistry;

    /// <summary>
    /// [EN] Creates a minimal runtime provider.
    /// [JA] minimal runtime provider を作成します。
    /// </summary>
    public MinimalRuntimeProvider(
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
    public string Name => "Minimal Runtime Provider";

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
                .RegisterAsync(MinimalRuntimeCapabilityContracts.ToContract(Descriptor))
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
