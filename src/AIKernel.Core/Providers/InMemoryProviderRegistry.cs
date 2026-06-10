namespace AIKernel.Core.Providers;

using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using AIKernel.Abstractions.Capabilities;
using AIKernel.Abstractions.Providers;
using AIKernel.Common.Results;
using AIKernel.Dtos.Capabilities;
using AIKernel.Enums;
using Microsoft.Extensions.DependencyInjection;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Providers.InMemoryProviderRegistry']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Providers.InMemoryProviderRegistry']/summary" />
public sealed class InMemoryProviderRegistry : IDynamicProviderRegistry
{
    private readonly ConcurrentDictionary<string, IProvider> _providers =
        new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ICapabilityModuleInvoker> _invokers =
        new(StringComparer.Ordinal);
    private static readonly JsonSerializerOptions ManifestJsonOptions =
        new(JsonSerializerDefaults.Web);
    private readonly ICapabilityModuleRegistry? _capabilityModuleRegistry;

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Providers.InMemoryProviderRegistry.#ctor']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Providers.InMemoryProviderRegistry.#ctor']/summary" />
    public InMemoryProviderRegistry()
    {
    }

    /// <summary>
    /// [EN] Initializes a registry with a capability module registry for dynamic provider manifests.
    /// [JA] dynamic provider manifest 用の capability module registry を持つ registry を初期化します。
    /// </summary>
    [ActivatorUtilitiesConstructor]
    public InMemoryProviderRegistry(
        ICapabilityModuleRegistry capabilityModuleRegistry)
    {
        _capabilityModuleRegistry = capabilityModuleRegistry;
    }

    /// <summary>
    /// [EN] Initializes a registry with manifest support and pre-registered providers.
    /// [JA] manifest support と pre-registered provider を持つ registry を初期化します。
    /// </summary>
    public InMemoryProviderRegistry(
        ICapabilityModuleRegistry capabilityModuleRegistry,
        IEnumerable<IProvider> providers)
        : this(capabilityModuleRegistry)
    {
        ArgumentNullException.ThrowIfNull(providers);

        foreach (var provider in providers)
        {
            RegisterProvider(provider);
        }
    }

    /// <summary>
    /// [EN] Initializes a registry with manifest support, providers, and invokers.
    /// [JA] manifest support、provider、invoker を持つ registry を初期化します。
    /// </summary>
    public InMemoryProviderRegistry(
        ICapabilityModuleRegistry capabilityModuleRegistry,
        IEnumerable<IProvider> providers,
        IEnumerable<ICapabilityModuleInvoker> invokers)
        : this(capabilityModuleRegistry, providers)
    {
        ArgumentNullException.ThrowIfNull(invokers);

        foreach (var invoker in invokers)
        {
            RegisterInvoker(invoker);
        }
    }

    /// <summary>
    /// [EN] Initializes a registry with pre-registered providers and no manifest registry.
    /// [JA] manifest registry を持たず、pre-registered provider を持つ registry を初期化します。
    /// </summary>
    public InMemoryProviderRegistry(IEnumerable<IProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        foreach (var provider in providers)
        {
            RegisterProvider(provider.ProviderId, provider);
        }
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Providers.InMemoryProviderRegistry.RegisterProvider']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Providers.InMemoryProviderRegistry.RegisterProvider']/summary" />
    public void RegisterProvider(string name, IProvider provider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(provider);

        _providers[NormalizeName(name)] = provider;
    }

    /// <summary>
    /// [EN] Registers a provider using its own ProviderId.
    /// [JA] Provider 自身の ProviderId で登録します。
    /// </summary>
    public void RegisterProvider(IProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        RegisterProvider(provider.ProviderId, provider);
    }

    /// <summary>
    /// [EN] Registers a capability module invoker.
    /// [JA] capability module invoker を登録します。
    /// </summary>
    public void RegisterInvoker(ICapabilityModuleInvoker invoker)
    {
        ArgumentNullException.ThrowIfNull(invoker);
        _invokers[invoker.GetType().FullName ?? invoker.GetType().Name] = invoker;
    }

