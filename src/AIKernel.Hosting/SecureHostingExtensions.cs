namespace AIKernel.Hosting;

using AIKernel.Abstractions.Security;
using AIKernel.Core.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

/// <summary>[EN] Documents this public package API member. [JA] SecureHostingExtensions を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Hosting.SecureHostingExtensions']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Hosting.SecureHostingExtensions']/summary" />
public static class SecureHostingExtensions
{
    /// <summary>[EN] Documents this public package API member. [JA] WithSecureOptions&lt;TOptions&gt; を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Hosting.SecureHostingExtensions.WithSecureOptions&lt;TOptions&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Hosting.SecureHostingExtensions.WithSecureOptions&lt;TOptions&gt;']/summary" />
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

    /// <summary>[EN] Documents this public package API member. [JA] WithSecureOptions&lt;TOptions&gt; を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Hosting.SecureHostingExtensions.WithSecureOptions&lt;TOptions&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Hosting.SecureHostingExtensions.WithSecureOptions&lt;TOptions&gt;']/summary" />
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

    /// <summary>[EN] Documents this public package API member. [JA] AddSecureCredentialResolution&lt;TOptions&gt; を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Hosting.SecureHostingExtensions.AddSecureCredentialResolution&lt;TOptions&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Hosting.SecureHostingExtensions.AddSecureCredentialResolution&lt;TOptions&gt;']/summary" />
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