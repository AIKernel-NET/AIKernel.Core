namespace AIKernel.Hosting;

using AIKernel.Abstractions.Security;
using AIKernel.Core.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

public static class SecureHostingExtensions
{
    public static AIKernelCoreBuilder WithSecureOptions<TOptions>(
        this AIKernelCoreBuilder builder,
        Action<TOptions> configure)
        where TOptions : class, ISecureOptions, new()
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services
            .AddOptions<TOptions>()
            .Configure(configure)
            .ValidateOnStart();

        AddSecureCredentialResolution<TOptions>(builder.Services);

        return builder;
    }

    public static AIKernelCoreBuilder WithSecureOptions<TOptions>(
        this AIKernelCoreBuilder builder,
        IConfiguration configurationSection)
        where TOptions : class, ISecureOptions, new()
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configurationSection);

        builder.Services
            .AddOptions<TOptions>()
            .Bind(configurationSection)
            .ValidateOnStart();

        AddSecureCredentialResolution<TOptions>(builder.Services);

        return builder;
    }

    public static IServiceCollection AddSecureCredentialResolution<TOptions>(
        this IServiceCollection services)
        where TOptions : class, ISecureOptions
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<ISecureCredentialProvider, EnvironmentCredentialProvider>();

        services.TryAddSingleton<SecureCredentialResolver<TOptions>>();

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, SecureOptionsStartupValidator<TOptions>>());

        return services;
    }
}