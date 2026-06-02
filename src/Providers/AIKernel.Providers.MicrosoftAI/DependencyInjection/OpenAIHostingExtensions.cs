namespace AIKernel.Providers.MicrosoftAI.DependencyInjection;

using AIKernel.Abstractions.Providers;
using AIKernel.Hosting;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

public static class OpenAIHostingExtensions
{
    public static AIKernelCoreBuilder WithOpenAI(
        this AIKernelCoreBuilder builder,
        Action<OpenAICompatibleProviderOptions> configure,
        Func<IServiceProvider, OpenAICompatibleProviderOptions, IChatClient> chatClientFactory)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        ArgumentNullException.ThrowIfNull(chatClientFactory);

        builder.WithSecureOptions(configure);

        RegisterOpenAIProvider(
            builder.Services,
            chatClientFactory);

        return builder;
    }

    public static AIKernelCoreBuilder WithOpenAI(
        this AIKernelCoreBuilder builder,
        IConfiguration configurationSection,
        Func<IServiceProvider, OpenAICompatibleProviderOptions, IChatClient> chatClientFactory)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configurationSection);
        ArgumentNullException.ThrowIfNull(chatClientFactory);

        builder.WithSecureOptions<OpenAICompatibleProviderOptions>(
            configurationSection);

        RegisterOpenAIProvider(
            builder.Services,
            chatClientFactory);

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
