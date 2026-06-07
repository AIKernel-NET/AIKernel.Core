namespace AIKernel.Hosting;

using AIKernel.Abstractions.Security;
using AIKernel.Core.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Hosting.SecureHostingExtensions']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Hosting.SecureHostingExtensions']" />
public static class SecureHostingExtensions
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Hosting.SecureHostingExtensions.WithSecureOptions&lt;TOptions&gt;']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Hosting.SecureHostingExtensions.WithSecureOptions&lt;TOptions&gt;']" />
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

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Hosting.SecureHostingExtensions.WithSecureOptions&lt;TOptions&gt;']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Hosting.SecureHostingExtensions.WithSecureOptions&lt;TOptions&gt;']" />
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

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Hosting.SecureHostingExtensions.AddSecureCredentialResolution&lt;TOptions&gt;']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Hosting.SecureHostingExtensions.AddSecureCredentialResolution&lt;TOptions&gt;']" />
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