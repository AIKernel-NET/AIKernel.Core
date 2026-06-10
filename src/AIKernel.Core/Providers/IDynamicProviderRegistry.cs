namespace AIKernel.Core.Providers;

using AIKernel.Abstractions.Capabilities;
using AIKernel.Abstractions.Providers;

/// <summary>
/// [EN] Core-owned extension surface for dynamic provider and manifest loading.
/// [JA] dynamic provider / manifest loading のための Core 所有拡張 surface です。
/// </summary>
public interface IDynamicProviderRegistry : IProviderRegistry
{
    /// <summary>
    /// [EN] Registers a provider using its own provider identity.
    /// [JA] provider 自身の identity を使って provider を登録します。
    /// </summary>
    void RegisterProvider(IProvider provider);

    /// <summary>
    /// [EN] Registers a capability module invoker for dynamic invocation.
    /// [JA] dynamic invocation のための capability module invoker を登録します。
    /// </summary>
    void RegisterInvoker(ICapabilityModuleInvoker invoker);

    /// <summary>
    /// [EN] Returns dynamically registered invokers in deterministic order.
    /// [JA] dynamic 登録された invoker を決定論的順序で返します。
    /// </summary>
    IReadOnlyList<ICapabilityModuleInvoker> GetRegisteredInvokers();

    /// <summary>
    /// [EN] Loads providers and invokers from a managed assembly path.
    /// [JA] managed assembly path から providers と invokers を読み込みます。
    /// </summary>
    IReadOnlyList<IProvider> LoadProviderFromAssembly(string path);

    /// <summary>
    /// [EN] Loads provider metadata, capabilities, and optional assembly from a manifest.
    /// [JA] manifest から provider metadata、capabilities、任意の assembly を読み込みます。
    /// </summary>
    ValueTask<IReadOnlyList<IProvider>> LoadProviderFromManifest(
        string path,
        CancellationToken cancellationToken = default);
}