    /// <summary>
    /// [EN] Returns registered capability module invokers in deterministic order.
    /// [JA] 登録済み capability module invoker を決定論的順序で返します。
    /// </summary>
    public IReadOnlyList<ICapabilityModuleInvoker> GetRegisteredInvokers()
        => _invokers
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => x.Value)
            .ToArray();

    /// <summary>
    /// [EN] Loads providers and invokers from an assembly path.
    /// [JA] assembly path から provider と invoker を読み込みます。
    /// </summary>
    public IReadOnlyList<IProvider> LoadProviderFromAssembly(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var assemblyPath = Path.GetFullPath(path);
        var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
        var providers = new List<IProvider>();

        foreach (var type in assembly.GetTypes().Where(IsConcrete))
        {
            if (typeof(IProvider).IsAssignableFrom(type) &&
                Activator.CreateInstance(type) is IProvider provider)
            {
                RegisterProvider(provider);
                providers.Add(provider);
            }

            if (typeof(ICapabilityModuleInvoker).IsAssignableFrom(type) &&
                Activator.CreateInstance(type) is ICapabilityModuleInvoker invoker)
            {
                RegisterInvoker(invoker);
            }
        }

        return providers
            .OrderBy(provider => provider.ProviderId, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// [EN] Loads provider metadata, capabilities, assembly, providers, and invokers from a JSON manifest.
    /// [JA] JSON manifest から provider metadata、capability、assembly、provider、invoker を読み込みます。
    /// </summary>
    public async ValueTask<IReadOnlyList<IProvider>> LoadProviderFromManifest(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        cancellationToken.ThrowIfCancellationRequested();

        var manifestPath = Path.GetFullPath(path);
        await using var stream = File.OpenRead(manifestPath);
        var manifest = await JsonSerializer.DeserializeAsync<ProviderManifest>(
            stream,
            ManifestJsonOptions,
            cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Provider manifest could not be parsed.");

        manifest.Validate();

        if (_capabilityModuleRegistry is not null)
        {
            await _capabilityModuleRegistry.RegisterAsync(
                manifest.ToCapabilityModuleDescriptor(),
                cancellationToken).ConfigureAwait(false);
        }

        if (string.IsNullOrWhiteSpace(manifest.Assembly))
        {
            return [];
        }

        var assemblyPath = Path.Combine(
            Path.GetDirectoryName(manifestPath) ?? "",
            manifest.Assembly);
        return LoadProviderFromAssembly(assemblyPath);
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Providers.InMemoryProviderRegistry.UnregisterProvider']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Providers.InMemoryProviderRegistry.UnregisterProvider']/summary" />
    public bool UnregisterProvider(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        return _providers.TryRemove(NormalizeName(name), out _);
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Providers.InMemoryProviderRegistry.GetRegisteredProviders']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Providers.InMemoryProviderRegistry.GetRegisteredProviders']/summary" />
    public IReadOnlyList<string> GetRegisteredProviders()
    {
        return _providers.Keys
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();
    }

    private static string NormalizeName(string name)
    {
        return name.Trim();
    }

    private static bool IsConcrete(Type type)
        => type is { IsAbstract: false, IsInterface: false } &&
           type.GetConstructor(Type.EmptyTypes) is not null;
}

/// <summary>
/// [EN] Describes an external provider manifest that can be converted into a capability module descriptor.
/// [JA] capability module descriptor へ変換できる外部 provider manifest を表します。
/// </summary>
/// <param name="ProviderId">
/// [EN] Stable provider identifier declared by the manifest.
/// [JA] manifest で宣言された安定した provider identifier です。
/// </param>
/// <param name="Name">
/// [EN] Human-readable provider name.
/// [JA] 人間が読める provider name です。
/// </param>
/// <param name="Version">
/// [EN] Provider version declared by the manifest.
/// [JA] manifest で宣言された provider version です。
/// </param>
/// <param name="Capabilities">
/// [EN] Capability identifiers exposed by the provider.
/// [JA] provider が公開する capability identifier です。
/// </param>
/// <param name="Metadata">
/// [EN] Provider metadata used for endpoint, model, and CLI configuration projection.
/// [JA] endpoint、model、CLI configuration projection に使う provider metadata です。
/// </param>
/// <param name="Assembly">
/// [EN] Optional relative or absolute assembly path to load after reading the manifest.
/// [JA] manifest 読み込み後に load する任意の relative/absolute assembly path です。
/// </param>
public sealed record ProviderManifest(
    string ProviderId,
    string Name,
    string Version,
    IReadOnlyList<string>? Capabilities,
    IReadOnlyDictionary<string, string>? Metadata,
    string? Assembly = null)
{
    /// <summary>
    /// [EN] Converts the manifest into the runtime capability module descriptor shape.
    /// [JA] manifest を runtime capability module descriptor の形に変換します。
    /// </summary>
    /// <returns>
    /// [EN] Capability module descriptor derived from the manifest.
    /// [JA] manifest から派生した capability module descriptor です。
    /// </returns>
    public CapabilityModuleDescriptor ToCapabilityModuleDescriptor()
        => new(
            ProviderId,
            Name,
            CapabilityModuleKind.RemoteEndpoint,
            CapabilityInvocationMode.Remote,
            Version,
            ReadMetadataOrNull("endpoint"),
            Assembly,
            null,
            CapabilitiesOrEmpty,
            ["network.egress", "llm.remote"],
            BuildMetadata());

    /// <summary>
    /// [EN] Validates required manifest fields before registration or assembly loading.
    /// [JA] registration または assembly loading の前に必須 manifest field を検証します。
    /// </summary>
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ProviderId);
        ArgumentException.ThrowIfNullOrWhiteSpace(Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(Version);

        if (CapabilitiesOrEmpty.Count == 0)
        {
            throw new InvalidOperationException("Provider manifest must declare at least one capability.");
        }
    }

    private IReadOnlyDictionary<string, string> BuildMetadata()
    {
        var metadata = new SortedDictionary<string, string>(StringComparer.Ordinal);

        foreach (var item in MetadataOrEmpty.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            metadata[item.Key] = item.Value;
        }

        if (Cli is not null)
        {
            if (!string.IsNullOrWhiteSpace(Cli.DefaultOperation))
            {
                metadata["cli.default_operation"] = Cli.DefaultOperation;
            }

            if (!string.IsNullOrWhiteSpace(Cli.Command))
            {
                metadata["cli.command"] = Cli.Command;
            }

            foreach (var key in Cli.ConfigKeysOrEmpty.OrderBy(x => x, StringComparer.Ordinal))
            {
                metadata[$"cli.config.{key}"] = key;
            }

            foreach (var variable in Cli.RequiredEnvironmentOrEmpty.OrderBy(x => x, StringComparer.Ordinal))
            {
                metadata[$"cli.env.{variable}"] = variable;
            }
        }

        return metadata;
    }

    /// <summary>
    /// [EN] Optional CLI settings declared by the provider manifest.
    /// [JA] provider manifest で宣言された任意の CLI setting です。
    /// </summary>
    public ProviderCliManifest? Cli { get; init; }

    private IReadOnlyList<string> CapabilitiesOrEmpty => Capabilities ?? [];

    private IReadOnlyDictionary<string, string> MetadataOrEmpty => Metadata
        ?? new Dictionary<string, string>(StringComparer.Ordinal);

    private Option<string> ReadMetadata(
        string key)
    {
        if (MetadataOrEmpty.TryGetValue(key, out var value) &&
            !string.IsNullOrWhiteSpace(value))
        {
            return Option<string>.Some(value);
        }

        return Option<string>.None();
    }

    private string? ReadMetadataOrNull(
        string key)
        => ReadMetadata(key)
            .Match<string?>(
                () => null,
                value => value);
}

/// <summary>
/// [EN] Describes CLI-facing settings embedded in a provider manifest.
/// [JA] provider manifest に埋め込まれた CLI 向け setting を表します。
/// </summary>
/// <param name="Command">
/// [EN] CLI command or provider alias used to install or invoke the provider.
/// [JA] provider の install または invoke に使う CLI command または provider alias です。
/// </param>
/// <param name="DefaultOperation">
/// [EN] Default operation used when the CLI does not receive an explicit operation.
/// [JA] CLI が明示的な operation を受け取らない場合に使う default operation です。
/// </param>
/// <param name="ConfigKeys">
/// [EN] Configuration keys accepted by the provider command.
/// [JA] provider command が受け付ける configuration key です。
/// </param>
/// <param name="RequiredEnvironment">
/// [EN] Environment variable names required by the provider command.
/// [JA] provider command が必要とする environment variable name です。
/// </param>
public sealed record ProviderCliManifest(
    string Command,
    string DefaultOperation,
    IReadOnlyList<string>? ConfigKeys,
    IReadOnlyList<string>? RequiredEnvironment)
{
    /// <summary>
    /// [EN] Returns configuration keys as an empty list when the manifest omits them.
    /// [JA] manifest が省略した場合に configuration key を empty list として返します。
    /// </summary>
    public IReadOnlyList<string> ConfigKeysOrEmpty => ConfigKeys ?? [];

    /// <summary>
    /// [EN] Returns required environment variables as an empty list when the manifest omits them.
    /// [JA] manifest が省略した場合に required environment variable を empty list として返します。
    /// </summary>
    public IReadOnlyList<string> RequiredEnvironmentOrEmpty => RequiredEnvironment ?? [];
}
