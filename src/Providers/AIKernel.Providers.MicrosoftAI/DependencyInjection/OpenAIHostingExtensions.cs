namespace AIKernel.Providers.MicrosoftAI.DependencyInjection;

using AIKernel.Abstractions.Providers;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

public static class OpenAIHostingExtensions
{
    public static TBuilder WithOpenAI<TBuilder>(
        this TBuilder builder,
        Action<OpenAICompatibleProviderOptions> configure,
        Func<IServiceProvider, OpenAICompatibleProviderOptions, IChatClient> chatClientFactory)
        where TBuilder : notnull
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        ArgumentNullException.ThrowIfNull(chatClientFactory);

        var services = GetServices(builder);

        services
            .AddOptions<OpenAICompatibleProviderOptions>()
            .Configure(configure);

        RegisterOpenAIProvider(services, chatClientFactory);

        return builder;
    }

    public static TBuilder WithOpenAI<TBuilder>(
        this TBuilder builder,
        IConfiguration configurationSection,
        Func<IServiceProvider, OpenAICompatibleProviderOptions, IChatClient> chatClientFactory)
        where TBuilder : notnull
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configurationSection);
        ArgumentNullException.ThrowIfNull(chatClientFactory);

        var services = GetServices(builder);

        services
            .AddOptions<OpenAICompatibleProviderOptions>()
            .Bind(configurationSection);

        RegisterOpenAIProvider(services, chatClientFactory);

        return builder;
    }

    private static void RegisterOpenAIProvider(
        IServiceCollection services,
        Func<IServiceProvider, OpenAICompatibleProviderOptions, IChatClient> chatClientFactory)
    {
        services.AddLogging();

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<
                IValidateOptions<OpenAICompatibleProviderOptions>,
                OpenAICompatibleProviderOptionsValidator>());

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<
                IHostedService,
                OpenAICompatibleProviderStartupValidator>());

        services.TryAddSingleton<IOpenAICompatibleResponseMapper, OpenAICompatibleResponseMapper>();

        services.AddSingleton<IChatClient>(serviceProvider =>
        {
            var options = serviceProvider
                .GetRequiredService<IOptions<OpenAICompatibleProviderOptions>>()
                .Value;

            EnsureApiKeyResolved(options);

            return chatClientFactory(serviceProvider, options);
        });

        services.AddSingleton<IModelProvider, OpenAICompatibleProvider>();
    }

    private static IServiceCollection GetServices<TBuilder>(
        TBuilder builder)
        where TBuilder : notnull
    {
        var servicesProperty = builder
            .GetType()
            .GetProperty("Services", typeof(IServiceCollection));

        if (servicesProperty?.GetValue(builder) is IServiceCollection services)
        {
            return services;
        }

        var builderType = builder.GetType().FullName ?? builder.GetType().Name;

        throw new InvalidOperationException(
            $"Builder type '{builderType}' does not expose an IServiceCollection Services property.");
    }

    private static void EnsureApiKeyResolved(
        OpenAICompatibleProviderOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new InvalidOperationException(
                "OpenAICompatibleProviderOptions.ApiKey has not been resolved. Ensure host startup has completed and SecretKeyName is valid.");
        }
    }
}
