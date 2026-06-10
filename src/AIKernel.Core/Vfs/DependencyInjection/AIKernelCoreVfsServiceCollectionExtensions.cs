namespace AIKernel.Core.Vfs.DependencyInjection;

using AIKernel.Core.Time;
using AIKernel.Core.Vfs.Local;
using AIKernel.Core.Vfs.Memory;
using AIKernel.Core.Vfs.Web;
using AIKernel.Vfs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Vfs.DependencyInjection.AIKernelCoreVfsServiceCollectionExtensions']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Vfs.DependencyInjection.AIKernelCoreVfsServiceCollectionExtensions']/summary" />
public static class AIKernelCoreVfsServiceCollectionExtensions
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.DependencyInjection.AIKernelCoreVfsServiceCollectionExtensions.AddAIKernelCoreVfsProviders']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.DependencyInjection.AIKernelCoreVfsServiceCollectionExtensions.AddAIKernelCoreVfsProviders']/summary" />
    public static IServiceCollection AddAIKernelCoreVfsProviders(
        this IServiceCollection services,
        Action<MemoryFileProviderOptions>? memory,
        Action<LocalFileProviderOptions> local,
        Action<WebGetFileProviderOptions> webGet)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(local);
        ArgumentNullException.ThrowIfNull(webGet);

        return services
            .AddMemoryFileProvider(memory)
            .AddLocalFileProvider(local)
            .AddWebGetFileProvider(webGet);
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.DependencyInjection.AIKernelCoreVfsServiceCollectionExtensions.AddAIKernelCoreVfsProviders']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.DependencyInjection.AIKernelCoreVfsServiceCollectionExtensions.AddAIKernelCoreVfsProviders']/summary" />
    public static IServiceCollection AddAIKernelCoreVfsProviders(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = "AIKernel:Vfs")
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(sectionName);

        return services
            .AddMemoryFileProvider(section.GetSection("MemoryFile"))
            .AddLocalFileProvider(section.GetSection("LocalFile"))
            .AddWebGetFileProvider(section.GetSection("WebGetFile"));
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.DependencyInjection.AIKernelCoreVfsServiceCollectionExtensions.AddAIKernelBrowserVfsProviders']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.DependencyInjection.AIKernelCoreVfsServiceCollectionExtensions.AddAIKernelBrowserVfsProviders']/summary" />
    public static IServiceCollection AddAIKernelBrowserVfsProviders(
        this IServiceCollection services,
        Action<MemoryFileProviderOptions>? memory = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddMemoryFileProvider(memory);
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.DependencyInjection.AIKernelCoreVfsServiceCollectionExtensions.AddAIKernelBrowserVfsProviders']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.DependencyInjection.AIKernelCoreVfsServiceCollectionExtensions.AddAIKernelBrowserVfsProviders']/summary" />
    public static IServiceCollection AddAIKernelBrowserVfsProviders(
        this IServiceCollection services,
        Action<WebGetFileProviderOptions> webGet,
        Action<MemoryFileProviderOptions>? memory = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(webGet);

        return services
            .AddMemoryFileProvider(memory)
            .AddWebGetFileProvider(webGet);
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.DependencyInjection.AIKernelCoreVfsServiceCollectionExtensions.AddMemoryFileProvider']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.DependencyInjection.AIKernelCoreVfsServiceCollectionExtensions.AddMemoryFileProvider']/summary" />
    public static IServiceCollection AddMemoryFileProvider(
        this IServiceCollection services,
        Action<MemoryFileProviderOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ThrowIfProviderAlreadyRegistered<MemoryFileProvider>(services);

        services.AddKernelClockDefaults();
        services.AddMemoryOptions(configure);
        services.AddSingleton<MemoryFileProvider>(CreateMemoryFileProvider);
        services.AddSingleton<IVfsProvider>(serviceProvider =>
            serviceProvider.GetRequiredService<MemoryFileProvider>());

        return services;
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.DependencyInjection.AIKernelCoreVfsServiceCollectionExtensions.AddMemoryFileProvider']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.DependencyInjection.AIKernelCoreVfsServiceCollectionExtensions.AddMemoryFileProvider']/summary" />
    public static IServiceCollection AddMemoryFileProvider(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ThrowIfProviderAlreadyRegistered<MemoryFileProvider>(services);

        services.AddKernelClockDefaults();
        services.AddMemoryOptions(configuration);
        services.AddSingleton<MemoryFileProvider>(CreateMemoryFileProvider);
        services.AddSingleton<IVfsProvider>(serviceProvider =>
            serviceProvider.GetRequiredService<MemoryFileProvider>());

        return services;
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.DependencyInjection.AIKernelCoreVfsServiceCollectionExtensions.AddLocalFileProvider']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.DependencyInjection.AIKernelCoreVfsServiceCollectionExtensions.AddLocalFileProvider']/summary" />
    public static IServiceCollection AddLocalFileProvider(
        this IServiceCollection services,
        Action<LocalFileProviderOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        ThrowIfProviderAlreadyRegistered<LocalFileProvider>(services);

        services.AddKernelClockDefaults();
        services.AddLocalOptions(configure);
        services.AddSingleton<LocalFileProvider>();
        services.AddSingleton<IVfsProvider>(serviceProvider =>
            serviceProvider.GetRequiredService<LocalFileProvider>());

        return services;
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.DependencyInjection.AIKernelCoreVfsServiceCollectionExtensions.AddLocalFileProvider']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.DependencyInjection.AIKernelCoreVfsServiceCollectionExtensions.AddLocalFileProvider']/summary" />
    public static IServiceCollection AddLocalFileProvider(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ThrowIfProviderAlreadyRegistered<LocalFileProvider>(services);

        services.AddKernelClockDefaults();
        services.AddLocalOptions(configuration);
        services.AddSingleton<LocalFileProvider>();
        services.AddSingleton<IVfsProvider>(serviceProvider =>
            serviceProvider.GetRequiredService<LocalFileProvider>());

        return services;
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.DependencyInjection.AIKernelCoreVfsServiceCollectionExtensions.AddWebGetFileProvider']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.DependencyInjection.AIKernelCoreVfsServiceCollectionExtensions.AddWebGetFileProvider']/summary" />
    public static IServiceCollection AddWebGetFileProvider(
        this IServiceCollection services,
        Action<WebGetFileProviderOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        ThrowIfProviderAlreadyRegistered<WebGetFileProvider>(services);

        services.AddKernelClockDefaults();
        services.AddWebGetOptions(configure);
        services.AddWebGetHttpClient();
        services.AddSingleton<WebGetFileProvider>(CreateWebGetFileProvider);
        services.AddSingleton<IVfsProvider>(serviceProvider =>
            serviceProvider.GetRequiredService<WebGetFileProvider>());

        return services;
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.DependencyInjection.AIKernelCoreVfsServiceCollectionExtensions.AddWebGetFileProvider']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.DependencyInjection.AIKernelCoreVfsServiceCollectionExtensions.AddWebGetFileProvider']/summary" />
    public static IServiceCollection AddWebGetFileProvider(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ThrowIfProviderAlreadyRegistered<WebGetFileProvider>(services);

        services.AddKernelClockDefaults();
        services.AddWebGetOptions(configuration);
        services.AddWebGetHttpClient();
        services.AddSingleton<WebGetFileProvider>(CreateWebGetFileProvider);
        services.AddSingleton<IVfsProvider>(serviceProvider =>
            serviceProvider.GetRequiredService<WebGetFileProvider>());

        return services;
    }

    private static IServiceCollection AddMemoryOptions(
        this IServiceCollection services,
        Action<MemoryFileProviderOptions>? configure)
    {
        services.AddSingleton<IValidateOptions<MemoryFileProviderOptions>, MemoryFileProviderOptionsValidator>();

        var builder = services.AddOptions<MemoryFileProviderOptions>();

        if (configure is not null)
        {
            builder.Configure(configure);
        }

        builder.ValidateOnStart();

        return services;
    }

    private static IServiceCollection AddMemoryOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IValidateOptions<MemoryFileProviderOptions>, MemoryFileProviderOptionsValidator>();
        services.AddOptions<MemoryFileProviderOptions>().Bind(configuration).ValidateOnStart();
        return services;
    }

    private static IServiceCollection AddLocalOptions(
        this IServiceCollection services,
        Action<LocalFileProviderOptions> configure)
    {
        services.AddSingleton<IValidateOptions<LocalFileProviderOptions>, LocalFileProviderOptionsValidator>();
        services.AddOptions<LocalFileProviderOptions>().Configure(configure).ValidateOnStart();
        return services;
    }

    private static IServiceCollection AddLocalOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IValidateOptions<LocalFileProviderOptions>, LocalFileProviderOptionsValidator>();
        services.AddOptions<LocalFileProviderOptions>().Bind(configuration).ValidateOnStart();
        return services;
    }

    private static IServiceCollection AddWebGetOptions(
        this IServiceCollection services,
        Action<WebGetFileProviderOptions> configure)
    {
        services.AddSingleton<IValidateOptions<WebGetFileProviderOptions>, WebGetFileProviderOptionsValidator>();
        services.AddOptions<WebGetFileProviderOptions>().Configure(configure).ValidateOnStart();
        return services;
    }

    private static IServiceCollection AddWebGetOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IValidateOptions<WebGetFileProviderOptions>, WebGetFileProviderOptionsValidator>();
        services.AddOptions<WebGetFileProviderOptions>().Bind(configuration).ValidateOnStart();
        return services;
    }

    private static IServiceCollection AddWebGetHttpClient(
        this IServiceCollection services)
    {
        services.AddHttpClient(
            WebGetFileProviderDefaults.HttpClientName,
            (serviceProvider, httpClient) =>
            {
                var options = serviceProvider
                    .GetRequiredService<IOptions<WebGetFileProviderOptions>>()
                    .Value;

                if (options.BaseUri is not null)
                {
                    httpClient.BaseAddress = options.BaseUri;
                }

                httpClient.Timeout = options.Timeout;
            });

        return services;
    }

    private static MemoryFileProvider CreateMemoryFileProvider(
        IServiceProvider serviceProvider)
    {
        var options = serviceProvider.GetRequiredService<IOptions<MemoryFileProviderOptions>>();
        var clock = serviceProvider.GetRequiredService<IKernelClock>();

        return new MemoryFileProvider(options, clock);
    }

    private static WebGetFileProvider CreateWebGetFileProvider(
        IServiceProvider serviceProvider)
    {
        var options = serviceProvider.GetRequiredService<IOptions<WebGetFileProviderOptions>>();
        var factory = serviceProvider.GetRequiredService<IHttpClientFactory>();
        var clock = serviceProvider.GetRequiredService<IKernelClock>();

        return new WebGetFileProvider(
            options,
            factory.CreateClient(WebGetFileProviderDefaults.HttpClientName),
            clock);
    }

    private static IServiceCollection AddKernelClockDefaults(
        this IServiceCollection services)
    {
        services.TryAddSingleton<IKernelClock>(_ => KernelClock.System());
        services.TryAddSingleton<AIKernel.Abstractions.Time.IKernelClock>(
            serviceProvider => (AIKernel.Abstractions.Time.IKernelClock)
                serviceProvider.GetRequiredService<IKernelClock>());
        services.TryAddSingleton(serviceProvider =>
            serviceProvider.GetRequiredService<IKernelClock>().Physical);
        services.TryAddSingleton(serviceProvider =>
            serviceProvider.GetRequiredService<IKernelClock>().Logical);

        return services;
    }

    private static void ThrowIfProviderAlreadyRegistered<TProvider>(
        IServiceCollection services)
    {
        var alreadyRegistered = services.Any(descriptor =>
            descriptor.ServiceType == typeof(TProvider)
            || (descriptor.ServiceType == typeof(IVfsProvider)
                && descriptor.ImplementationType == typeof(TProvider)));

        if (alreadyRegistered)
        {
            throw new InvalidOperationException(
                $"{typeof(TProvider).Name} is already registered as {nameof(IVfsProvider)}.");
        }
    }
}
