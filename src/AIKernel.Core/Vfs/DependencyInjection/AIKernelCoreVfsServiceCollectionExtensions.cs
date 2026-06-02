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

public static class AIKernelCoreVfsServiceCollectionExtensions
{
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

    public static IServiceCollection AddMemoryFileProvider(
        this IServiceCollection services,
        Action<MemoryFileProviderOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ThrowIfProviderAlreadyRegistered<MemoryFileProvider>(services);

        services.AddKernelClockDefaults();
        services.AddMemoryOptions(configure);
        services.AddSingleton<IVfsProvider, MemoryFileProvider>();

        return services;
    }

    public static IServiceCollection AddMemoryFileProvider(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ThrowIfProviderAlreadyRegistered<MemoryFileProvider>(services);

        services.AddKernelClockDefaults();
        services.AddMemoryOptions(configuration);
        services.AddSingleton<IVfsProvider, MemoryFileProvider>();

        return services;
    }

    public static IServiceCollection AddLocalFileProvider(
        this IServiceCollection services,
        Action<LocalFileProviderOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        ThrowIfProviderAlreadyRegistered<LocalFileProvider>(services);

        services.AddKernelClockDefaults();
        services.AddLocalOptions(configure);
        services.AddSingleton<IVfsProvider, LocalFileProvider>();

        return services;
    }

    public static IServiceCollection AddLocalFileProvider(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ThrowIfProviderAlreadyRegistered<LocalFileProvider>(services);

        services.AddKernelClockDefaults();
        services.AddLocalOptions(configuration);
        services.AddSingleton<IVfsProvider, LocalFileProvider>();

        return services;
    }

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
        services.AddSingleton<IVfsProvider, WebGetFileProvider>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<WebGetFileProviderOptions>>();
            var factory = serviceProvider.GetRequiredService<IHttpClientFactory>();
            var clock = serviceProvider.GetRequiredService<IKernelClock>();
            return new WebGetFileProvider(
                options,
                factory.CreateClient(WebGetFileProviderDefaults.HttpClientName),
                clock);
        });

        return services;
    }

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
        services.AddSingleton<IVfsProvider, WebGetFileProvider>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<WebGetFileProviderOptions>>();
            var factory = serviceProvider.GetRequiredService<IHttpClientFactory>();
            var clock = serviceProvider.GetRequiredService<IKernelClock>();
            return new WebGetFileProvider(
                options,
                factory.CreateClient(WebGetFileProviderDefaults.HttpClientName),
                clock);
        });

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

    private static IServiceCollection AddKernelClockDefaults(
        this IServiceCollection services)
    {
        services.TryAddSingleton<IKernelClock>(_ => KernelClock.System());
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
            descriptor.ServiceType == typeof(IVfsProvider)
            && descriptor.ImplementationType == typeof(TProvider));

        if (alreadyRegistered)
        {
            throw new InvalidOperationException(
                $"{typeof(TProvider).Name} is already registered as {nameof(IVfsProvider)}.");
        }
    }
}
